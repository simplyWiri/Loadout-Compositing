using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Inventory {

    // Add functionality to the +/- buttons in the bill menu
    [HarmonyPatch(typeof(Bill_Production), nameof(Bill_Production.DoConfigInterface))]
    public class DoConfigInterface_Patch {

        private static FieldInfo dragSlider = AccessTools.Field(typeof(SoundDefOf), nameof(SoundDefOf.DragSlider));
        private static MethodInfo playOneShotOnCamera = AccessTools.Method(typeof(SoundStarter), nameof(SoundStarter.PlayOneShotOnCamera));

        private static FieldInfo repeatMode = AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.repeatMode));

        private static FieldInfo w_PerTag = AccessTools.Field(typeof(Inventory.InvBillRepeatModeDefOf), nameof(InvBillRepeatModeDefOf.W_PerTag));
        private static MethodInfo plusButton = AccessTools.Method(typeof(DoConfigInterface_Patch), nameof(DoConfigInterface_Patch.PlusButton));
        private static MethodInfo minusButton = AccessTools.Method(typeof(DoConfigInterface_Patch), nameof(DoConfigInterface_Patch.MinusButton));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator) {
            int occurence = 0;
            var insts = instructions.ToList();
            for (int i = 0; i < insts.Count; i++) {
                if (Matches(insts, i)) {
                    var label = generator.DefineLabel();

                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(insts[i]); // billProduction
                    yield return new CodeInstruction(OpCodes.Ldfld, repeatMode); // billProduction.repeatMode
                    yield return new CodeInstruction(OpCodes.Ldsfld, w_PerTag); // BillRepeatModeDefOf.W_PerTag
                    yield return new CodeInstruction(OpCodes.Bne_Un, label); // if ( repeatMode != W_PerTag )

                    yield return new CodeInstruction(OpCodes.Ldarg_0); // billProduction
                    yield return new CodeInstruction(OpCodes.Call, occurence == 0 ? plusButton : minusButton);

                    yield return new CodeInstruction(insts[i]).WithLabels(label);
                    occurence++;
                }
                else {
                    yield return insts[i];
                }
            }
        }

        public static void PlusButton(Bill_Production __instance) {
            var delta = __instance.recipe.targetCountAdjustment * GenUI.CurrentAdjustmentMultiplier();
            __instance.targetCount += delta;
        }

        public static void MinusButton(Bill_Production __instance) {
            var delta = __instance.recipe.targetCountAdjustment * GenUI.CurrentAdjustmentMultiplier();
            __instance.targetCount = Mathf.Max(1, __instance.targetCount - delta);
        }

        private static bool Matches(List<CodeInstruction> instructions, int i) {
            return i < instructions.Count - 2
                   && instructions[i + 0].opcode == OpCodes.Ldsfld
                   && instructions[i + 0].operand.Equals(dragSlider)
                   && instructions[i + 1].opcode == OpCodes.Ldnull
                   && instructions[i + 2].opcode == OpCodes.Call
                   && instructions[i + 2].operand.Equals(playOneShotOnCamera);
        }

    }

}