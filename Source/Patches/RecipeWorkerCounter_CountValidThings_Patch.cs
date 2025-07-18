using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory {

    [HarmonyPatch(typeof(RecipeWorkerCounter), nameof(RecipeWorkerCounter.CountValidThings), typeof(List<Thing>), typeof(Bill_Production), typeof(ThingDef))]
    public class RecipeWorkerCounter_CountValidThings_Patch {
    
        public static bool Prefix(ref int __result, RecipeWorkerCounter __instance, List<Thing> things, Bill_Production bill, ThingDef def) {
            __result = things.Where(t => __instance.CountValidThing(t, bill, def)).Sum(t => Math.Max(1, t.stackCount));
            return false;
        }
    
    }

}