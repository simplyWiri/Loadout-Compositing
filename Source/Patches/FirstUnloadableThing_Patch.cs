using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    [HarmonyPatch]
    public static class FirstUnloadableThing_Patch {

        private static MethodInfo shouldDrop = AccessTools.Method(typeof(FirstUnloadableThing_Patch), nameof(FirstUnloadableThing_Patch.ShouldDropThing));
        private static MethodInfo inLoadout = AccessTools.Method(typeof(FirstUnloadableThing_Patch), nameof(FirstUnloadableThing_Patch.ThingInLoadout));

        public static MethodInfo TargetMethod() {
            return typeof(Pawn_InventoryTracker).GetMethod("get_FirstUnloadableThing");
        }

        /*
 <Before>
        * foreach (Thing thing in this.innerContainer) {
        *     int index1 = -1;
        *     ...
        *
 <After>
        * foreach (Thing thing in this.innerContainer) {
        * {
        *       if ( ThingInLoadout(this, thing) ) {
        *           ThingCount tempCount;
        *           if ( ShouldDropThing(this, thing, ref tempCount) ) {
        *               return tempCount;
        *           } else {
        *               continue;
        *           }
        *       } 
        *
        *       int index1 = -1
        *       ...   
        */

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions,
            ILGenerator iLGenerator) {
            int matches = 0, branchMatches = 0;
            var insts = instructions.ToList();
            var localCount = iLGenerator.DeclareLocal(typeof(ThingCount));
            var continueExecutingLabel = iLGenerator.DefineLabel();
            var breakLoopLabel = iLGenerator.DefineLabel();

            for (int i = 0; i < insts.Count; i++) {
                if (Matches(insts, i)) {
                    var thing = insts[i - 1].operand as LocalBuilder;
                    
                    for (int j = i; j < insts.Count; j++) {
                        if (MatchesBranchCondition(insts, j)) {
                            insts[j].labels.Add(breakLoopLabel);
                            branchMatches++;
                        }
                    }

                    yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(insts[i]); // this
                    yield return new CodeInstruction(OpCodes.Ldloc_S, thing.LocalIndex); // thing
                    yield return new CodeInstruction(OpCodes.Call, inLoadout);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, continueExecutingLabel);

                    // if we are here, the item is from our loadout, and we now want to see if it is here in
                    // excess, and as such should be removed
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // this
                    yield return new CodeInstruction(OpCodes.Ldloc_S, thing.LocalIndex); // thing
                    yield return new CodeInstruction(OpCodes.Ldloca, localCount); // ref thingCount
                    yield return new CodeInstruction(OpCodes.Call, shouldDrop);
                    yield return new CodeInstruction(OpCodes.Brfalse_S, breakLoopLabel);
                    yield return new CodeInstruction(OpCodes.Ldloc, localCount); // return ref thingCount
                    yield return new CodeInstruction(OpCodes.Ret);

                    yield return insts[i].WithLabels(continueExecutingLabel);
                    matches++;
                }
                else {
                    yield return insts[i];
                }
            }

            if (matches != 1 || branchMatches != 1) {
                Utility.TranspilerError(__originalMethod, "Stopping pawns continuously dropping items from their loadout");
            }
        }

        public static bool Matches(List<CodeInstruction> instructions, int index) {
            return index > 1 && index < instructions.Count - 2
                   && instructions[index - 1].opcode == OpCodes.Stloc_S
                   && instructions[index + 0].opcode == OpCodes.Ldc_I4_M1
                   && instructions[index + 1].opcode == OpCodes.Stloc_S;
        }

        public static bool MatchesBranchCondition(List<CodeInstruction> instructions, int index) {
            // Looking for the end of the main loop - MoveNext call
            // IL_031d: ldloca.s     V_8
            // IL_031f: call         instance bool valuetype [mscorlib]System.Collections.Generic.List`1/Enumerator<class Verse.Thing>::MoveNext()
            // IL_0324: brtrue       IL_014b
            return index < instructions.Count - 3
                   && instructions[index + 0].opcode == OpCodes.Ldloca_S
                   && instructions[index + 1].opcode == OpCodes.Call
                   && instructions[index + 1].operand.ToString().Contains("MoveNext")
                   && instructions[index + 2].opcode == OpCodes.Brtrue;
        }

        public static bool ThingInLoadout(Pawn_InventoryTracker inventory, Thing thing) {
            if (!inventory.pawn.IsValidLoadoutHolder()) return false;

            var comp = inventory.pawn.TryGetComp<LoadoutComponent>();
            return comp?.Loadout?.Desires(thing, true) ?? false;
        }

        // precondition: the thing pointed to by `thingIndex` must be in the pawns loadout
        public static bool ShouldDropThing(Pawn_InventoryTracker inventory, Thing thing, ref ThingCount count) {
            var comp = inventory.pawn.GetComp<LoadoutComponent>();

            var item = comp.Loadout.ItemsAccepting(thing).FirstOrDefault();

            if (item is null) {
                count = new ThingCount(thing, thing.stackCount);
                return true;
            }
            
            var allThings = inventory.pawn.InventoryAndEquipment().ToList();
            var currentQuantity = item.CountIn(allThings);
            var desiredQuantity = comp.Loadout.DesiredCount(allThings, item);

            var difference = currentQuantity - desiredQuantity;
            if (difference <= 0) return false;
            
            count = new ThingCount(thing, Mathf.Min(difference, thing.stackCount));
            return true;
        }

    }

}