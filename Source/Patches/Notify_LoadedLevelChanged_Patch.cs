using System.Linq;
using HarmonyLib;
using Verse;

namespace Inventory {

    // triggers code to run after defs have been loaded.
    [HarmonyPatch(typeof(Messages), nameof(Messages.Notify_LoadedLevelChanged))]
    public class Notify_LoadedLevelChanged_Patch {

        private static bool patchedDefs = false;

        private static void Postfix() {
            if (patchedDefs) return;

            foreach (var thing in DefDatabase<ThingDef>.AllDefs.Where(t => t.race?.Humanlike == true)) {
                if (thing.comps.Any(c => c.compClass == typeof(LoadoutComponent))) continue;

                thing.comps.Add(new CompProperties(typeof(LoadoutComponent)));
            }

            Utility.CalculateDefLists();

            patchedDefs = true;

            LateModPatches();
        }

        private static void LateModPatches() {

            if (ModLister.HasActiveModWithName("RPG Style Inventory Revamped")) {
                Log.Message("[Loadout Compositing] Enabled mod integrations with RPG Style Inventory Revamped, inserting tag menu into pawn ITab.");

                var method = AccessTools.Method("Sandy_Detailed_RPG_GearTab:FillTab");
                if (method != null) {
                    var hp = new HarmonyMethod(typeof(RPG_Inventory_Patch), nameof(RPG_Inventory_Patch.Transpiler));
                    ModBase.harmony.Patch(method, transpiler: hp);
                }
            }

            if (ModLister.HasActiveModWithName("[LTO] Colony Groups")) {
                Log.Message("[Loadout Compositing] Enabled mod integrations with [LTO] Colony Groups, enabling colonist bar hooks.");

                var handleClickMethod = AccessTools.Method("TacticalGroups.ColonistBarColonistDrawer:HandleClicks");
                if (handleClickMethod != null) {
                    var hp = new HarmonyMethod(typeof(ColonistBarClicker_Patch), nameof(ColonistBarClicker_Patch.Transpiler));
                    ModBase.harmony.Patch(handleClickMethod, transpiler: hp);
                }

                var otherHandleClickMethod = AccessTools.Method("TacticalGroups.ManagementMenu:HandleClicks");
                if (otherHandleClickMethod != null) {
                    var hp = new HarmonyMethod(typeof(ColonistBarClicker_Patch), nameof(ColonistBarClicker_Patch.Transpiler));
                    ModBase.harmony.Patch(otherHandleClickMethod, transpiler: hp);
                }
            }
        }

    }

}