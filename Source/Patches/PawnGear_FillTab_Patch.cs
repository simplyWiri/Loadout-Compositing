using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // Add a new section which details the tags being applied to a pawn
    [HarmonyPatch(typeof(ITab_Pawn_Gear), nameof(ITab_Pawn_Gear.FillTab))]
    class PawnGear_FillTab_Patch {

        private static MethodInfo showOverallArmour = AccessTools.Method(typeof(ITab_Pawn_Gear), nameof(ITab_Pawn_Gear.TryDrawOverallArmor));
        private static MethodInfo getPawn = AccessTools.PropertyGetter(typeof(ITab_Pawn_Gear), nameof(ITab_Pawn_Gear.SelPawnForGear));
        private static MethodInfo rectWidth = AccessTools.PropertyGetter(typeof(Rect), nameof(Rect.width));
        private static MethodInfo drawTags = AccessTools.Method(typeof(Panel_GearTab), nameof(Panel_GearTab.DrawTags));

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions) {
            var matches = 0;
            var insts = instructions.ToList();
            for (int i = 0; i < insts.Count; i++) {
                if (Matches(insts, i)) {
                    matches++;
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(insts[i]);
                    yield return new CodeInstruction(OpCodes.Call, getPawn);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, insts[i - 8].operand);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, insts[i - 7].operand);
                    yield return new CodeInstruction(OpCodes.Call, rectWidth);
                    yield return new CodeInstruction(OpCodes.Call, drawTags);
                }

                yield return insts[i];
            }

            if (matches != 1) {
                Utility.TranspilerError(__originalMethod, "Tag view to the pawn 'Gear' tab");
            }
        }
        

        private static bool Matches(List<CodeInstruction> insts, int i) {
            return i >= 1 && i <= insts.Count - 1
                          && insts[i - 1].opcode == OpCodes.Call
                          && insts[i - 1].operand is MethodInfo method && method == showOverallArmour
                          && insts[i].opcode == OpCodes.Ldarg_0
                          && insts[i + 1].opcode == OpCodes.Ldarg_0;
        }

    }

}