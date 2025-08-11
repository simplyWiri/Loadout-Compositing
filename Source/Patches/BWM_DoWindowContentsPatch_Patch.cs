using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Inventory {

    // Allow the bill type added by CL to integrate w/ BWM to include pawns which are off map. 
    public class BWM_DoWindowContentsPatch_Patch {
        private static MethodInfo insertMethod = AccessTools.Method(typeof(BWM_DoWindowContentsPatch_Patch), nameof(ShouldCountAway));
        private static bool patched = false;

        public static void TryPatch(Harmony harmonyInstance)
        {
            var bpcLoaded = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLowerInvariant() == "falconne.bwm".ToLowerInvariant());
            var ebgoLoaded = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLowerInvariant() == "uuugggg.everybodygetsone".ToLowerInvariant())
                || LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLowerInvariant() == "memegoddess.everybodygetsone".ToLowerInvariant());

            if ( bpcLoaded && !ebgoLoaded && !patched ) {
                var method = AccessTools.Method("ImprovedWorkbenches.BillConfig_DoWindowContents_Patch:DrawFilters");
                harmonyInstance.Patch(method, transpiler: new HarmonyMethod(typeof(BWM_DoWindowContentsPatch_Patch), nameof(Transpiler)));
                patched = true;

                Log.Message("[Loadout Compositing] Enabled mod integrations with Better Workbench Management");
            }

            if (ebgoLoaded && !patched)
            {
                var method = AccessTools.Method("Everybody_Gets_One.OrPersonCount_Transpiler:IsAnyTargetMode");
                harmonyInstance.Patch(method, postfix: new HarmonyMethod(typeof(BWM_DoWindowContentsPatch_Patch), nameof(Postfix)));
                
                Log.Message("[Loadout Compositing] Enabled mod integrations with Everybody Gets One");
                
                patched = true;
            }
        }

        public static void Postfix(BillRepeatModeDef repeatMode, ref bool __result)
        {
            if (repeatMode == InvBillRepeatModeDefOf.W_PerTag)
            {
                __result = true;
            }
        }
        
        // if (ExtendedBillDataStorage.CanOutputBeFiltered(billProduction) && billProduction.repeatMode == BillRepeatModeDefOf.TargetCount)
        // Gets changed to
        // if (ExtendedBillDataStorage.CanOutputBeFiltered(billProduction) && ShouldCountAway(billProduction))
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var matches = 0;
            var insts = instructions.ToList();

            for (int i = 0; i < insts.Count; i++) {
                var inst = insts[i];

                yield return inst;

                if (Matches(insts, i)) {
                    ++matches;

                    yield return new CodeInstruction(OpCodes.Call, insertMethod);

                    i += 3;
                }
            }
            
            if (matches != 1) {
                Log.ErrorOnce($"[Loadout Compositing] {matches} Failed to apply Better Workbench Management's workbench transpiler, " +
                              $"will not be able to count pawns that are off map", 946382);
            }
        }

        public static bool Matches(List<CodeInstruction> instructions, int i)
        {
            return i <= instructions.Count - 5
                   && instructions[i + 0].opcode == OpCodes.Ldloc_0
                   && instructions[i + 1].opcode == OpCodes.Ldfld
                   && instructions[i + 2].opcode == OpCodes.Ldsfld
                   && instructions[i + 3].opcode == OpCodes.Ceq;
        }

        public static bool ShouldCountAway(Bill_Production billProduction)
        {
            return (billProduction.repeatMode == BillRepeatModeDefOf.TargetCount ||
                    billProduction.repeatMode == InvBillRepeatModeDefOf.W_PerTag);
        }
    }

}