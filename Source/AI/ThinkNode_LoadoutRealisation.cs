using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Inventory {

    using OptimizeApparel = JobGiver_OptimizeApparel;

    public class ThinkNode_LoadoutRealisation : ThinkNode_ConditionalColonist {

        // Think nodes outlive games, so this is cleared whenever a game is started or loaded: otherwise a loaded game's
        // pawns would inherit the previous game's timers, and the previous game's pawns would never be released.
        private static Dictionary<Pawn, int> nextUpdateTick = new Dictionary<Pawn, int>();

        public static void ResetUpdateTicks() => nextUpdateTick.Clear();

        private static JobDef EquipApparel => JobDefOf.Wear;
        private static JobDef EquipItem => JobDefOf.TakeInventory;
        private static JobDef HoldItem => JobDefOf.Equip;
        private static JobDef UnloadItem => InvJobDefOf.CL_UnloadInventory;
        
        public ThinkNode_LoadoutRealisation() { }

        public override bool Satisfied(Pawn pawn) {
            if (!pawn.IsValidLoadoutHolder()) return true;
            // nothing to do on a caravan
            if (pawn.Map == null) return true;

            return false;
        }

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams) {
            // For some reason, the `ThinkNode_SubtreesByTag` does not check to see if the ThinkNode is satisfied before querying it, which would negate the need for this check.
            if (!pawn.IsValidLoadoutHolder()) {
                return ThinkResult.NoJob;
            }

            // This is a relatively high priority node on the thinknode, so we do some sanity safeguards to check that the pawn
            // isn't shirking 'more important' responsibilities, like eating when they are starving, or walking around while they are dying!
            if (HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) < 45000) {
                return ThinkResult.NoJob;
            }

            if (pawn.needs.food is not null && pawn.needs.food.CurLevelPercentage < pawn.needs.food.PercentageThreshUrgentlyHungry) {
                return ThinkResult.NoJob;
            }

            var comp = pawn.TryGetComp<LoadoutComponent>();
            
            if (comp.Loadout.NeedsUpdate || PawnNeedsUpdate(pawn)) {
                
                if (comp.Loadout.ThingsToRemove.Count > 0) {
                    var removeThingsJob = RemoveThingsJob(pawn, comp.Loadout);
                    if (removeThingsJob != null) {
                        return new ThinkResult(removeThingsJob, this);
                    }
                }
                
                var job = SatisfyLoadoutClothingJob(pawn, comp.Loadout);
                if (job != null) {
                    return new ThinkResult(job, this);
                }
                
                job = SatisfyLoadoutItemsJob(pawn, comp.Loadout);
                if (job != null) {
                    return new ThinkResult(job, this);
                }

                // if we have no job, it means there are no items on the map which match those required
                // by the pawns loadout, or their loadout is fully satisfied, either way we do not need
                // to re-check the pawns loadout status for a while (10-15k) ticks.
                SetPawnLastUpdated(pawn);
                comp.Loadout.Updated();
            }

            return ThinkResult.NoJob;
        }

        private bool PawnNeedsUpdate(Pawn pawn) {
            if (!nextUpdateTick.TryGetValue(pawn, out var nextTick)) {
                nextTick = 0;
                nextUpdateTick.Add(pawn, nextTick);
            }

            return GenTicks.TicksAbs >= nextTick;
        }

        // pre-condition that in `nextUpdateTick` there should be an entry for `pawn`
        private void SetPawnLastUpdated(Pawn pawn) {
            nextUpdateTick[pawn] = GenTicks.TicksAbs + Rand.Range(10_000, 15_000);
        }

        private Job RemoveThingsJob(Pawn pawn, Loadout loadout) {

            var pawnGear = pawn.InventoryAndEquipment().ToList();
            var assignment = loadout.AssignGear(pawn, pawnGear);

            foreach (var item in loadout.ThingsToRemove.ToList()) {
                // anything the removed item accepts which the active items have not claimed is no longer needed
                var thing = pawnGear.Where(t => item.Allows(t) && assignment.Surplus(t) > 0)
                    .OrderByDescending(t => assignment.Surplus(t))
                    .FirstOrDefault();

                if (thing != null) {
                    var job = UnloadJob(thing, Mathf.Min(assignment.Surplus(thing), item.Quantity));
                    if (job.count >= item.Quantity) {
                        loadout.ThingsToRemove.Remove(item);
                    }
                    return job;
                }

                loadout.ThingsToRemove.Remove(item);
            }

            return null;
        }

        // a heavily modified version of JobGiver_OptimizeApparel:TryGiveJob
        private Job SatisfyLoadoutClothingJob(Pawn pawn, Loadout loadout) {
            var wornApparel = pawn.apparel.WornApparel;
            var list = CandidateApparel(pawn, loadout);

            if (list.Count == 0) {
                return null;
            }

            var upgradeJob = RankUpgradeJob(pawn, loadout, list);
            if (upgradeJob != null) {
                return upgradeJob;
            }

            OptimizeApparel.neededWarmth = PawnApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.Tile, GenLocalDate.Twelfth(pawn));
            OptimizeApparel.wornApparelScores.Clear();
            foreach (var apparel in wornApparel) {
                OptimizeApparel.wornApparelScores.Add(OptimizeApparel.ApparelScoreRaw(pawn, apparel));
            }

            Apparel bestApparel = null;
            var bestApparelScore = 0f;

            foreach (var apparel in list) {
                var apparelScore = OptimizeApparel.ApparelScoreGain(pawn, apparel, OptimizeApparel.wornApparelScores);

                if (apparelScore < 0.05f || apparelScore < bestApparelScore) continue;

                if (!CanReachToWear(pawn, apparel)) continue;

                bestApparel = apparel;
                bestApparelScore = apparelScore;
            }

            return bestApparel == null ? null : JobMaker.MakeJob(EquipApparel, bestApparel);
        }

        // Apparel which an active apparel item accepts and the pawn could wear. Looked up by the items' defs rather than
        // scanning all apparel on the map, and filtered before any (expensive) scoring.
        private List<Apparel> CandidateApparel(Pawn pawn, Loadout loadout) {
            var candidates = new List<Apparel>();
            var apparelItems = loadout.Items.Where(item => item.IsApparel).Distinct().ToList();
            if (apparelItems.Count == 0) {
                return candidates;
            }

            void Consider(Thing thing) {
                if (thing is Apparel apparel && apparelItems.Any(item => item.Allows(apparel)) && ValidApparelFor(apparel, pawn)) {
                    candidates.Add(apparel);
                }
            }

            var lister = pawn.Map.listerThings;
            foreach (var def in apparelItems.SelectMany(item => item.Defs).ToHashSet()) {
                foreach (var thing in lister.ThingsOfDef(def)) {
                    Consider(thing);
                }
            }

            // apparel held by things such as outfit stands isn't spawned, so isn't listed by def
            foreach (var source in lister.haulSources) {
                foreach (var thing in source.GetDirectlyHeldThings()) {
                    Consider(thing);
                }
            }

            return candidates;
        }

        // strict preference: swap worn apparel for a better ranked member of the same item, regardless of score
        private Job RankUpgradeJob(Pawn pawn, Loadout loadout, List<Apparel> candidates) {
            foreach (var worn in pawn.apparel.WornApparel) {
                var item = loadout.ItemsAccepting(worn).FirstOrDefault(i => i.IsApparel);
                if (item == null || item.Members.Count == 1) continue;

                var wornRank = item.RankOf(worn);
                var betterApparel = candidates
                    .Where(apparel => item.Allows(apparel) && item.RankOf(apparel) < wornRank)
                    .OrderBy(item.RankOf);

                foreach (var apparel in betterApparel) {
                    if (!OnlyDisplacesItem(pawn, loadout, item, apparel)) continue;
                    if (!CanReachToWear(pawn, apparel)) continue;

                    return JobMaker.MakeJob(EquipApparel, apparel);
                }
            }

            return null;
        }

        // would wearing `apparel` only take off apparel which `item` accepts, or which the loadout doesn't want
        private bool OnlyDisplacesItem(Pawn pawn, Loadout loadout, Item item, Apparel apparel) {
            foreach (var worn in pawn.apparel.WornApparel) {
                if (RimWorld.ApparelUtility.CanWearTogether(apparel.def, worn.def, pawn.RaceProps.body)) continue;
                if (pawn.apparel.IsLocked(worn)) return false;
                if (!item.Allows(worn) && loadout.Desires(worn)) return false;
            }

            return true;
        }

        private bool CanReachToWear(Pawn pawn, Apparel apparel) {
            // apparel held in a source (e.g. an outfit stand) is reserved and reached via the source, the
            // `Wear` job handles walking to the source and removing the apparel from it.
            LocalTargetInfo target = apparel;
            if (apparel.ParentHolder is IApparelSource source && source is Thing sourceThing) {
                if (!source.ApparelSourceEnabled) return false;
                target = sourceThing;
            }

            return pawn.CanReserveAndReach(target, PathEndMode.OnCell, pawn.NormalMaxDanger());
        }

        private bool ValidApparelFor(Apparel apparel, Pawn pawn) {
            if (!pawn.outfits.CurrentApparelPolicy.filter.Allows(apparel)) return false;
            if (apparel.def.apparel.gender != Gender.None && apparel.def.apparel.gender != pawn.gender) return false;
            if (!RimWorld.ApparelUtility.HasPartsToWear(pawn, apparel.def)) return false;

            return Utility.ShouldAttemptToEquip(pawn, apparel);
        }

        private Job SatisfyLoadoutItemsJob(Pawn pawn, Loadout loadout) {
            var pawnGear = pawn.InventoryAndEquipment().ToList();
            var assignment = loadout.AssignGear(pawn, pawnGear);
            var items = loadout.CarriedItems.Where(item => !item.IsApparel).Distinct().ToList();

            foreach (var item in items) {
                // fetch the best ranked members first, lower ranks only make up what the better ranks can't
                var remaining = item.Quantity;
                foreach (var rank in item.RankedMembers) {
                    if (remaining == 0) break;

                    var held = assignment.HeldOfRank(item, rank.Key);
                    if (held < remaining) {
                        var job = FindItem(pawn, item, rank, remaining - held);
                        if (job != null) {
                            return job;
                        }
                    }

                    remaining -= Mathf.Min(held, remaining);
                }
            }

            // things are only surplus once the items accepting them are full, so we never drop something before
            // its replacement is held. Multiple tags wanting the same thing are accounted for by the assignment, as
            // are things this pawn can't use (e.g. meals their food policy disallows), which are always surplus.
            foreach (var thing in assignment.SurplusThings) {
                if (items.Any(item => item.Allows(thing))) {
                    return UnloadJob(thing, assignment.Surplus(thing));
                }
            }

            return null;
        }

        // fetch `count` of the members of `rank`
        private Job FindItem(Pawn pawn, Item item, IGrouping<int, ItemMember> rank, int count) {
            var things = item.ThingsOnMap(pawn, rank).ToList();
            var primary = pawn.equipment.Primary;

            var orderedList = count == 1
                ? DecideItemPriority(pawn, things)
                : things.OrderByDescending(t => t.stackCount);
            
            foreach (var thing in orderedList) {
                // equipping swaps out a worse ranked primary of the same item
                var equip = count == 1 && thing.def.IsWeapon && (primary is null || item.RankOf(primary) > rank.Key);
                // taking to inventory reserves only the part of a stack taken, so pawns can share a stack
                if (!Utility.ShouldAttemptToEquip(pawn, thing, true, equip ? -1 : Mathf.Min(count, thing.stackCount))) {
                    continue;
                }

                if (equip)
                {
                    return JobMaker.MakeJob(HoldItem, thing);
                } else {
                    var job = JobMaker.MakeJob(EquipItem, thing);
                    job.count = Mathf.Min(count, thing.stackCount);

                    return job;
                }
            }

            return null;
        }

        // Mostly for better bio-coded weapon integration.
        private IOrderedEnumerable<Thing> DecideItemPriority(Pawn pawn, List<Thing> things)
        {
            if (things.Empty())
            {
                return new List<Thing>().OrderByDescending(t => t.stackCount);
            }
            
            var example = things.First();
            if (example.def.IsWeapon)
            {
                // If the things are bio-codable, if any are coded to the pawn - we should prioritize that one - if they
                // are coded to another person, we should ignore it.
                if (example.TryGetComp<CompBiocodable>()?.Biocodable ?? false)
                {
                    // Remove each thing which is bio-coded to a pawn other than the target    
                    things.RemoveAll(t =>
                    {
                        var comp = t.TryGetComp<CompBiocodable>();
                        return comp != null && comp.CodedPawn != null && comp.CodedPawn != pawn;
                    });
                
                    // If there exists a weapon which is coded to our pawn, lets equip that instead of an uncoded one, 
                    // otherwise pick the closest occurence.
                    var marked = things.Where(t => t.TryGetComp<CompBiocodable>()?.CodedPawn == pawn).ToList();
                    if (marked.Any())
                    {
                        things = marked;
                        // Special case the case we have a bonded weapon, but its forbidden
                        if (things.All(t => t.IsForbidden(pawn)))
                        {
                            Messages.Message(new Message(
                                $"{pawn.Name} cannot equip {string.Join(", ", things.Select(t => t.LabelShort))} as it is forbidden.", 
                                MessageTypeDefOf.SilentInput,
                                things
                            ));
                        }
                    }
                }

                if (example.def.IsMeleeWeapon)
                {
                    return things.OrderByDescending(t => t.GetStatValueForPawn(StatDefOf.MeleeWeapon_AverageDPS, pawn));
                }
            }
  
            return things.OrderBy(t => t.InteractionCell.DistanceToSquared(pawn.InteractionCell));
        }

        private Job UnloadJob(Thing thing, int count) {
            var job = JobMaker.MakeJob(UnloadItem);
            job.SetTarget(TargetIndex.A, thing);
            job.count = Mathf.Min(count, thing.stackCount);

            return job;
        }

    }

}