using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // bias the apparel score gain significantly in favour of apparel in the pawns loadout
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.ApparelScoreRaw))]
    public static class OptimizeApparel_ApparelScoreGain_Patch {

        public static void Postfix(Pawn pawn, Apparel ap, ref float __result) {
            var comp = pawn.GetComp<LoadoutComponent>();
            var multiplier = comp?.Loadout.WeightAtWhichLoadoutDesires(ap) ?? 0;
            if (multiplier != 0) {
                if (ModBase.settings.biasLoadBearingItems && Utility.massBoostingClothes.TryGetValue(ap.def, out var func)) {
                    var hasQuality = ap.TryGetQuality(out var quality);
                    var score = func(hasQuality ? quality : QualityCategory.Normal);
                    __result += Mathf.Lerp(0.16f, 0.24f, score / 135f);
                }
                __result += 0.24f; // flat bonus for being an apparel in the loadout
                __result *= multiplier;
            } else if ( ModBase.settings.onlyItemsFromLoadout ) {
                __result = -1000f;
            }
        }

    }

}