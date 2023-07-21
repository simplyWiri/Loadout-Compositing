using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory {

    [HarmonyPatch(typeof(Pawn), "SetFaction")]
    public class Pawn_SetFaction {

        public static void Postfix(Pawn __instance) {
            if (__instance.IsValidLoadoutHolder()) {
                var comp = __instance.TryGetComp<LoadoutComponent>();

                // Remove any tags which may no longer exist, consider the case wherein a tag has been deleted
                // while a colonist was arrested & left the faction. When joining again it will have invalid state
                // because the entry was not properly cleaned up.
                comp.Loadout.elements.RemoveAll(elem => elem.Tag is null);

                foreach (var tag in comp.Loadout.AllTags) {
                    var list = LoadoutManager.PawnsWithTags[tag];
                    if (!list.Pawns.Contains(__instance)) {
                        list.Pawns.Add(__instance);
                    }
                }

                if (comp.Loadout.elements.Count == 0) {
                    foreach (var tag in LoadoutManager.Tags) {
                        if (tag.defaultEnabled) {
                            comp.AddTag(tag);
                        }
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