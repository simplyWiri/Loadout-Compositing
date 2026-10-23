using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory {

    // A shortcut pinned in the tag editor which adds an ordinary group holding every def of a kind, best first. Once
    // added it is a normal item, its alternatives can be reordered or removed like any other group.
    public class ItemPreset {
        public static readonly ItemPreset Medicine = new ItemPreset("Medicine",
            def => def.IsMedicine,
            defs => defs.OrderByDescending(def => def.GetStatValueAbstract(StatDefOf.MedicalPotency)));

        public static readonly ItemPreset Meals = new ItemPreset("Meals",
            def => def.ingestible?.IsMeal ?? false,
            defs => defs
                .OrderByDescending(def => def.ingestible.preferability)
                .ThenByDescending(def => def.GetStatValueAbstract(StatDefOf.Nutrition)));

        public readonly string key;
        private readonly Func<ThingDef, bool> accepts;
        private readonly Func<IEnumerable<ThingDef>, IOrderedEnumerable<ThingDef>> order;

        private List<ThingDef> defs;

        public string Label => $"{Strings.P}ItemPreset.{key}".TranslateSimple();

        // best first, empty until the def lists have been calculated
        public List<ThingDef> Defs {
            get {
                if (defs == null && Utility.items != null) {
                    defs = order(Utility.items.Where(accepts)).ToList();
                }

                return defs ?? new List<ThingDef>();
            }
        }

        private ItemPreset(string key, Func<ThingDef, bool> accepts, Func<IEnumerable<ThingDef>, IOrderedEnumerable<ThingDef>> order) {
            this.key = key;
            this.accepts = accepts;
            this.order = order;
        }

        public Item MakeItem() {
            var defs = Defs;
            var item = new Item(defs[0]);
            foreach (var def in defs.Skip(1)) {
                item.AddAlternative(def);
            }

            return item;
        }
    }

}
