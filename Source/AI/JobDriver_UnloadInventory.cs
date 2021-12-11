using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Inventory
{
	// Virtually identical to JobDriver_UnloadYourInventory
	// but it doesn't check `this.pawn.inventory.UnloadEverything`
    public class JobDriver_UnloadInventory : JobDriver
    {
	    private int countToDrop = -1;
	    private const TargetIndex ItemToHaulInd = TargetIndex.A;
	    private const TargetIndex StoreCellInd = TargetIndex.B;
	    
		public override void ExposeData()
		{
			Scribe_Values.Look(ref this.countToDrop, "countToDrop");
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

		public override IEnumerable<Toil> MakeNewToils()
		{
			yield return Toils_General.Wait(10);
			
			yield return new Toil
			{
				initAction = delegate()
				{
					if (!this.pawn.inventory.HasAnyUnloadableThing)
					{
						EndJobWith(JobCondition.Succeeded);
						return;
					}
					var firstUnloadableThing = this.pawn.inventory.FirstUnloadableThing;
					if (!StoreUtility.TryFindStoreCellNearColonyDesperate(firstUnloadableThing.Thing, this.pawn, out var cell))
					{
						this.pawn.inventory.innerContainer.TryDrop(firstUnloadableThing.Thing, ThingPlaceMode.Near, firstUnloadableThing.Count, out var _);
						EndJobWith(JobCondition.Succeeded);
						return;
					}
					this.job.SetTarget(TargetIndex.A, firstUnloadableThing.Thing);
					this.job.SetTarget(TargetIndex.B, cell);
					this.countToDrop = firstUnloadableThing.Count;
				}
			};
			yield return Toils_Reserve.Reserve(TargetIndex.B);
			yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.Touch);
			yield return new Toil
			{
				initAction = delegate()
				{
					Thing thing = this.job.GetTarget(TargetIndex.A).Thing;
					if (thing == null || !this.pawn.inventory.innerContainer.Contains(thing))
					{
						EndJobWith(JobCondition.Incompletable);
						return;
					}
					if (!this.pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation) || !thing.def.EverStorable(false))
					{
						this.pawn.inventory.innerContainer.TryDrop(thing, ThingPlaceMode.Near, this.countToDrop, out _);
						EndJobWith(JobCondition.Succeeded);
					}
					else
					{
						this.pawn.inventory.innerContainer.TryTransferToContainer(thing, this.pawn.carryTracker.innerContainer, this.countToDrop, out _);
						this.job.count = this.countToDrop;
						this.job.SetTarget(TargetIndex.A, thing);
					}
					thing.SetForbidden(false, false);
				}
			};
			Toil carryToCell = Toils_Haul.CarryHauledThingToCell(TargetIndex.B);
			yield return carryToCell;
			yield return Toils_Haul.PlaceHauledThingInCell(TargetIndex.B, carryToCell, true);
		}


    }
}