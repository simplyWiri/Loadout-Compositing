using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Inventory
{
    // add 'Pick up' option to add things to inventory (like pick up and haul);
    [HarmonyPatch]
    public class AddHumanLikeOrders_Patch
    {
        public static bool Prepare()
        {
            return !LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "Mehni.PickUpAndHaul".ToLowerInvariant());
        }

        public static MethodInfo TargetMethod()
        {
            return AccessTools.Method(typeof(FloatMenuMakerMap), "AddHumanlikeOrders");
        }
        
        public static void Postfix(Vector3 clickPos, Pawn pawn, List<FloatMenuOption> opts)
        {
            if (pawn.IsQuestLodger())
                return;

            var position = IntVec3.FromVector3(clickPos);
            var items = position.GetThingList(pawn.Map);

            foreach (Thing item in items)
            {
                if (item.def.category != ThingCategory.Item) continue;
                
                var count = MassUtility.CountToPickUpUntilOverEncumbered(pawn, item);
                if (count == 0) continue;
                
                count = Math.Min(count, item.stackCount);

                var displayText = Strings.PickUpItems(item.LabelNoCount, count.ToString());
                var option = FloatMenuUtility.DecoratePrioritizedTask(
                    new FloatMenuOption(
                        displayText, () =>
                        {
                            var job = JobMaker.MakeJob(JobDefOf.TakeInventory, item);
                            job.count = count;
                            job.checkEncumbrance = true;
                            pawn.jobs.TryTakeOrderedJob(job);
                        }), 
                        pawn,item);
                opts.Add(option);
            }
        }
    }
}