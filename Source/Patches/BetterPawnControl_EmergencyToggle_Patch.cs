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

        public static void TryPatch(Harmony harmonyInstance)
        {
            var bpcLoaded = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLowerInvariant() == "VouLT.BetterPawnControl".ToLowerInvariant());
            if ( bpcLoaded && !patched ) {
                var method = AccessTools.Method("BetterPawnControl.Patches.PlaySettings_DoPlaySettingsGlobalControls:EmergencyToogleON");
                harmonyInstance.Patch(method, postfix: new HarmonyMethod(typeof(BetterPawnControl_EmergencyToggle_Patch), nameof(ToggleOn)));
                
                method = AccessTools.Method("BetterPawnControl.Patches.PlaySettings_DoPlaySettingsGlobalControls:EmergencyToogleOFF");
                harmonyInstance.Patch(method, postfix: new HarmonyMethod(typeof(BetterPawnControl_EmergencyToggle_Patch), nameof(ToggleOff)));
                patched = true;

                Log.Message("[Loadout Compositing] Enabled mod integrations with Better Pawn Control");
            }
        }

        public static void ToggleOn() {
            LoadoutManager.TogglePanicMode(true);
        }

        public static void ToggleOff() {
            LoadoutManager.TogglePanicMode(false);
        }
    }
}
