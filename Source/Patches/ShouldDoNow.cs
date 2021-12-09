using System.Net;
using HarmonyLib;
using RimWorld;

namespace Inventory
{
    // drives whether a bill should be run or not at a given point in time
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.ShouldDoNow))]
    public static class ShouldDoNow_Patch
    {
        public static bool Prefix(ref bool __result, Bill_Production __instance)
        {
            if (__instance.repeatMode != BillRepeatModeDefOf.W_PerTag) return true;

            if (__instance.suspended || __instance.Satisfied()) {
                __result = false;
                return false;
            };

            __result = !__instance.paused;
            return false;
        }

    }
}