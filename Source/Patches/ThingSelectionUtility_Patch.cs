using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Inventory
{
    [HarmonyPatch(typeof(ThingSelectionUtility))]
    public static class ThingSelectionUtility_Patch
    {
        public static IEnumerable<MethodInfo> TargetMethods() {
            yield return AccessTools.Method(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.SelectNextColonist));
            yield return AccessTools.Method(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.SelectPreviousColonist));
        }

        public static void Postfix() {
            var targetWindow = Find.WindowStack.WindowOfType<Dialog_LoadoutEditor>();
            if (targetWindow == null) {
                return;
            }

            var targetPawn = Find.Selector.SelectedPawns.FirstOrDefault(p => p.IsValidLoadoutHolder());
            if ( targetPawn is null ) {
                return;
            }

            if ( targetPawn == targetWindow.pawn ) {
                return;
            }


            // We need to close any windows which may be referencing any (about to be) invalid state.
            Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_TagEditor));
            Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_SetTagLoadoutState));
            Find.WindowStack.RemoveWindowsOfType(typeof(Dialog_TagSelector));

            targetWindow.Close();
            Find.WindowStack.Add(new Dialog_LoadoutEditor(targetPawn, targetWindow));
        }
    }
}
