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

        private Dictionary<Pawn, int> nextUpdateTick = new Dictionary<Pawn, int>();

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
            if ( (pawn.needs.food?.CurLevelPercentage < pawn.needs.food?.PercentageThreshUrgentlyHungry) || (HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) < 45000) ) {
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

            foreach (var item in loadout.ThingsToRemove.ToList()) {
                var itemCount = item.CountIn(pawnGear);
                var loadoutDesiredCount = loadout.DesiredCount(pawnGear, item) - item.Quantity;

                if (itemCount > loadoutDesiredCount) {
                    var job = RemoveItem(pawnGear, item, Mathf.Min(itemCount - loadoutDesiredCount, item.Quantity));
                    if (job != null) {
                        if (job.count >= item.Quantity) {
                            loadout.ThingsToRemove.Remove(item);
                        }
                        return job;
                    }
                }

                loadout.ThingsToRemove.Remove(item);
            }

            return null;
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

            OptimizeApparel.neededWarmth = PawnApparelGenerator.CalculateNeededWarmth(pawn, pawn.Map.Tile, GenLocalDate.Twelfth(pawn));
            OptimizeApparel.wornApparelScores.Clear();
            foreach (var apparel in wornApparel) {
                OptimizeApparel.wornApparelScores.Add(OptimizeApparel.ApparelScoreRaw(pawn, apparel));
            }

            Apparel bestApparel = null;
            var bestApparelScore = 0f;

            foreach (var apparel in list) {
                if (!ValidApparelFor(apparel, pawn)) {
                    continue;
                }

                var apparelScore = OptimizeApparel.ApparelScoreGain(pawn, apparel, OptimizeApparel.wornApparelScores);

                if (apparelScore < 0.05f || apparelScore < bestApparelScore) continue;
                if (!pawn.CanReserveAndReach(apparel, PathEndMode.OnCell, pawn.NormalMaxDanger())) continue;

                bestApparel = apparel;
                bestApparelScore = apparelScore;
            }

            return bestApparel == null ? null : JobMaker.MakeJob(EquipApparel, bestApparel);
        }

        private bool ValidApparelFor(Apparel apparel, Pawn pawn) {
            if (!pawn.outfits.CurrentApparelPolicy.filter.Allows(apparel)) return false;
            if (apparel.def.apparel.gender != Gender.None && apparel.def.apparel.gender != pawn.gender) return false;
            if (!RimWorld.ApparelUtility.HasPartsToWear(pawn, apparel.def)) return false;

            return Utility.ShouldAttemptToEquip(pawn, apparel);
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
            var things = item.ThingsOnMap(pawn.Map).ToList();

            var orderedList = count == 1
                ? DecideItemPriority(pawn, things)
                : things.OrderByDescending(t => t.stackCount);
            
            foreach (var thing in orderedList) {
                if (!Utility.ShouldAttemptToEquip(pawn, thing, true)) {
                    continue;
                }

                if (count == 1 && thing.def.IsWeapon && pawn.equipment.Primary is null )
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

        private Job RemoveItem(List<Thing> pawnGear, Item item, int count) {
            if (count == 0) return null;
            
            var gear = pawnGear.Where(item.Allows).OrderByDescending(thing => thing.stackCount).ToList();

            var thing = gear.FirstOrDefault();
            if (thing == null) return null;

            var job = JobMaker.MakeJob(UnloadItem);
            job.SetTarget(TargetIndex.A, thing);
            job.count = Mathf.Min(count, thing.stackCount);

            return job;
        }

    }

}