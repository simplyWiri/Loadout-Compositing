using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory
{
    // bias the apparel score gain significantly in favour of apparel in the pawns loadout
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreRaw))]
    public static class OptimizeApparel_ApparelScoreGain_Patch
    {
        public static void Postfix(Pawn pawn, Apparel ap, ref float __result)
        {
            var comp = pawn.GetComp<LoadoutComponent>();
            if (comp?.Loadout.Desires(ap) ?? false) {
                __result += 0.24f;
                __result *= 10;
            }
        }
    }
}