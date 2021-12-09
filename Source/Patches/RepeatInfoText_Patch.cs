using System.Reflection;
using HarmonyLib;
using RimWorld;

namespace Inventory
{
    // fills a description string which displays a string with the current progress of the bill
    [HarmonyPatch]
    public class RepeatInfoText_Patch
    {
        public static MethodInfo TargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(Bill_Production), nameof(Bill_Production.RepeatInfoText));
        }
        
        public static bool Prefix(ref string __result, Bill_Production __instance)
        {
            if (__instance.repeatMode != InvBillRepeatModeDefOf.W_PerTag) return true;
            
            __result = $"{__instance.recipe.WorkerCounter.CountProducts(__instance)}/{__instance.DesiredTargetCount()} ({__instance.targetCount})";
            return false;
        }
    }
}