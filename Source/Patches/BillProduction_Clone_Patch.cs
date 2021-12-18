using HarmonyLib;
using RimWorld;

namespace Inventory {

    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.Clone))]
    public class BillProduction_Clone_Patch {

        public static void Postfix(ref Bill __result, Bill_Production __instance) {
            var tag = LoadoutManager.TagFor(__instance);
            LoadoutManager.SetTagForBill(__result as Bill_Production, tag);
        }

    }

}