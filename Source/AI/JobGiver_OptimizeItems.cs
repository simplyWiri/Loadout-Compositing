using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Inventory {
    public class JobGiver_OptimizeItems : ThinkNode_JobGiver
    {
        public static Dictionary<Pawn, int> nextUpdates = new Dictionary<Pawn, int>();
        
        public override Job TryGiveJob(Pawn pawn)
        {
            if (!pawn.IsValidLoadoutHolder()) 
                return null;
            
            if (!nextUpdates.TryGetValue(pawn, out var nextTick)) {
                nextTick = Find.TickManager.TicksGame + Rand.Range(10000, 15000);
                nextUpdates.Add(pawn, nextTick);
            }
            
            if (Find.TickManager.TicksGame < nextTick)
                return null;

            var comp = pawn.GetComp<LoadoutComponent>();

            var items = pawn.inventory.innerContainer.InnerListForReading.ConcatIfNotNull(pawn.equipment.AllEquipmentListForReading).ToList();
            var requiredItems = comp.Loadout.DesiredItems(items).ToList();

            // check to see if there are any required items on the map
            foreach (var item in requiredItems) {
                var thingsOnMap = pawn.Map.listerThings.ThingsOfDef(item.Def);
                if ( thingsOnMap.NullOrEmpty() ) continue;
                thingsOnMap = thingsOnMap.Where(thing => item.Filter.Allows(thing)).ToList();
                if (thingsOnMap.NullOrEmpty()) continue;

                foreach (var thing in thingsOnMap)
                {
                    if (!pawn.CanReserve(thing) || !pawn.CanReach(thing, PathEndMode.Touch, Danger.Unspecified) || thing.IsForbidden(pawn)) continue;
                    
                    var job = JobMaker.MakeJob(JobDefOf.TakeInventory, thing);
                    if (thing.def.category == ThingCategory.Item)
                    {
                        var currentlyHeldAmount = items.Where(th => th.def == item.Def && item.Filter.Allows(th)).Sum(t => t.stackCount);
                        job.count = Mathf.Min(item.Quantity - currentlyHeldAmount, MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing));
                    }
                    job.checkEncumbrance = true;
                    return job;
                }
            }

            // Removed a tag, and have an item lingering around
            foreach (var item in comp.Loadout.itemsToRemove.ToList()) {
                // player might have manually removed thing
                if (!pawn.inventory.Contains(item.thing)) {
                    comp.Loadout.itemsToRemove.Remove(item);
                    continue;
                }

                Log.Message("From itemsToRemove");
                
                var job = JobMaker.MakeJob(InvJobDefOf.CL_UnloadInventory);
                job.SetTarget(TargetIndex.A, item.thing);
                job.count = item.Count;

                comp.Loadout.itemsToRemove.Remove(item);


                return job;
            }

            // check to see if we are holding too many items (holding 75 wood, expecting 50, drop the 25)
            // the actual logic to calc what to drop is in `FirstUnloadableThing_Patch.cs`
            if (comp.ShouldDropSomething()) {
                Log.Message("From `shouldDropSomething`");
                return JobMaker.MakeJob(InvJobDefOf.CL_UnloadInventory);
            }

            nextUpdates[pawn] = Find.TickManager.TicksGame + Rand.Range(10000, 15000);
            
            return null;
        }
    }
}