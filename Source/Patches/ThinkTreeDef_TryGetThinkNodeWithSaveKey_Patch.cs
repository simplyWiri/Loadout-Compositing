using HarmonyLib;
using Verse;
using Verse.AI;

namespace Inventory {

    // A job remembers the think node which gave it, by key. On loading, vanilla only looks for that node among the
    // main tree's children, but our node is inserted by tag, which vanilla attaches lazily and doesn't count as a
    // child. So jobs our node gave (e.g. fetching an item) would log "Could not find think node" when loaded mid-way.
    [HarmonyPatch(typeof(ThinkTreeDef), nameof(ThinkTreeDef.TryGetThinkNodeWithSaveKey))]
    public static class ThinkTreeDef_TryGetThinkNodeWithSaveKey_Patch {

        public static void Postfix(int key, ref ThinkNode outNode, ref bool __result) {
            if (__result || key == -1) return;

            var root = DefDatabase<ThinkTreeDef>.GetNamedSilentFail("CL_LoadoutRealisation")?.thinkRoot;
            if (root == null || root.UniqueSaveKey != key) return;

            outNode = root;
            __result = true;
        }

    }

}
