// using HarmonyLib;
// using RimWorld;
//
// namespace Inventory
// {
//     [HarmonyPatch(typeof(Bill_Production), "RepeatInfoText")]
//     public class RepeatInfoText
//     {
//         public static bool Prefix(ref string __result, Bill_Production __instance)
//         {
//             if (__instance.repeatMode != BillRepeatModeDefOf.W_PerTag) return true;
//             
//             __result = $"{LoadoutManager.ColonistCountFor(__instance)} / dd ff";
//             return false;
//         }
//     }
// }