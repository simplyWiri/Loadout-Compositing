using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    [HarmonyPatch]
    public static class FirstUnloadableThing_Patch
    {
        private static MethodInfo InLoadout = AccessTools.Method(typeof(FirstUnloadableThing_Patch), nameof(FirstUnloadableThing_Patch.ThingInLoadout));
        public static MethodInfo TargetMethod()
        {
            return typeof(Pawn_InventoryTracker).GetMethod("get_FirstUnloadableThing");
        }
        
        /*
 <Before>
        * for (int j = 0; j < this.innerContainer.Count; j++)
        * {
        *       if (!this.innerContainer[j].def.IsDrug)
        *           ...   
        *
 <After>
        * for (int j = 0; j < this.innerContainer.Count; j++)
        * {
        *       if (ThingInLoadout(this, j)
        *            continue;
        *
        *      if (!this.innerContainer[j].def.IsDrug)
        *           ...   
        */
        
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
        {
            var insts = instructions.ToList();
            for (int i = 0; i < insts.Count; i++)
            {
                if (Matches(insts, i)) {
                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(insts[i]); // this
                    yield return new CodeInstruction(insts[i + 2]); // j
                    yield return new CodeInstruction(OpCodes.Call, InLoadout);
                    yield return new CodeInstruction(OpCodes.Brtrue, insts[i + 6].operand);
                }

                yield return insts[i];
            }
        }

        public static bool Matches(List<CodeInstruction> instructions, int index)
        {
            return index < instructions.Count - 7
                   && instructions[index + 0].opcode == OpCodes.Ldarg_0
                   && instructions[index + 1].opcode == OpCodes.Ldfld
                   && instructions[index + 2].opcode == OpCodes.Ldloc_3
                   && instructions[index + 3].opcode == OpCodes.Callvirt
                   && instructions[index + 4].opcode == OpCodes.Ldfld
                   && instructions[index + 5].opcode == OpCodes.Callvirt
                   && instructions[index + 6].opcode == OpCodes.Brtrue_S;
        }

        public static bool ThingInLoadout(Pawn_InventoryTracker inventory, int thingIndex)
        {
            if (!inventory.pawn.IsColonist || inventory.pawn.IsQuestLodger()) return false;

            var comp = inventory.pawn.GetComp<LoadoutComponent>();
            
            return comp?.Loadout?.Desires(inventory.innerContainer.innerList[thingIndex]) ?? false;
        }
        
    }
}