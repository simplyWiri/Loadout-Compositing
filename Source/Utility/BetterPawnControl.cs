using Verse;

namespace Inventory {

    // Support for Better Pawn Control mod to get/set an active loadout for a pawn.
    public static class BetterPawnControl {

        public static int GetLoadoutId(Pawn pawn) {
            return pawn.GetActiveState()?.id ?? -1;
        }

        public static void SetLoadoutById(Pawn pawn, int id) {
            foreach( LoadoutState state in LoadoutManager.States ) {
                if( state.id == id) {
                    pawn.SetActiveState(state, immediatelyResolve: BetterPawnControl_EmergencyToggle_Patch.emergencyActive);
                    return;
                }
            }
            pawn.SetActiveState(null, immediatelyResolve: BetterPawnControl_EmergencyToggle_Patch.emergencyActive);
        }
    }
}
