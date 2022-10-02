using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Inventory {

    public static class Utility {

        public static List<ThingDef> apparelDefs = null;
        public static Dictionary<ThingDef, Func<Thing, float>> massBoostingClothes = null;
        public static List<ThingDef> meleeWeapons = null;
        public static List<ThingDef> rangedWeapons = null;
        public static List<ThingDef> medicinalDefs = null;
        public static List<ThingDef> items = null;

        public static void CalculateDefLists() {
            items ??= DefDatabase<ThingDef>.AllDefsListForReading.Where(td => !(
                !td.EverHaulable
                || td.IsFrame
                || td.destroyOnDrop
                || !td.PlayerAcquirable
                || typeof(UnfinishedThing).IsAssignableFrom(td.thingClass)
                || typeof(MinifiedThing).IsAssignableFrom(td.thingClass)
                || td.IsCorpse)).ToList();

            apparelDefs ??= items.Where(def => def.IsApparel).ToList();
            massBoostingClothes = new Dictionary<ThingDef, Func<Thing, float>>();
            meleeWeapons ??= items.Where(def => def.IsMeleeWeapon).ToList();
            rangedWeapons ??= items.Where(def => def.IsRangedWeapon && def.category != ThingCategory.Building).ToList();
            medicinalDefs ??= items.Where(def => def.IsMedicine || def.IsDrug).ToList();

            if (ModLister.HasActiveModWithName("Vanilla Apparel Expanded — Accessories")) {
                var type = AccessTools.TypeByName("VAE_Accessories.CaravanCapacityApparelDef");
                var field = type.GetField("carryingCapacity", BindingFlags.Instance | BindingFlags.Public);
                
                foreach (var def in apparelDefs) {
                    if (def.GetType() == type) {
                        var carryingCapacity = (float)field.GetValue(def);
                        massBoostingClothes.Add(def, (thing) => {
                            if (thing.TryGetQuality(out var qc)) {
                                return carryingCapacity * qc switch {
                                    QualityCategory.Awful      => 0.5f,
                                    QualityCategory.Poor       => 0.8f,
                                    QualityCategory.Normal     => 1.0f,
                                    QualityCategory.Good       => 1.2f,
                                    QualityCategory.Excellent  => 1.5f,
                                    QualityCategory.Masterwork => 1.7f,
                                    QualityCategory.Legendary  => 2.0f,
                                    _                          => 0
                                };
                            }

                            return carryingCapacity;
                        });
                    }
                }
            }
        }

        public static QualityCategory Next(this QualityCategory qc) {
            return (QualityCategory)Mathf.Min((int)qc + 1, (int)QualityCategory.Legendary);
        }

        public static QualityCategory Previous(this QualityCategory qc) {
            return (QualityCategory)Mathf.Max((int)qc - 1, (int)QualityCategory.Awful);
        }

        public static Thing MakeThingWithoutID(ThingDef def, ThingDef stuff, QualityCategory quality) {
            var thing = (Thing)Activator.CreateInstance(def.thingClass);
            thing.def = def;
            if (def.MadeFromStuff)
                thing.SetStuffDirect(stuff);
            if (thing.def.useHitPoints)
                thing.HitPoints = thing.MaxHitPoints;

            if (thing is ThingWithComps thingWithComps)
                thingWithComps.InitializeComps();

            thing.TryGetComp<CompQuality>()?.SetQuality(quality, ArtGenerationContext.Outsider);

            return thing;
        }

        public static float HypotheticalEncumberancePercent(Pawn p, List<Item> items) {
            return Mathf.Clamp01(HypotheticalUnboundedEncumberancePercent(p, items));
        }

        public static float HypotheticalUnboundedEncumberancePercent(Pawn p, List<Item> items) {
            return HypotheticalGearAndInventoryMass(p, items) / HypotheticalCapacity(p, items);
        }

        public static float HypotheticalGearAndInventoryMass(Pawn p, List<Item> items) {
            var mass = 0f;
            foreach (var item in items) {
                var thing = item.MakeDummyThingNoId();
                mass += (thing.GetStatValue(StatDefOf.Mass) * item.Quantity);
            }

            return mass;
        }

        public static float HypotheticalCapacity(Pawn p, List<Item> items) {
            if (!MassUtility.CanEverCarryAnything(p)) {
                return 0f;
            }

            var massCapacity = p.BodySize * 35f;

            if (massBoostingClothes.Count == 0) {
                return massCapacity;
            }
            
            foreach (var item in items.Where(item => massBoostingClothes.ContainsKey(item.Def))) {
                var dummyItem = item.MakeDummyThingNoId();
                massCapacity += massBoostingClothes[item.Def](dummyItem);
            }

            return massCapacity;
        }


        public static LoadoutState GetActiveState(this Pawn p) {
            var comp = p.TryGetComp<LoadoutComponent>();
            
            return comp is not null ? comp.Loadout.CurrentState : null;
        }

        public static void SetActiveState(this Pawn p, LoadoutState state, bool immediatelyResolve = false) {
            var loadout = p.TryGetComp<LoadoutComponent>().Loadout;
            loadout.SetState(state);
            if ( immediatelyResolve ) {
                loadout.RequiresUpdate();
            }
        }

        public static bool IsValidLoadoutHolder(this Pawn pawn) {
            return pawn.RaceProps.Humanlike
                   && !pawn.RaceProps.Animal
                   && pawn.IsColonist
                   && !pawn.Dead
                   && !pawn.IsQuestLodger()
                   && !(pawn.apparel?.AnyApparelLocked ?? true);
        }

        public static IEnumerable<Thing> InventoryAndEquipment(this Pawn pawn) {
            return pawn.inventory.innerContainer.InnerListForReading
                .ConcatIfNotNull(pawn.equipment.AllEquipmentListForReading);
        }
        
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)       
            => self.Select((item, index) => (item, index)); 
        
        public static void SetOrAppend<K, V>(this Dictionary<K, HashSet<V>> dictionary, K key, IEnumerable<V> elements) {
            if (dictionary.TryGetValue(key, out var elems)) {
                elems.AddRange(elements);
                return;
            }

            dictionary.Add(key, elements.ToHashSet());
        }
        public static bool ShouldAttemptToEquip(Pawn pawn, Thing thing, bool checkReach = false) {
            if (thing.IsForbidden(pawn)) return false;
            if (thing.IsBurning()) return false;
            if (CompBiocodable.IsBiocoded(thing) && !CompBiocodable.IsBiocodedFor(thing, pawn)) return false;
            if (checkReach && !pawn.CanReserveAndReach(thing, PathEndMode.OnCell, pawn.NormalMaxDanger())) return false;
            
            return true;
        }

        public static bool InvalidBill(this Bill bill) {
            if (bill == null) return true;
            if (bill.deleted) return true;

            return bill.billStack?.billGiver is not Thing bench || bench.Destroyed;
        }

        public static void EnqueueEmptyInventory(this Pawn pawn) {
            var firstUnloadableThing = pawn.inventory.FirstUnloadableThing;
            if (firstUnloadableThing == default) return;

            pawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(InvJobDefOf.CL_UnloadInventory), JobTag.DraftedOrder, true);
        }
    }

}