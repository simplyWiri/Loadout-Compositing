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

    // when a colonist portrait is clicked while LoadoutEditor window is open, change currently selected pawn.
    [HarmonyPatch]
    public class ColonistBarClicker_Patch {

        static IEnumerable<MethodBase> TargetMethods() {
            if (ModLister.HasActiveModWithName("Owl's Colonist Bar (dev)")) {
                Log.Message("[Loadout Compositing] Enabled mod integrations with Owl's Colonist Bar (dev).");
                yield return AccessTools.Method("OwlBar.OwlColonistBar:HandleClicks");
            }
            
            yield return AccessTools.Method(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.HandleClicks));
        }

#if VERSION_1_3
        private static MethodInfo target = AccessTools.Method(typeof(CameraJumper), nameof(CameraJumper.TryJump), new Type[] { typeof(GlobalTargetInfo) });
#elif VERSION_1_4
        private static MethodInfo target = AccessTools.Method(typeof(CameraJumper), nameof(CameraJumper.TryJump), new Type[] { typeof(GlobalTargetInfo), typeof(CameraJumper.MovementMode) });
#endif
        private static MethodInfo insertMethod = AccessTools.Method(typeof(ColonistBarClicker_Patch), "ChangeWindowTo");
        
        // CameraJumper.TryJump(pawn)
        // + ChangeWindowTo(pawn) <-- gets added
        public static IEnumerable<CodeInstruction> Transpiler(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions)
        {
            var matches = 0;
            var insts = instructions.ToList();

            for (int i = 0; i < insts.Count; i++) {
                var inst = insts[i];

                yield return inst;

                if (inst.Calls(target)) {
                    matches++;
                    var param = GetParamForMethod(__originalMethod);
                    var index = 1 + param.Position;

                    yield return new CodeInstruction(OpCodes.Ldarg, index);
                    yield return new CodeInstruction(OpCodes.Call, insertMethod);
                }
            }
            
            if (matches != 1) {
                Log.ErrorOnce($"[Loadout Compositing] {matches} Failed to colonist clicker transpiler, will not be able to click on colonists in the colonist bar & update the gui", 946382);
            }
        }

        private static ParameterInfo GetParamForMethod(MethodBase method) {
            var parameters = method.GetParameters();

            return parameters.First(p => p.ParameterType == typeof(Pawn));
        }

        private static void ChangeWindowTo(Pawn pawn) {
            var old = Find.WindowStack.WindowOfType<Dialog_LoadoutEditor>();
            
            if (old != null) {
                Find.WindowStack.TryRemove(old, false);
                Find.WindowStack.Add(new Dialog_LoadoutEditor(pawn, old));
            }
        }

    }

}