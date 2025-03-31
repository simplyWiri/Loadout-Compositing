using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory
{
    // Manually patched because BPC unfortunately has references to Textures inside its patch methods, so when this patch gets called, it triggers the method
    // to the JIT'd which then triggers the static class which holds the references to be initialize, which occurs on a separate thread, leading to unity errors.
    // We work around this by patching this in the GameComponent instead of the mod constructor.
    static class BetterPawnControl_EmergencyToggle_Patch
    {
        private static bool patched = false;
        public static bool emergencyActive = false;

        public static void TryPatch(Harmony harmonyInstance)
        {
            var bpcLoaded = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLowerInvariant() == "VouLT.BetterPawnControl".ToLowerInvariant());
            if ( bpcLoaded && !patched ) {
                if( AccessTools.PropertyGetter("BetterPawnControl.Widget_ModsAvailable:CompositableAvailable") != null ) {
                    // New BPC switches loadouts as part of emergency mode switching,
                    // so only set flag to force immediate resolving before BPC switches the loadouts.
                    var method = AccessTools.Method("BetterPawnControl.Patches.PlaySettings_DoPlaySettingsGlobalControls:EmergencyToogleON");
                    harmonyInstance.Patch(method, prefix: new HarmonyMethod(typeof(BetterPawnControl_EmergencyToggle_Patch), nameof(ToggleOnNew)));

                    method = AccessTools.Method("BetterPawnControl.Patches.PlaySettings_DoPlaySettingsGlobalControls:EmergencyToogleOFF");
                    harmonyInstance.Patch(method, prefix: new HarmonyMethod(typeof(BetterPawnControl_EmergencyToggle_Patch), nameof(ToggleOffNew)));

                    Log.Message("[Loadout Compositing] Enabled mod integrations with Better Pawn Control (new mode)");
                } else {
                    var method = AccessTools.Method("BetterPawnControl.Patches.PlaySettings_DoPlaySettingsGlobalControls:EmergencyToogleON");
                    harmonyInstance.Patch(method, postfix: new HarmonyMethod(typeof(BetterPawnControl_EmergencyToggle_Patch), nameof(ToggleOnOld)));

                    method = AccessTools.Method("BetterPawnControl.Patches.PlaySettings_DoPlaySettingsGlobalControls:EmergencyToogleOFF");
                    harmonyInstance.Patch(method, postfix: new HarmonyMethod(typeof(BetterPawnControl_EmergencyToggle_Patch), nameof(ToggleOffOld)));

                    Log.Message("[Loadout Compositing] Enabled mod integrations with Better Pawn Control");
                }
                patched = true;
            }
        }

        public static void ToggleOnNew() {
            emergencyActive = true;
        }

        public static void ToggleOffNew() {
            emergencyActive = false;
        }

        public static void ToggleOnOld() {
            LoadoutManager.TogglePanicMode(true);
        }

        public static void ToggleOffOld() {
            LoadoutManager.TogglePanicMode(false);
        }
    }
}
