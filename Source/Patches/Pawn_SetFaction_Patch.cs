using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory {

    [HarmonyPatch(typeof(Pawn), "SetFaction")]
    public class Pawn_SetFaction {

        public static void Postfix(Pawn __instance) {
            if (__instance.IsValidLoadoutHolder()) {
                var comp = __instance.TryGetComp<LoadoutComponent>();

                if (comp.Loadout.elements.Count == 0) {
                    foreach (var tag in LoadoutManager.Tags) {
                        if (tag.defaultEnabled) {
                            var state = LoadoutManager.States.FirstOrDefault(s => s.name == tag.defaultState);
                            comp.AddTag(tag, state, tag.defaultCondition, false);
                        }
                    }
                }
            }
        }

    }

}