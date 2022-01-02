using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class RPG_Inventory_Patch {

        private static MethodInfo targetMethod = AccessTools.Method(typeof(Panel_GearTab), nameof(Panel_GearTab.DrawTags));

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var insts = instructions.ToList();
            var insertMeth = AccessTools.Method("Sandy_Detailed_RPG_Inventory.Sandy_Detailed_RPG_GearTab:DrawInventory");

            int count = 0;
            for (int i = 0; i < insts.Count; i++) {
                yield return insts[i];
                if (insts[i].Calls(insertMeth) && ++count > 1 ) {
                    yield return insts[i - 8]; // ldarg_0
                    yield return insts[i - 7]; // .get_SelPawnForGear
                    yield return insts[i - 2]; // ref num
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 3); // viewRect
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Rect), nameof(Rect.width)));
                    yield return new CodeInstruction(OpCodes.Call, targetMethod);
                }
            }
        }

    }

}