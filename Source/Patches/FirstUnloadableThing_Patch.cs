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
        private static MethodInfo shouldDrop = AccessTools.Method(typeof(FirstUnloadableThing_Patch), nameof(FirstUnloadableThing_Patch.ShouldDropThing));
        private static MethodInfo inLoadout = AccessTools.Method(typeof(FirstUnloadableThing_Patch), nameof(FirstUnloadableThing_Patch.ThingInLoadout));
        
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
        *       if ( ThingInLoadout(this, j) )
        *           ThingCount tempCount;
        *           if ( ShouldDropThing(this, j, ref tempCount) ) {
        *               return tempCount;
        *           } else {
        *               continue;
        *           }
        *
        *      if (!this.innerContainer[j].def.IsDrug)
        *           ...   
        */

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator iLGenerator)
        {
            var insts = instructions.ToList();
            var localCount = iLGenerator.DeclareLocal(typeof(ThingCount));
            var continueExecutingLabel = iLGenerator.DefineLabel();
            var breakLoopLabel = iLGenerator.DefineLabel();
            
            for (int i = 0; i < insts.Count; i++)
            {
                if (Matches(insts, i)) {
                    for (int j = i; j < insts.Count; j++) {
                        if (MatchesBranchCondition(insts, j)) {
                            insts[j].labels.Add(breakLoopLabel);
                        }
                    }

                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(insts[i]); // this
                    yield return new CodeInstruction(insts[i + 2]); // j
                    yield return new CodeInstruction(OpCodes.Call, inLoadout);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, continueExecutingLabel);
                    
                    // if we are here, the item is from our loadout, and we now want to see if it is here in
                    // excess, and as such should be removed
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // this
                    yield return new CodeInstruction(insts[i + 2]); // j
                    yield return new CodeInstruction(OpCodes.Ldloca, localCount); // ref thingCount
                    yield return new CodeInstruction(OpCodes.Call, shouldDrop);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, breakLoopLabel);
                    yield return new CodeInstruction(OpCodes.Ldloc, localCount); // return ref thingCount
                    yield return new CodeInstruction(OpCodes.Ret);

                    yield return insts[i].WithLabels(continueExecutingLabel);
                }
                else
                {
                    yield return insts[i];
                }

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

        public static bool MatchesBranchCondition(List<CodeInstruction> instructions, int index)
        {
            return index < instructions.Count - 9
                   && instructions[index + 0].opcode == OpCodes.Ldloc_3
                   && instructions[index + 1].opcode == OpCodes.Ldc_I4_1
                   && instructions[index + 2].opcode == OpCodes.Add
                   && instructions[index + 3].opcode == OpCodes.Stloc_3
                   && instructions[index + 4].opcode == OpCodes.Ldloc_3
                   && instructions[index + 5].opcode == OpCodes.Ldarg_0
                   && instructions[index + 6].opcode == OpCodes.Ldfld
                   && instructions[index + 7].opcode == OpCodes.Callvirt
                   && instructions[index + 8].opcode == OpCodes.Blt;
        }

        public static bool ThingInLoadout(Pawn_InventoryTracker inventory, int thingIndex)
        {
            if (!inventory.pawn.IsValidLoadoutHolder()) return false;

            var thing = inventory.innerContainer.innerList[thingIndex];
            var comp = inventory.pawn.GetComp<LoadoutComponent>();

            return comp?.Loadout?.Desires(thing) ?? false;
        }
        
        // precondition: the thing pointed to by `thingIndex` must be in the pawns loadout
        public static bool ShouldDropThing(Pawn_InventoryTracker inventory, int thingIndex, ref ThingCount count)
        {
            var thing = inventory.innerContainer.innerList[thingIndex];
            var comp = inventory.pawn.GetComp<LoadoutComponent>();

            var allOtherEqualThings = inventory.pawn.InventoryAndEquipment().ToList()
                .Where(td => td.def == thing.def)
                .ToList();

            var itemsAcceptingThing = comp.Loadout.ItemsAccepting(thing).ToList();
            var desiredQuantity = itemsAcceptingThing.Sum(item => item.Quantity);
            int currentQuantity = thing.stackCount;

            foreach (var otherThing in allOtherEqualThings) {
                if (itemsAcceptingThing.Any(item => item.Allows(otherThing))) {
                    currentQuantity += otherThing.stackCount;
                }
            }

            if (currentQuantity > desiredQuantity) {
                if (thing.stackCount == 1) {
                    count = new ThingCount(thing, 1);
                }
                else
                {
                    var dropCount = Mathf.Min(currentQuantity - desiredQuantity, thing.stackCount);
                    count = new ThingCount(thing, dropCount);
                }

                return true;
            }
            
            return false;
        }
        
    }
}