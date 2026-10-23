using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    // A hard-coded category of things (e.g. any medicine), an `Item` of a category saves only its `key`. Members are
    // computed from the def database and ranked best first, things which are equally good share a rank.
    public class GenericCategory {
        public static readonly GenericCategory Medicine = new GenericCategory("Medicine",
            def => def.IsMedicine,
            members => members.OrderByDescending(def => def.GetStatValueAbstract(StatDefOf.MedicalPotency)),
            def => def.GetStatValueAbstract(StatDefOf.MedicalPotency));

        public static readonly GenericCategory Meals = new GenericCategory("Meals",
            def => def.ingestible?.IsMeal ?? false,
            members => members
                .OrderByDescending(def => def.ingestible.preferability)
                .ThenByDescending(def => def.GetStatValueAbstract(StatDefOf.Nutrition)),
            def => (def.ingestible.preferability, def.GetStatValueAbstract(StatDefOf.Nutrition)));

        public static readonly List<GenericCategory> All = new List<GenericCategory> { Medicine, Meals };

        public readonly string key;
        private readonly Func<ThingDef, bool> accepts;
        private readonly Func<IEnumerable<ThingDef>, IOrderedEnumerable<ThingDef>> order;
        private readonly Func<ThingDef, object> rankKey;

        private List<ItemMember> members;
        private Dictionary<ThingDef, int> ranks;

        public string Label => $"{Strings.P}GenericCategory.{key}".TranslateSimple();

        // empty until the def lists have been calculated
        public List<ItemMember> Members {
            get {
                if (members == null) CalculateMembers();
                return members ?? new List<ItemMember>();
            }
        }

        private GenericCategory(string key, Func<ThingDef, bool> accepts, Func<IEnumerable<ThingDef>, IOrderedEnumerable<ThingDef>> order, Func<ThingDef, object> rankKey) {
            this.key = key;
            this.accepts = accepts;
            this.order = order;
            this.rankKey = rankKey;
        }

        public static GenericCategory Named(string key) => All.FirstOrDefault(category => category.key == key);

        public int RankOf(ThingDef def) => ranks != null && ranks.TryGetValue(def, out var rank) ? rank : -1;

        public void CalculateMembers() {
            if (Utility.items == null) return;

            var defs = order(Utility.items.Where(accepts)).ToList();
            members = defs.Select(def => new ItemMember(new SafeDef(def), new HashSet<SafeDef>())).ToList();

            ranks = new Dictionary<ThingDef, int>();
            var rank = -1;
            object lastKey = null;
            foreach (var def in defs) {
                var curKey = rankKey(def);
                if (!Equals(curKey, lastKey)) rank++;
                ranks[def] = rank;
                lastKey = curKey;
            }
        }
    }

}
