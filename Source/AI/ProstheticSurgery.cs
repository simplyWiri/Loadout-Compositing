using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    public enum AutoSurgery : byte {
        Off = 0,
        Suspended = 1,
        Active = 2
    }

    // Queues the surgeries which install the prosthetics pawns' tags ask for. A surgery is only queued once a spare
    // prosthetic is on the map, so pawns don't queue bills which race each other for a single part.
    public static class ProstheticSurgery {
        public const int CheckInterval = GenDate.TicksPerHour;

        public static void QueueBills(Map map) {
            var mode = ModBase.settings.autoSurgery;
            if (mode == AutoSurgery.Off) return;

            var spare = new Dictionary<ThingDef, int>();
            var pawns = LoadoutManager.PawnsWithTags.Values.SelectMany(p => p).Distinct()
                .Where(p => p != null && p.Map == map && p.IsValidLoadoutHolder() && !p.Dead)
                .ToList();

            foreach (var pawn in pawns) {
                var loadout = pawn.TryGetComp<LoadoutComponent>().Loadout;
                var slots = loadout.HypotheticalProsthetics(loadout.CurrentState, pawn.RaceProps.body);
                var required = loadout.Items.Where(item => item.IsProsthetic).Select(item => item.RepresentativeDef).ToHashSet();

                foreach (var item in loadout.Items.Where(item => item.IsProsthetic).Distinct()) {
                    var def = item.RepresentativeDef;
                    var recipe = ProstheticUtility.InstallRecipeFor(pawn, def);
                    if (recipe == null || !recipe.AvailableNow || loadout.Declined(def)) continue;

                    var queued = pawn.BillStack.Bills.Count(bill => bill.recipe == recipe);
                    if (ProstheticUtility.SatisfiedCount(pawn, def) + queued >= item.Quantity) continue;

                    if (!spare.TryGetValue(def, out var spareCount)) {
                        spareCount = SpareCount(map, def);
                    }
                    if (spareCount <= 0) continue;

                    var part = ChoosePart(pawn, recipe, slots, required);
                    if (part == null && recipe.targetsBodyPart) continue;

                    var bill = HealthCardUtility.CreateSurgeryBill(pawn, recipe, part, sendMessages: false);
                    bill.suspended = mode == AutoSurgery.Suspended;
                    spare[def] = spareCount - 1;
                }
            }
        }

        // unforbidden prosthetics on the map, less those which install bills are already waiting on
        private static int SpareCount(Map map, ThingDef def) {
            var onMap = map.listerThings.ThingsOfDef(def).Where(t => !t.IsForbidden(Faction.OfPlayer)).Sum(t => t.stackCount);
            var recipes = ProstheticUtility.InstallRecipesFor(def);
            var waiting = map.mapPawns.AllPawnsSpawned.Sum(p => p.BillStack?.Bills.Count(bill => recipes.Contains(bill.recipe)) ?? 0);

            return onMap - waiting;
        }

        // The part to install onto: one this prosthetic is placed on (or which nothing higher priority wants), with no
        // bill of its own, which wouldn't remove a better prosthetic or one another tag requires. Missing parts first,
        // then damaged ones, healthy ones, and lastly replacing a lesser prosthetic.
        private static BodyPartRecord ChoosePart(Pawn pawn, RecipeDef recipe, ProstheticSlots slots, HashSet<ThingDef> required) {
            if (!recipe.targetsBodyPart) return null;

            var def = ProstheticUtility.ProstheticInstalledBy(recipe);
            var billedParts = pawn.BillStack.Bills.OfType<Bill_Medical>().Select(bill => bill.Part).ToHashSet();
            var hediffSet = pawn.health.hediffSet;

            bool Keeps(BodyPartRecord part) {
                var replaced = ProstheticUtility.IsAddedPart(recipe)
                    ? hediffSet.hediffs.Where(h => IsOnOrBelow(h.Part, part))
                    : hediffSet.hediffs.Where(h => h.Part == part);

                return replaced.Any(h => {
                    var thing = h.def.spawnThingOnRemoved;
                    var requiredElsewhere = thing != null && thing != def && required.Contains(thing);
                    return requiredElsewhere || ProstheticUtility.IsBetter(h.def, recipe.addsHediff);
                });
            }

            int Order(BodyPartRecord part) {
                if (hediffSet.PartIsMissing(part)) return 0;
                if (hediffSet.HasDirectlyAddedPartFor(part)) return 3;
                return hediffSet.hediffs.Any(h => h.Part == part && h.def.isBad) ? 1 : 2;
            }

            return recipe.Worker.GetPartsToApplyOn(pawn, recipe)
                .Where(part => recipe.AvailableOnNow(pawn, part) && !billedParts.Contains(part) && slots.Free(def, part) && !Keeps(part))
                .OrderBy(Order)
                .FirstOrDefault();
        }

        private static bool IsOnOrBelow(BodyPartRecord part, BodyPartRecord ancestor) {
            for (; part != null; part = part.parent) {
                if (part == ancestor) return true;
            }

            return false;
        }
    }

}
