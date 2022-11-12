using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    [HarmonyPatch(typeof(PlaySettings), "DoPlaySettingsGlobalControls")]
    public static class PlaySettings_DoPlaySettingsGlobalControls_Patch
    {
        public static bool Prepare()
        {
            var bpcLoaded = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "voult.betterpawncontrol".ToLowerInvariant());
            if (bpcLoaded) {
                Log.Message("[Loadout Compositing] Enabled mod integrations with Better Pawn Control, disabling duplicate functionality.");
            }
            return !bpcLoaded;
        }

        // Adds the panic state button to the global controls panel in the bottom left of the game, this functionality
        // is disabled when better pawn control is loaded, as the bpc emergency functionality is instead used.
        public static void Postfix(WidgetRow row, bool worldView)
        {
            if (worldView || row == null) {
                return;
            }

            var activePanicState = LoadoutManager.ActivePanicState();
            var msg = activePanicState ? Strings.DeactivePanicState : Strings.ActivatePanicState(LoadoutManager.PanicState);
            var col = activePanicState ? Color.red : Color.white;

            GUI.color = col;
            if (row.ColoredButtonIcon(Textures.PanicButtonTex, msg, col)) {
                LoadoutManager.TogglePanicMode();
            }
            GUI.color = Color.white;
        }
    }
}


