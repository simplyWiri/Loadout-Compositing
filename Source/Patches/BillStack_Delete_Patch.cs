using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory {

    // deleting the surgery for a tagged prosthetic before it is done declines it, so it isn't queued again until the
    // pawn's tags change. A surgery which is done (even if it failed) deletes itself after completing.
    [HarmonyPatch(typeof(BillStack), nameof(BillStack.Delete))]
    public static class BillStack_Delete_Patch {

        public static void Prefix(BillStack __instance, Bill bill) {
            if (bill is not Bill_Medical medical || medical.iterationCompleted) return;
            if (__instance.billGiver is not Pawn pawn || !pawn.IsValidLoadoutHolder()) return;

            var def = ProstheticUtility.ProstheticInstalledBy(medical.recipe);
            if (def == null) return;

            pawn.TryGetComp<LoadoutComponent>()?.Loadout.Decline(def);
        }

    }

}
