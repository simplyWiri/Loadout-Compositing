using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    // Which body parts the prosthetic items of a set of tags would be installed on, and which items don't fit. Items
    // are placed greedily in priority order, as hypothetical worn apparel is. An added part (e.g. a bionic arm on the
    // shoulder) takes its part and everything below it; an implant takes no space, but can't sit below an added part
    // or beside an implant it is incompatible with.
    public class ProstheticSlots {
        private readonly BodyDef body;
        private readonly Dictionary<BodyPartRecord, List<Tuple<RecipeDef, Tag>>> reserved = new Dictionary<BodyPartRecord, List<Tuple<RecipeDef, Tag>>>();
        private readonly Dictionary<ThingDef, List<BodyPartRecord>> placedDefs = new Dictionary<ThingDef, List<BodyPartRecord>>();
        private readonly Dictionary<Item, Tag> conflicts = new Dictionary<Item, Tag>();

        // items which could not be (fully) placed, along with the tag which took their place, or null if a body
        // simply hasn't enough parts
        public IReadOnlyDictionary<Item, Tag> Conflicts => conflicts;

        public bool Conflicting(Item item) => conflicts.ContainsKey(item);

        // `items` in priority order, best first
        public ProstheticSlots(BodyDef body, IEnumerable<Tuple<Tag, Item>> items) {
            this.body = body;

            foreach (var (tag, item) in items) {
                if (item.IsProsthetic) Place(item, tag);
            }
        }

        // could another `def` be installed alongside everything placed
        public bool Fits(ThingDef def) {
            var recipe = RecipeFor(def);
            return recipe == null || !recipe.targetsBodyPart || ProstheticUtility.TargetParts(body, recipe).Any(part => BlockedBy(recipe, part) == null);
        }

        // could `def` go on `part`, i.e. it was placed there, or nothing placed is in the way
        public bool Free(ThingDef def, BodyPartRecord part) {
            if (placedDefs.TryGetValue(def, out var placed) && placed.Contains(part)) return true;

            var recipe = RecipeFor(def);
            return recipe != null && BlockedBy(recipe, part) == null;
        }

        private void Place(Item item, Tag tag) {
            var def = item.RepresentativeDef;
            var recipe = RecipeFor(def);
            if (recipe == null || !recipe.targetsBodyPart) return;

            // the same prosthetic asked for by several tags is shared between them
            if (!placedDefs.TryGetValue(def, out var placed)) {
                placed = new List<BodyPartRecord>();
                placedDefs.Add(def, placed);
            }

            Tag blocker = null;
            foreach (var part in ProstheticUtility.TargetParts(body, recipe)) {
                if (placed.Count >= item.Quantity) break;
                if (placed.Contains(part)) continue;

                var blockedBy = BlockedBy(recipe, part);
                if (blockedBy != null) {
                    blocker ??= blockedBy.Item2;
                    continue;
                }

                if (!reserved.TryGetValue(part, out var onPart)) {
                    onPart = new List<Tuple<RecipeDef, Tag>>();
                    reserved.Add(part, onPart);
                }

                onPart.Add(new Tuple<RecipeDef, Tag>(recipe, tag));
                placed.Add(part);
            }

            if (placed.Count < item.Quantity) {
                conflicts[item] = blocker;
            }
        }

        // the reservation stopping `recipe` installing on `part`, or null if nothing does
        private Tuple<RecipeDef, Tag> BlockedBy(RecipeDef recipe, BodyPartRecord part) {
            // nothing can go on or below an added part
            for (var ancestor = part; ancestor != null; ancestor = ancestor.parent) {
                var addedPart = Reservations(ancestor).FirstOrDefault(r => ProstheticUtility.IsAddedPart(r.Item1));
                if (addedPart != null) return addedPart;
            }

            if (ProstheticUtility.IsAddedPart(recipe)) {
                // an added part replaces everything below it
                return SelfAndDescendants(part).SelectMany(Reservations).FirstOrDefault();
            }

            return Reservations(part).FirstOrDefault(r => r.Item1.addsHediff == recipe.addsHediff || !recipe.CompatibleWithHediff(r.Item1.addsHediff));
        }

        private IEnumerable<Tuple<RecipeDef, Tag>> Reservations(BodyPartRecord part) {
            return reserved.TryGetValue(part, out var onPart) ? onPart : Enumerable.Empty<Tuple<RecipeDef, Tag>>();
        }

        private static IEnumerable<BodyPartRecord> SelfAndDescendants(BodyPartRecord part) {
            yield return part;
            foreach (var descendant in part.parts.SelectMany(SelfAndDescendants)) {
                yield return descendant;
            }
        }

        private RecipeDef RecipeFor(ThingDef def) {
            return ProstheticUtility.InstallRecipesFor(def).FirstOrDefault(recipe => !recipe.targetsBodyPart || ProstheticUtility.TargetParts(body, recipe).Any());
        }
    }

}
