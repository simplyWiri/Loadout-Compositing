using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory
{
    [HarmonyPatch]
    public class OptionsMaker_OtherOptionsMaker_Patch
    {
        private static List<PawnColumnDef> defs = null;
        private static MethodInfo floatMenuOptionsFor = null;
        
        public static bool Prepare() {
            if (!LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "Mehni.Numbers".ToLowerInvariant()))
            {
                return false;
            }
            
            floatMenuOptionsFor = AccessTools.Method("Numbers.OptionsMaker:FloatMenuOptionsFor", new Type[] { typeof(IEnumerable<PawnColumnDef>) });

            if (floatMenuOptionsFor == null)
            {
                Log.ErrorOnce("[CL] Failed to apply 'Numbers' compatibility patch, Loadout overview column will not show up", 5672382);
            }
            else
            {
                Log.Message("[CL] Applied 'Numbers' compatibility patch, Loadout overview column will show up");           
            }
            
            return true;
        }

        public static MethodInfo TargetMethod() {
            return AccessTools.Method("Numbers.OptionsMaker:OtherOptionsMaker");
        }

        public static void Postfix(object __instance, ref List<FloatMenuOption> __result) {
            defs ??= new List<PawnColumnDef> { DefDatabase<PawnColumnDef>.GetNamed("CL_ViewLoadoutState") };

            var list = floatMenuOptionsFor.Invoke(__instance, new object[] { defs }) as List<FloatMenuOption>;
            __result.AddRange(list);
        }
    }
}