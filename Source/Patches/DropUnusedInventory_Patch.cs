using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Inventory {

    // don't drop items that are included in a pawns tags
    [HarmonyPatch(typeof(JobGiver_DropUnusedInventory), nameof(JobGiver_DropUnusedInventory.Drop))]
    public static class DropUnusedInventory_Patch {

        public static bool Prefix(Pawn pawn, Thing thing) {
            if (!pawn.IsValidLoadoutHolder()) {
                return true;
            }
            
            return !(pawn.TryGetComp<LoadoutComponent>()?.Loadout.Desires(thing) ?? false);
        }

    }

}