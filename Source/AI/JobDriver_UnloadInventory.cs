using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Inventory {

    // Virtually identical to JobDriver_UnloadYourInventory
    // but it doesn't check `this.pawn.inventory.UnloadEverything`
    public class JobDriver_UnloadInventory : JobDriver {

        static Func<Pawn, bool, object> getCompFromPawn = null;
        static FastInvokeHandler informOfDroppedSidearm = null;

        static bool simpleSidearmsIntegration = false;

        static JobDriver_UnloadInventory() {
            var ssLoaded = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLowerInvariant() == "petetimessix.simplesidearms");
            if ( !ssLoaded ) {
                return;
            }

            getCompFromPawn = AccessTools.MethodDelegate<Func<Pawn, bool, object>>(AccessTools.Method("CompSidearmMemory:GetMemoryCompForPawn"));
            informOfDroppedSidearm = MethodInvoker.GetHandler(AccessTools.Method("CompSidearmMemory:InformOfDroppedSidearm"));

            simpleSidearmsIntegration = getCompFromPawn is not null && informOfDroppedSidearm is not null;
            if ( simpleSidearmsIntegration ) {
                Log.Message("[Loadout Compositing] Enabling mod integrations with Simple Sidearms");
            } else {
                Log.Warning("[Loadout Compositing] Could not enable mod integrations with Simple Sidearms");
            }
        }

        private int countToDrop = -1;
        private bool specificItem = false;
        private const TargetIndex ItemToHaulInd = TargetIndex.A;
        private const TargetIndex StoreCellInd = TargetIndex.B;

        public override void ExposeData() {
            Scribe_Values.Look(ref countToDrop, nameof(countToDrop));
            Scribe_Values.Look(ref specificItem, nameof(specificItem));
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        public override IEnumerable<Toil> MakeNewToils() {
            var inven = pawn.inventory;

            var firstToil = Toils_General.Wait(10);
            yield return firstToil;
            var finalToil = new Toil() {
                initAction = () => {
                    if (!specificItem && pawn.inventory.FirstUnloadableThing != default) {
                        job.SetTarget(TargetIndex.A, LocalTargetInfo.Invalid);
                        JumpToToil(firstToil);
                    }
                }
            };
            
            yield return new Toil {
                initAction = delegate() {
                    countToDrop = 0;
                    
                    if (TargetA.HasThing) {
                        countToDrop = job.count;
                        specificItem = true;
                    } else {
                        var firstUnloadableThing = inven.FirstUnloadableThing;
                        if (firstUnloadableThing == default) {
                            EndJobWith(JobCondition.Succeeded);
                            return;
                        } 
                        
                        job.SetTarget(TargetIndex.A, firstUnloadableThing.Thing);
                        countToDrop = firstUnloadableThing.Count;
                    }

                    var thing = TargetA.Thing;
                    
                    if (!StoreUtility.TryFindStoreCellNearColonyDesperate(thing, pawn, out var cell)) {
                        inven.innerContainer.TryDrop(thing, ThingPlaceMode.Near, countToDrop, out var _);
                        pawn.jobs.curDriver.JumpToToil(finalToil);
                        return;
                    }

                    job.SetTarget(TargetIndex.B, cell);
                }
            };
            yield return Toils_Reserve.Reserve(TargetIndex.B);
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
            yield return new Toil {
                initAction = delegate() {
                    var thing = job.GetTarget(TargetIndex.A).Thing;
                    if (thing == null || !inven.pawn.InventoryAndEquipment().Contains(thing)) {
                        EndJobWith(JobCondition.Incompletable);
                        return;
                    }

                    // Simple sidearms integration. 
                    if (simpleSidearmsIntegration && thing.def.IsWeapon) {
                        var comp = getCompFromPawn(pawn, false);
                        informOfDroppedSidearm(comp, thing, true);
                    }

                    ThingOwner thingOwner = inven.innerContainer.Contains(thing) ? inven.innerContainer : inven.pawn.equipment.equipment;

                    if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStorable(false)) {
                        thingOwner.TryDrop(thing, ThingPlaceMode.Near, countToDrop, out _);
                        pawn.jobs.curDriver.JumpToToil(finalToil);
                        return;
                    } else {
                        thingOwner.TryTransferToContainer(thing, pawn.carryTracker.innerContainer, countToDrop, out _);
                        job.count = countToDrop;
                        job.SetTarget(TargetIndex.A, thing);
                    }

                    thing.SetForbidden(false, false);
                }
            };

            var carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
            yield return carryToCell;
            yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);
            yield return finalToil;
        }


    }


}