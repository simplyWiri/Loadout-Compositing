using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory {

    [HarmonyPatch(typeof(Pawn), "SetFaction")]
    public class Pawn_SetFaction {

        public static void Postfix(Pawn __instance) {
            if (__instance.IsValidLoadoutHolder()) {
                foreach (var tag in __instance.TryGetComp<LoadoutComponent>().Loadout.AllTags) {
                    var list = LoadoutManager.PawnsWithTags[tag];
                    if (!list.Pawns.Contains(__instance)) {
                        list.Pawns.Add(__instance);
                    }
                }
            } else {
                var comp = __instance.TryGetComp<LoadoutComponent>();
                if (comp == null) return;
                var tags = comp.Loadout.AllTags;
                
                foreach (var tag in tags) {
                    var list = LoadoutManager.PawnsWithTags[tag];
                    if (list.Pawns.Contains(__instance)) {
                        list.Pawns.Remove(__instance);
                    }
                }
            }
        }

    }

}