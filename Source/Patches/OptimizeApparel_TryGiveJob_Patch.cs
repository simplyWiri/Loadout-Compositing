using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory
{

    // Pawns should automatically drop apparel which they are wearing if it does not meet the criteria of their current
    // set of apparel speicifed by their loadout (when the mod setting is active).
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), nameof(JobGiver_OptimizeApparel.TryGiveJob))]
    public static class OptimizeApparel_TryGiveJob_Patch
    {
        public static MethodInfo targetMethod = AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.Allows), new System.Type[] { typeof(Thing) });
        public static MethodInfo injectMethod = AccessTools.Method(typeof(OptimizeApparel_TryGiveJob_Patch), nameof(OptimizeApparel_TryGiveJob_Patch.ShouldDrop));

        // Conditional looks something like
        // if ( !outfitContainsApparel(outfit, wornApparel[i]) && canDrop(...) )
        // we are adding an extra condition to the left side, I.e.
        // if ( (!outfitContainsApparel(outfit, wornApparel[i] || loadoutShouldDrop(wornApparel[i]) && canDrop(...) )

        // More literally:
        // if (!currentOutfit.filter.Allows(wornApparel[i]) && pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel[i]) && !pawn.apparel.IsLocked(wornApparel[i]))
        // to
        // if ( (!currentOutfit.filter.Allows(wornApparel[i]) || shouldDrop(pawn, wornApparel[i])) && pawn.outfits.forcedHandler.AllowedToAutomaticallyDrop(wornApparel[i]) && !pawn.apparel.IsLocked(wornApparel[i]))

        public static IEnumerable<CodeInstruction> Transpiler(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions) {
            int matches = 0;
            var insts = instructions.ToList();

            for (int i = 0; i < insts.Count; i++)
            {
                if (Matches(insts, i)) {
                    matches++;
                    yield return new CodeInstruction(OpCodes.Ldarg_1); // pawn : Pawn
                    yield return insts[i - 4]; // wornApparel : List<Apparel>
                    yield return insts[i - 3]; // i : int
                    yield return insts[i - 2]; // wornApparel[i]
                    yield return new CodeInstruction(OpCodes.Call, injectMethod); // shouldDrop(pawn, apparel[i]);
                    yield return new CodeInstruction(OpCodes.Not);
                    yield return new CodeInstruction(OpCodes.And);
                }

                yield return insts[i];
            }

            if (matches != 1) {
                Log.ErrorOnce($"[Loadout Compositing] {matches} Failed to apply patch to enforce pawns dropping apparel not in their loadout if the only wear apparel in loadout setting is active", 485949272);
            }
        }

        public static bool ShouldDrop(Pawn pawn, Apparel apparel)
        {
            // We don't care in this branch.
            if (!ModBase.settings.onlyItemsFromLoadout)
            {
                return false;
            }

            return !pawn.TryGetComp<LoadoutComponent>()?.Loadout.Desires(apparel) ?? false;
        }

        public static bool Matches(List<CodeInstruction> instructions, int i)
        {
            return i >= 6
                && instructions[i - 6].opcode == OpCodes.Ldloc_1
                && instructions[i - 5].opcode == OpCodes.Ldfld
                && instructions[i - 4].opcode == OpCodes.Ldloc_2
                && instructions[i - 3].opcode == OpCodes.Ldloc_S
                && instructions[i - 2].opcode == OpCodes.Callvirt
                && instructions[i - 1].opcode == OpCodes.Callvirt
                && instructions[i - 1].Calls(targetMethod)
                && instructions[i + 0].opcode == OpCodes.Brtrue_S;
        }

    }

}