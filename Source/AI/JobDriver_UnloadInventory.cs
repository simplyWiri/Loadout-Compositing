using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Inventory {

    // Virtually identical to JobDriver_UnloadYourInventory
    // but it doesn't check `this.pawn.inventory.UnloadEverything`
    public class JobDriver_UnloadInventory : JobDriver {

        private int countToDrop = -1;
        private const TargetIndex ItemToHaulInd = TargetIndex.A;
        private const TargetIndex StoreCellInd = TargetIndex.B;

        public override void ExposeData() {
            Scribe_Values.Look(ref this.countToDrop, "countToDrop");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override IEnumerable<Toil> MakeNewToils() {
            var inven = pawn.inventory;
            
            yield return Toils_General.Wait(10);
            yield return new Toil {
                initAction = delegate() {
                    if (!inven.HasAnyUnloadableThing) {
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }

                    var firstUnloadableThing = inven.FirstUnloadableThing;
                    if (!StoreUtility.TryFindStoreCellNearColonyDesperate(firstUnloadableThing.Thing, pawn,
                            out var cell)) {
                        inven.innerContainer.TryDrop(firstUnloadableThing.Thing, ThingPlaceMode.Near,
                            firstUnloadableThing.Count, out var _);
                        EndJobWith(JobCondition.Succeeded);
                        return;
                    }

                    job.SetTarget(TargetIndex.A, firstUnloadableThing.Thing);
                    job.SetTarget(TargetIndex.B, cell);
                    countToDrop = firstUnloadableThing.Count;
                }
            };
            yield return Toils_Reserve.Reserve(TargetIndex.B);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
            yield return new Toil {
                initAction = delegate() {
                    var thing = job.GetTarget(TargetIndex.A).Thing;
                    if (thing == null || !inven.innerContainer.Contains(thing)) {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) ||
                        !thing.def.EverStorable(false)) {
                        inven.innerContainer.TryDrop(thing, ThingPlaceMode.Near, countToDrop, out _);
                        EndJobWith(JobCondition.Succeeded);
                    }
                    else {
                        inven.innerContainer.TryTransferToContainer(thing, pawn.carryTracker.innerContainer, countToDrop, out _);
                        job.count = countToDrop;
                        job.SetTarget(TargetIndex.A, thing);
                    }

                    thing.SetForbidden(false, false);
                }
            };
            var carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);
        }

    }

}