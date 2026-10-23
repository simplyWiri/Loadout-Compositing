using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    // Prosthetics are the things installed by surgery, e.g. a field hand. Any surgery which adds a hediff and takes a
    // single tech hediff thing as its ingredient counts, so mods which follow the vanilla pattern (e.g. EPOE) need no
    // special handling. A tag item of a prosthetic is installed on the pawn, rather than carried.
    public static class ProstheticUtility {
        private static Dictionary<ThingDef, List<RecipeDef>> installRecipes = null;

        private static Dictionary<ThingDef, List<RecipeDef>> InstallRecipes => installRecipes ??= BuildIndex();

        public static bool IsProsthetic(ThingDef def) => def != null && InstallRecipes.ContainsKey(def);

        public static IReadOnlyList<RecipeDef> InstallRecipesFor(ThingDef def) {
            return InstallRecipes.TryGetValue(def, out var recipes) ? recipes : new List<RecipeDef>();
        }

        // the surgery which installs `def` on `pawn`, or null if their race has none
        public static RecipeDef InstallRecipeFor(Pawn pawn, ThingDef def) {
            return InstallRecipesFor(def).FirstOrDefault(recipe => pawn.def.AllRecipes.Contains(recipe));
        }

        // how many of `def` are installed on `pawn`
        public static int InstalledCount(Pawn pawn, ThingDef def) {
            var hediffs = InstallRecipesFor(def).Select(recipe => recipe.addsHediff).ToHashSet();
            return pawn.health.hediffSet.hediffs.Count(hediff => hediffs.Contains(hediff.def));
        }

        private static Dictionary<ThingDef, List<RecipeDef>> BuildIndex() {
            var index = new Dictionary<ThingDef, List<RecipeDef>>();

            foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading) {
                if (!recipe.IsSurgery || recipe.addsHediff == null || !(recipe.Worker is Recipe_Surgery)) continue;
                // levelled implants (e.g. psylinks) are raised one level at a time, rather than installed once
                if (typeof(Hediff_Level).IsAssignableFrom(recipe.addsHediff.hediffClass)) continue;
                if (recipe.ingredients.Count != 1 || !recipe.ingredients[0].IsFixedIngredient) continue;

                var def = recipe.ingredients[0].FixedIngredient;
                if (def == null || !def.isTechHediff) continue;

                if (!index.TryGetValue(def, out var recipes)) {
                    recipes = new List<RecipeDef>();
                    index.Add(def, recipes);
                }

                recipes.Add(recipe);
            }

            if (Prefs.DevMode) {
                Log.Message($"[Loadout Compositing] Found {index.Count} prosthetics, installed by {index.Values.Sum(r => r.Count)} surgeries");
            }

            return index;
        }
    }

}
