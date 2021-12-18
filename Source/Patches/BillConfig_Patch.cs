using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // Add bill options under 'X per Tag' in the bill config
    [HarmonyPatch(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.DoWindowContents))]
    public static class BillConfig_Patch {

        private static FieldInfo bill = AccessTools.Field(typeof(Dialog_BillConfig), nameof(Dialog_BillConfig.bill));
        private static MethodInfo drawConfig = AccessTools.Method(typeof(Panel_BillConfig), nameof(Panel_BillConfig.Draw));
        private static FieldInfo repeatMode = AccessTools.Field(typeof(Bill_Production), nameof(Bill_Production.repeatMode));
        private static FieldInfo w_PerTag = AccessTools.Field(typeof(Inventory.InvBillRepeatModeDefOf), nameof(InvBillRepeatModeDefOf.W_PerTag));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
            ILGenerator generator) {
            var insts = instructions.ToList();

            for (int i = 0; i < insts.Count; i++) {
                if (Matches(insts, i)) {
                    var label = generator.DefineLabel();
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(insts[i]);
                    yield return new CodeInstruction(OpCodes.Ldfld, bill);
                    yield return new CodeInstruction(OpCodes.Ldfld, repeatMode);
                    yield return new CodeInstruction(OpCodes.Ldsfld, w_PerTag);
                    yield return new CodeInstruction(OpCodes.Bne_Un, label);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return insts[i + 1];
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, bill);
                    yield return new CodeInstruction(OpCodes.Call, drawConfig);

                    yield return insts[i].WithLabels(label);
                }
                else {
                    yield return insts[i];
                }
            }
        }
        
        public static bool Matches(List<CodeInstruction> insts, int i) {
            return i > 3 && i < insts.Count - 3
                         && insts[i - 4].opcode == OpCodes.Ldfld
                         && insts[i - 3].opcode == OpCodes.Ldfld
                         && insts[i - 2].opcode == OpCodes.Call
                         && insts[i - 1].opcode == OpCodes.Stfld
                         && insts[i + 0].opcode == OpCodes.Ldloc_S
                         && insts[i + 1].opcode == OpCodes.Ldloc_S
                         && insts[i + 2].opcode == OpCodes.Callvirt
                         && insts[i + 3].opcode == OpCodes.Ldloc_S;
        }

    }

}