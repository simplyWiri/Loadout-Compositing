using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
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

        // whether `pawn` could ever have `def`, ignoring research and what's already installed (e.g. not for their race,
        // or not at their age)
        public static bool CanEverInstall(Pawn pawn, ThingDef def) {
            var recipe = InstallRecipeFor(pawn, def);
            if (recipe == null || !recipe.Worker.AvailableOnNow(pawn)) return false;

            return !recipe.targetsBodyPart || TargetParts(pawn.RaceProps.body, recipe).Any();
        }

        // why `pawn` objects to having prosthetics installed, or null if they don't
        public static string Objection(Pawn pawn) {
            var trait = pawn.story?.traits?.GetTrait(TraitDefOf.BodyPurist);
            if (trait != null) return trait.LabelCap;

            var ideo = pawn.Ideo;
            if (ideo == null) return null;

            foreach (var defName in new[] { "BodyMod_Disapproved", "BodyMod_Abhorrent" }) {
                var precept = DefDatabase<PreceptDef>.GetNamedSilentFail(defName);
                if (precept != null && ideo.HasPrecept(precept)) return precept.LabelCap;
            }

            return null;
        }

        // the parts of `body` which `recipe` installs onto
        public static IEnumerable<BodyPartRecord> TargetParts(BodyDef body, RecipeDef recipe) {
            return body.AllParts.Where(part => (recipe.appliedOnFixedBodyParts?.Contains(part.def) ?? false)
                                               || (recipe.appliedOnFixedBodyPartGroups?.Any(group => part.groups.Contains(group)) ?? false));
        }

        // the most of `def` which could be installed on a pawn with `body` at once
        public static int MaxInstallable(BodyDef body, ThingDef def) {
            var max = InstallRecipesFor(def).Select(recipe => recipe.targetsBodyPart ? TargetParts(body, recipe).Count() : 1).DefaultIfEmpty(1).Max();
            return Mathf.Max(1, max);
        }

        // e.g. "left hand, right hand"
        public static string TargetPartsLabel(BodyDef body, ThingDef def) {
            var recipe = InstallRecipesFor(def).FirstOrDefault();
            if (recipe == null || !recipe.targetsBodyPart) return null;

            return string.Join(", ", TargetParts(body, recipe).Select(part => part.Label).Distinct());
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
