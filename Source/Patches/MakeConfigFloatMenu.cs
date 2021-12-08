// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Reflection;
// using System.Reflection.Emit;
// using HarmonyLib;
// using RimWorld;
// using Verse;
//
// namespace Inventory
// {
//     [HarmonyPatch(typeof(BillRepeatModeUtility), nameof(BillRepeatModeUtility.MakeConfigFloatMenu))]
//     public class MakeConfigFloatMenu
//     {
//         private static ConstructorInfo ctor = AccessTools.Constructor(typeof(List<>).MakeGenericType(typeof(FloatMenuOption)));
//         private static MethodInfo getOptions = AccessTools.Method(typeof(MakeConfigFloatMenu), nameof(MakeConfigFloatMenu.getOptions));
//
//         public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
//         {
//             var insts = instructions.ToList();
//             for (int i = 0; i < insts.Count; i++) {
//                 if (Matches(insts, i))
//                 {
//                     yield return new CodeInstruction(insts[i]); // ldloc
//                     yield return new CodeInstruction(OpCodes.Ldarg_0); // bill_production
//                     yield return new CodeInstruction(OpCodes.Call, getOptions); 
//                 }
//                 yield return insts[i];
//             }
//         }
//
//         public static bool Matches(List<CodeInstruction> instructions, int i)
//         {
//             return i > 1
//                    && instructions[i - 2].opcode == OpCodes.Newobj
//                    && instructions[i - 2].operand.Equals(ctor)
//                    && instructions[i - 1].IsStloc();
//         }
//
//         public static void GetOptions(List<FloatMenuOption> options, Bill_Production bill)
//         {
//             options.Add(new FloatMenuOption(BillRepeatModeDefOf.W_PerTag.LabelCap, () => {
//                 if (!bill.recipe.WorkerCounter.CanCountProducts(bill))
//                 {
//                     Messages.Message("RecipeCannotHaveTargetCount".Translate(), MessageTypeDefOf.RejectInput, false);
//                     return;
//                 }
//                 bill.repeatMode = BillRepeatModeDefOf.W_PerTag;
//             }));
//         }
//     }
// }