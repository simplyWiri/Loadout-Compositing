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
            if (!pawn.IsColonist || pawn.IsQuestLodger() || pawn.apparel.AnyApparelLocked ) 
                return null;

            if (!nextUpdates.TryGetValue(pawn, out var nextTick)) {
                nextTick = Find.TickManager.TicksGame + Rand.Range(10000, 15000);
                nextUpdates.Add(pawn, nextTick);
            }
            
            if (Find.TickManager.TicksGame < nextTick)
                return null;

            var items = pawn.inventory.innerContainer.InnerListForReading.ConcatIfNotNull(pawn.equipment.AllEquipmentListForReading).ToList();
            var requiredItems = pawn.GetComp<LoadoutComponent>().Loadout.DesiredItems(items);

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
            
            nextUpdates[pawn] = Find.TickManager.TicksGame + Rand.Range(10000, 15000);
            
            return null;
        }
    }
}