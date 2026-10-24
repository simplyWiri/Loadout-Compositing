using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // Prosthetics are the things installed by surgery, e.g. a field hand. Any surgery which adds a hediff and takes a
    // single tech hediff thing among its ingredients (alongside the medicine every surgery takes) counts, so mods which follow the vanilla pattern (e.g. EPOE) need no
    // special handling. A tag item of a prosthetic is installed on the pawn, rather than carried.
    public static class ProstheticUtility {
        private static Dictionary<ThingDef, List<RecipeDef>> installRecipes = null;
        private static Dictionary<ThingDef, Dictionary<string, float>> effects = new Dictionary<ThingDef, Dictionary<string, float>>();
        private static Dictionary<BodyDef, List<BodyPartRecord>> targetableParts = new Dictionary<BodyDef, List<BodyPartRecord>>();

        private static Dictionary<ThingDef, List<RecipeDef>> InstallRecipes => installRecipes ??= BuildIndex();

        public static bool IsProsthetic(ThingDef def) => def != null && InstallRecipes.ContainsKey(def);

        public static IReadOnlyList<RecipeDef> InstallRecipesFor(ThingDef def) {
            return InstallRecipes.TryGetValue(def, out var recipes) ? recipes : new List<RecipeDef>();
        }

        // the surgery which installs `def` on `pawn`, or null if their race has none
        public static RecipeDef InstallRecipeFor(Pawn pawn, ThingDef def) {
            return InstallRecipesFor(def).FirstOrDefault(recipe => pawn.def.AllRecipes.Contains(recipe));
        }

        // the prosthetic `recipe` installs, or null if it doesn't install one
        public static ThingDef ProstheticInstalledBy(RecipeDef recipe) {
            var def = PartIngredient(recipe);
            return def != null && InstallRecipesFor(def).Contains(recipe) ? def : null;
        }

        // the one tech hediff thing among `recipe`'s ingredients, or null if there isn't exactly one. The others are
        // the medicine, which is a category of things rather than a single (fixed) one
        private static ThingDef PartIngredient(RecipeDef recipe) {
            var defs = recipe.ingredients
                .Where(ingredient => ingredient.IsFixedIngredient)
                .Select(ingredient => ingredient.FixedIngredient)
                // raw materials (e.g. wood, for peg legs) are installed directly too, but they aren't prosthetics
                .Where(def => def != null && def.isTechHediff && !def.IsStuff)
                .ToList();

            return defs.Count == 1 ? defs[0] : null;
        }

        // how many of `def` are installed on `pawn`
        public static int InstalledCount(Pawn pawn, ThingDef def) {
            var hediffs = InstallRecipesFor(def).Select(recipe => recipe.addsHediff).ToHashSet();
            return pawn.health.hediffSet.hediffs.Count(hediff => hediffs.Contains(hediff.def));
        }

        // how many of `def`'s target parts have it, or a better prosthetic, installed
        public static int SatisfiedCount(Pawn pawn, ThingDef def) {
            var recipe = InstallRecipeFor(pawn, def);
            if (recipe == null) return InstalledCount(pawn, def);

            var targets = TargetParts(pawn.RaceProps.body, recipe).ToHashSet();
            return pawn.health.hediffSet.hediffs.Count(h => h.def == recipe.addsHediff || (h.Part != null && targets.Contains(h.Part) && IsBetter(h.def, recipe.addsHediff)));
        }

        // whether the part `hediff` adds works better than the one `than` adds, e.g. an archotech over a bionic leg
        public static bool IsBetter(HediffDef hediff, HediffDef than) {
            return hediff.addedPartProps != null && than.addedPartProps != null && hediff.addedPartProps.partEfficiency > than.addedPartProps.partEfficiency;
        }

        // an added part replaces its body part, and everything below it, rather than sitting on it as an implant does
        public static bool IsAddedPart(RecipeDef recipe) => typeof(Hediff_AddedPart).IsAssignableFrom(recipe.addsHediff.hediffClass);

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

        // the parts of `body` which any prosthetic goes on, in body order
        public static IReadOnlyList<BodyPartRecord> TargetableParts(BodyDef body) {
            if (targetableParts.TryGetValue(body, out var cached)) return cached;

            var parts = InstallRecipes.Values.SelectMany(recipes => recipes)
                .Where(recipe => recipe.targetsBodyPart)
                .SelectMany(recipe => TargetParts(body, recipe))
                .ToHashSet();

            var result = body.AllParts.Where(parts.Contains).ToList();
            targetableParts.Add(body, result);
            return result;
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

        // what installing `def` does, by the label the game shows it under in the hediff's stats, e.g. "Food poisoning
        // chance factor" -> 0.5. An immunity has a value of 1, e.g. "Prevents infection: gut worms" -> 1
        public static IReadOnlyDictionary<string, float> EffectsOf(ThingDef def) {
            if (effects.TryGetValue(def, out var cached)) return cached;

            var result = new Dictionary<string, float>();
            effects.Add(def, result);

            // the same label may come up twice (e.g. a stat offset and factor), the first is kept
            void Add(string label, float value) {
                if (!result.ContainsKey(label)) result.Add(label, value);
            }

            var hediff = InstallRecipesFor(def).FirstOrDefault()?.addsHediff;
            if (hediff == null) return result;

            if (hediff.addedPartProps != null) Add("PartEfficiency".Translate(), hediff.addedPartProps.partEfficiency);

            var stage = hediff.stages?.FirstOrDefault();
            if (stage == null) return result;

            foreach (var capMod in stage.capMods ?? new List<PawnCapacityModifier>()) {
                if (capMod.offset != 0f) Add(capMod.capacity.GetLabelFor().CapitalizeFirst(), capMod.offset);
                if (capMod.postFactor != 1f) Add(capMod.capacity.GetLabelFor().CapitalizeFirst(), capMod.postFactor);
            }

            foreach (var modifier in (stage.statOffsets ?? new List<StatModifier>()).Concat(stage.statFactors ?? new List<StatModifier>())) {
                Add(modifier.stat.LabelCap, modifier.value);
            }

            if (stage.painOffset != 0f) Add("Pain".Translate(), stage.painOffset);
            if (stage.painFactor != 1f) Add("Pain".Translate(), stage.painFactor);
            if (stage.hungerRateFactor != 1f) Add("HungerRate".Translate(), stage.hungerRateFactor);
            if (stage.restFallFactor != 1f) Add("Tiredness".Translate(), stage.restFallFactor);
            if (stage.totalBleedFactor != 1f) Add("Stat_Hediff_TotalBleedFactor_Name".Translate(), stage.totalBleedFactor);
            if (stage.naturalHealingFactor != -1f) Add("Stat_Hediff_NaturalHealingFactor_Name".Translate(), stage.naturalHealingFactor);
            if (stage.foodPoisoningChanceFactor != 1f) Add("Stat_Hediff_FoodPoisoningChanceFactor_Name".Translate(), stage.foodPoisoningChanceFactor);

            foreach (var immunity in stage.makeImmuneTo ?? new List<HediffDef>()) {
                Add("PreventsInfection".Translate() + ": " + immunity.label, 1f);
            }

            return result;
        }

        private static Dictionary<ThingDef, List<RecipeDef>> BuildIndex() {
            var index = new Dictionary<ThingDef, List<RecipeDef>>();

            foreach (var recipe in DefDatabase<RecipeDef>.AllDefsListForReading) {
                if (!recipe.IsSurgery || recipe.addsHediff == null || !(recipe.Worker is Recipe_Surgery)) continue;
                // levelled implants (e.g. psylinks) are raised one level at a time, rather than installed once
                if (typeof(Hediff_Level).IsAssignableFrom(recipe.addsHediff.hediffClass)) continue;
                var def = PartIngredient(recipe);
                if (def == null) continue;

                if (!index.TryGetValue(def, out var recipes)) {
                    recipes = new List<RecipeDef>();
                    index.Add(def, recipes);
                }

                recipes.Add(recipe);
            }

            // a thing which installs as different parts (e.g. one install recipe per part) can't say which it means
            foreach (var def in index.Keys.ToList().Where(def => index[def].Select(recipe => recipe.addsHediff).Distinct().Count() > 1)) {
                index.Remove(def);
            }

            if (Prefs.DevMode) {
                Log.Message($"[Loadout Compositing] Found {index.Count} prosthetics, installed by {index.Values.Sum(r => r.Count)} surgeries");
            }

            return index;
        }
    }

}
