using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Inventory {

    public class ThinkNode_LoadoutRealisation : ThinkNode_ConditionalColonist {

        private Dictionary<Pawn, int> nextUpdateTick = new Dictionary<Pawn, int>();

        private static JobDef EquipApparel => JobDefOf.Wear;
        private static JobDef RemoveApparel => JobDefOf.RemoveApparel;
        private static JobDef EquipItem => JobDefOf.TakeInventory;
        private static JobDef UnloadItem => InvJobDefOf.CL_UnloadInventory;

        // Thoughts on how to do design this properly:
        // 1. There should be only one point in the mod where AI is impacted, and
        // all actions should be directly traceable to simple logic in that point
        // 2. There should be multiple levels of priority, (pawn immediately
        // equipping apparel they have just hot-swapped to / passive pawn-searching
        // for items/apparel from their loadout)
        // 3. This should utilise vanilla apparel-scoring such that mods which patch
        // JobGiver_OptimiseApparel do not become defunct

        // Current thoughts on how to actually do this
        // 1. Make a High-Priority ThinkNode which has a couple methods, roughly
        //      - RemovePawnExcessItems(Pawn), removes items held/equipped by a pawn
        // from tag(s) which have just been disabled 
        //      - PawnGetRequiredItems(Pawn), makes the pawn seek out items which 
        // have just been enabled by adding a new tag/enabling a tag
        //      - PawnSatisfyLoadout(Pawn), makes the pawn seek out items passively 
        // which match their current loadout, and remove items which are in excess
        // to that which they are looking for in their loadout.
        // 2. Replace JobGiver_OptimiseApparel, but use their methods in order to score
        // apparel to wear, hopefully maintaining compatibility with mods which bias
        // which apparel should be worn
        public ThinkNode_LoadoutRealisation() { }

        public override bool Satisfied(Pawn pawn) {
            if (!pawn.IsValidLoadoutHolder()) return true;
            // nothing to do on a caravan
            if (pawn.Map == null) return true;
            
            return false;
        }

        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams) {
            var comp = pawn.TryGetComp<LoadoutComponent>();

            // 1. Does the pawn have any things which need to be removed immediately? This can occur from:
            // - tag has just been deleted
            // - tag removed from loadout
            // - tag just disabled via hotswap mechanism

            // if ( comp.RemoveThings ) {
            //      var removeThingsJob = RemoveThingsJob();
            //      if ( removeThingsJob != null) {
            //          return new ThinkResult(removeThingsJob, this);
            //      }
            // }

            // 2. Does the pawn have any items which need to be equipped immediately? This can occur from:
            // - a tag has just been added to a pawns loadout
            // - a tag has just been enabled via hotswap mechanism

            // if ( comp.EquipItems ) {
            //      var equipItemsJob = EquipItemsJob();
            //      if ( equipItemsJob != null) {
            //          return new ThinkResult(equipItemsJob, this);
            //      }
            // }

            // 3. Is the pawns loadout fully satisfied, or does it still need to get some more things?

            if (PawnNeedsUpdate(pawn)) {
                var job = SatisfyLoadoutItemsJob(pawn, comp.Loadout);
                if (job != null) {
                    return new ThinkResult(job, this);
                }

                job = SatisfyLoadoutClothingJob(pawn, comp.Loadout);
                if (job != null) {
                    return new ThinkResult(job, this);
                }

                // if we have no job, it means there are no items on the map which match those required
                // by the pawns loadout, or their loadout is fully satisfied, either way we do not need
                // to re-check the pawns loadout status for a while (10-15k) ticks.
                SetPawnLastUpdated(pawn);
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

        // a heavily modified version of JobGiver_OptimizeApparel:TryGiveJob
        private Job SatisfyLoadoutClothingJob(Pawn pawn, Loadout loadout) {
            var wornApparel = pawn.apparel.WornApparel;
            var list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel)
                .OfType<Apparel>()
                .Where(app => loadout.Items.Any(item => item.Allows(app)))
                .ToList();
            
            if (list.Count == 0) {
                return null;
            }
            
            JobGiver_OptimizeApparel.neededWarmth = PawnApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.Tile, GenLocalDate.Twelfth(pawn));
            JobGiver_OptimizeApparel.wornApparelScores.Clear();
            foreach (var apparel in wornApparel) {
                JobGiver_OptimizeApparel.wornApparelScores.Add(JobGiver_OptimizeApparel.ApparelScoreRaw(pawn, apparel));
            }

            Apparel bestApparel = null;
            var bestApparelScore = 0f;

            foreach (var apparel in list) {
                if (!ValidApparelFor(apparel, pawn)) {
                    continue;
                }
                
                var apparelScore = JobGiver_OptimizeApparel.ApparelScoreGain(pawn, apparel, JobGiver_OptimizeApparel.wornApparelScores);

                if (apparelScore < 0.05f || apparelScore < bestApparelScore) continue;
                if (!pawn.CanReserveAndReach(apparel, PathEndMode.OnCell, pawn.NormalMaxDanger())) continue; 

                bestApparel = apparel;
                bestApparelScore = apparelScore;
            }

            return bestApparel == null ? null : JobMaker.MakeJob(EquipApparel, bestApparel);
        }

        private bool ValidApparelFor(Apparel apparel, Pawn pawn) {
            if (!pawn.outfits.CurrentOutfit.filter.Allows(apparel)) return false;
            if (apparel.IsForbidden(pawn)) return false;
            if (apparel.IsBurning()) return false;
            if (apparel.def.apparel.gender != Gender.None && apparel.def.apparel.gender != pawn.gender) return false;
            if (CompBiocodable.IsBiocoded(apparel) && !CompBiocodable.IsBiocodedFor(apparel, pawn)) return false;
            if (!RimWorld.ApparelUtility.HasPartsToWear(pawn, apparel.def)) return false;
            return true;
        }

        private Job SatisfyLoadoutItemsJob(Pawn pawn, Loadout loadout) {
            var pawnGear = pawn.InventoryAndEquipment().ToList();

            foreach (var item in loadout.Items.Where(item => !item.Def.IsApparel)) {
                var itemCount = item.CountIn(pawnGear);
                var loadoutDesiredCount = loadout.DesiredCount(pawnGear, item);

                // if we have the ideal amount for our item, we can just continue and consider this item
                // satisfied
                if (itemCount == loadoutDesiredCount) {
                    continue;
                }
                
                // we need to pick up some more of this item to consider it satisfied
                if (itemCount < loadoutDesiredCount) {
                    var job = FindItem(pawn, item, loadoutDesiredCount - itemCount);
                    if (job != null) {
                        return job;
                    }
                }
                else {
                    // we have too many for this item, but what happens if there are other tags which
                    // have this item in it too? we need to check for them too. I.e. if multiple tags
                    // want a pawn to pick up medicine, we need to remove only if we are above the sum
                    // of quantities of all those items in those tags.

                    if (loadoutDesiredCount >= itemCount) {
                        continue;
                    }

                    var job = RemoveItem(pawnGear, item, itemCount - loadoutDesiredCount);
                    if (job != null) {
                        return job;
                    }
                }
            }

            return null;
        }

        private Job FindItem(Pawn pawn, Item item, int count) {
            var things = item.ThingsOnMap(pawn.Map);

            foreach (var thing in things.OrderByDescending(t => t.stackCount)) {
                if ( !pawn.CanReserve(thing) ) continue;
                if ( !pawn.CanReach(thing, PathEndMode.Touch, Danger.Unspecified) ) continue;
                if (!thing.IsForbidden(pawn)) continue;
                
                var job = JobMaker.MakeJob(EquipItem, thing);
                job.count = Mathf.Min(count, thing.stackCount);

                return job;
            }

            return null;
        }

        private Job RemoveItem(List<Thing> pawnGear, Item item, int count) {
            var gear = pawnGear.Where(item.Allows).OrderByDescending(thing => thing.stackCount).ToList();

            var thing = gear.FirstOrDefault();
            if (thing == null) return null;

            var job = JobMaker.MakeJob(UnloadItem, thing);
            job.count = Mathf.Min(count, thing.stackCount);

            return job;
        }

    }

}