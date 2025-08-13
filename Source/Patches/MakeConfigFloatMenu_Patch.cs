using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory {

    // Adds the 'W_PerTag' def option to float menu dropdowns.
    [HarmonyPatch(typeof(BillRepeatModeUtility), nameof(BillRepeatModeUtility.MakeConfigFloatMenu))]
    public class MakeConfigFloatMenu_Patch {

        private static FieldInfo repeatCount = AccessTools.Field(typeof(BillRepeatModeDefOf), nameof(BillRepeatModeDefOf.RepeatCount));
        private static MethodInfo getOptions = AccessTools.Method(typeof(MakeConfigFloatMenu_Patch), nameof(MakeConfigFloatMenu_Patch.GetOptions));

        
        public static IEnumerable<CodeInstruction> Transpiler(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions) {
            var matches = 0;
            var insts = instructions.ToList();

            var locs = __originalMethod.GetMethodBody()?.LocalVariables ?? new List<LocalVariableInfo>();
            var targetLoc = locs.First(loc => loc.LocalType == typeof(List<>).MakeGenericType(typeof(FloatMenuOption)));
            
            for (int i = 0; i < insts.Count; i++) {
                if (Matches(insts, i)) {
                    matches++;
                    yield return new CodeInstruction(OpCodes.Ldloc, targetLoc.LocalIndex); // ldloc
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // bill_production
                    yield return new CodeInstruction(OpCodes.Call, getOptions);
                }

                yield return insts[i];
            }

            if (matches != 1) {
                Utility.TranspilerError(__originalMethod, "'X per tag' option to float menu map options");
            }
        }

        public static bool Matches(List<CodeInstruction> instructions, int i) {
            return instructions[i].LoadsField(repeatCount);
        }

        public static void GetOptions(List<FloatMenuOption> options, Bill_Production bill) {
            options.Add(new FloatMenuOption(InvBillRepeatModeDefOf.W_PerTag.LabelCap, () => {
                if (!bill.recipe.WorkerCounter.CanCountProducts(bill)) {
                    Messages.Message("RecipeCannotHaveTargetCount".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // some qol for common use cases
                bill.repeatMode = InvBillRepeatModeDefOf.W_PerTag;
                bill.targetCount = 1;
                bill.repeatCount = 0;
                bill.includeEquipped = true;
            }));
        }

    }

}