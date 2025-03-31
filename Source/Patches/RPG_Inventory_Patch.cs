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
            int matches = 0;
            var insts = instructions.ToList();
            for (int i = 0; i < insts.Count; i++) {
                if ( insts[i].Calls(AccessTools.Method(typeof(Widgets), nameof(Widgets.EndScrollView))) )  {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(insts[i]); // this
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field("Sandy_Detailed_RPG_GearTab:cachedPawn"));
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 3); // ref num)
                    yield return new CodeInstruction(OpCodes.Ldloca_S, matches == 0 ? 11 : 8); // viewRect
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Rect), nameof(Rect.width)));
                    yield return new CodeInstruction(OpCodes.Call, targetMethod);

                    ++matches;
                }
                yield return insts[i];
            }
        }

    }

}