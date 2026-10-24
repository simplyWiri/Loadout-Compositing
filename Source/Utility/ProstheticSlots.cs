using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    // Which of the prosthetic items of a set of tags can be installed together. As with apparel, a prosthetic claims
    // every part of the kind it goes on, however many are asked for: a bionic leg takes both legs, so no other leg
    // fits alongside it, and a bionic arm takes both shoulders and everything below them (e.g. hands). An implant
    // only clashes with an added part above it, or with an incompatible implant on the same part. Items are taken in
    // priority order, and any which overlap one taken before them conflict with it.
    public class ProstheticSlots {
        // how a prosthetic takes a part: installed on it, claimed but not installed on (e.g. the second shoulder of a
        // single bionic arm), or replaced along with the added part above it (e.g. the toes under a bionic leg)
        public enum PartUse { Installed, Reserved, Replaced }

        public class Placement {
            public Item item;
            public Tag tag;
            public ThingDef def;
            public PartUse use;
        }

        private readonly BodyDef body;
        private readonly List<Tuple<ThingDef, Tag>> accepted = new List<Tuple<ThingDef, Tag>>();
        private readonly Dictionary<Item, Tag> conflicts = new Dictionary<Item, Tag>();
        private readonly Dictionary<BodyPartRecord, List<Placement>> placements = new Dictionary<BodyPartRecord, List<Placement>>();
        private readonly Dictionary<BodyPartRecord, List<Tuple<Item, Tag>>> blocked = new Dictionary<BodyPartRecord, List<Tuple<Item, Tag>>>();

        // items which can't be installed, along with the tag whose prosthetic is in the way, or null if a body
        // simply hasn't enough parts for them
        public IReadOnlyDictionary<Item, Tag> Conflicts => conflicts;

        public bool Conflicting(Item item) => conflicts.ContainsKey(item);

        // the prosthetics which take `part`, and how
        public IReadOnlyList<Placement> PlacementsOn(BodyPartRecord part) {
            return placements.TryGetValue(part, out var list) ? list : new List<Placement>();
        }

        // the prosthetics (and the tags asking for them) which would go on `part`, but conflict with another
        public IReadOnlyList<Tuple<Item, Tag>> BlockedOn(BodyPartRecord part) {
            return blocked.TryGetValue(part, out var list) ? list : new List<Tuple<Item, Tag>>();
        }

        // `items` in priority order, best first
        public ProstheticSlots(BodyDef body, IEnumerable<Tuple<Tag, Item>> items) {
            this.body = body;

            foreach (var (tag, item) in items) {
                if (item.IsProsthetic) Place(item, tag);
            }
        }

        // could another `def` be installed alongside everything accepted
        public bool Fits(ThingDef def) => accepted.All(other => other.Item1 == def || !Overlaps(body, def, other.Item1));

        // could `def` go on `part`, i.e. it was accepted, and `part` is one it goes on
        public bool Free(ThingDef def, BodyPartRecord part) {
            return accepted.Any(other => other.Item1 == def) && Coverage.For(body, def).targets.Contains(part);
        }

        private void Place(Item item, Tag tag) {
            var def = item.RepresentativeDef;
            var coverage = Coverage.For(body, def);
            if (coverage.recipe == null) return;

            // the same prosthetic asked for by several tags is shared between them
            var blocker = accepted.FirstOrDefault(other => other.Item1 != def && Overlaps(body, def, other.Item1));
            if (blocker != null) {
                conflicts[item] = blocker.Item2;
                foreach (var part in coverage.targets) {
                    Add(blocked, part, new Tuple<Item, Tag>(item, tag));
                }
                return;
            }

            if (coverage.recipe.targetsBodyPart && coverage.targets.Count < item.Quantity) {
                conflicts[item] = null;
            }

            var shared = accepted.Any(other => other.Item1 == def);
            accepted.Add(new Tuple<ThingDef, Tag>(def, tag));
            if (!shared) PlaceOnParts(item, tag, def, coverage);
        }

        // the first parts (in body order) get as many as are asked for, the rest of the targets are reserved
        private void PlaceOnParts(Item item, Tag tag, ThingDef def, Coverage coverage) {
            Placement Make(PartUse use) => new Placement { item = item, tag = tag, def = def, use = use };

            var targets = body.AllParts.Where(coverage.targets.Contains).ToList();
            for (int i = 0; i < targets.Count; i++) {
                Add(placements, targets[i], Make(i < item.Quantity ? PartUse.Installed : PartUse.Reserved));
            }

            if (!coverage.addedPart) return;
            foreach (var part in body.AllParts.Where(part => coverage.covered.Contains(part) && !coverage.targets.Contains(part))) {
                Add(placements, part, Make(PartUse.Replaced));
            }
        }

        private static void Add<T>(Dictionary<BodyPartRecord, List<T>> dict, BodyPartRecord part, T value) {
            if (!dict.TryGetValue(part, out var list)) {
                list = new List<T>();
                dict.Add(part, list);
            }
            list.Add(value);
        }

        // whether `a` and `b` can't both be installed, as one replaces the parts the other goes on, or they are
        // incompatible implants on the same part
        public static bool Overlaps(BodyDef body, ThingDef a, ThingDef b) {
            var first = Coverage.For(body, a);
            var second = Coverage.For(body, b);
            if (first.recipe == null || second.recipe == null) return false;

            if (first.addedPart && second.targets.Overlaps(first.covered)) return true;
            if (second.addedPart && first.targets.Overlaps(second.covered)) return true;
            if (first.addedPart || second.addedPart) return false;

            return first.targets.Overlaps(second.targets)
                   && (first.recipe.addsHediff == second.recipe.addsHediff || !first.recipe.CompatibleWithHediff(second.recipe.addsHediff));
        }

        // the parts of a body a prosthetic goes on, and (for an added part) those it replaces: them, and all below
        private class Coverage {
            private static readonly Dictionary<Tuple<BodyDef, ThingDef>, Coverage> cache = new Dictionary<Tuple<BodyDef, ThingDef>, Coverage>();

            public RecipeDef recipe;
            public bool addedPart;
            public HashSet<BodyPartRecord> targets = new HashSet<BodyPartRecord>();
            public HashSet<BodyPartRecord> covered = new HashSet<BodyPartRecord>();

            public static Coverage For(BodyDef body, ThingDef def) {
                var key = new Tuple<BodyDef, ThingDef>(body, def);
                if (cache.TryGetValue(key, out var coverage)) return coverage;

                coverage = new Coverage {
                    recipe = ProstheticUtility.InstallRecipesFor(def).FirstOrDefault(r => !r.targetsBodyPart || ProstheticUtility.TargetParts(body, r).Any())
                };

                if (coverage.recipe != null) {
                    coverage.addedPart = ProstheticUtility.IsAddedPart(coverage.recipe);
                    coverage.targets = ProstheticUtility.TargetParts(body, coverage.recipe).ToHashSet();
                    coverage.covered = coverage.targets.SelectMany(SelfAndDescendants).ToHashSet();
                }

                cache[key] = coverage;
                return coverage;
            }

            private static IEnumerable<BodyPartRecord> SelfAndDescendants(BodyPartRecord part) {
                yield return part;
                foreach (var descendant in part.parts.SelectMany(SelfAndDescendants)) {
                    yield return descendant;
                }
            }
        }
    }

}
