using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Loadout : IExposable {

        // should never be used, exists for backwards compat
        private List<Tag> tags = null;

        public List<LoadoutElement> elements;
        public List<ThingCount> itemsToRemove;
        public LoadoutState currentState;

        public IEnumerable<Tag> AllTags => elements.Select(elem => elem.Tag);
        public IEnumerable<Item> AllItems => AllTags.SelectMany(t => t.requiredItems);
        public IEnumerable<Tag> Tags => TagsWith(currentState);
        public IEnumerable<Tag> TagsWith(LoadoutState state) => elements.Where(e => e.Active(state)).Select(e => e.Tag);
        public IEnumerable<Item> Items => Tags.SelectMany(t => t.requiredItems);

        public Loadout() {
            elements = new List<LoadoutElement>();
            itemsToRemove = new List<ThingCount>();
        }
        
        public int IndexOf(Tag tag) {
            return elements.FirstIndexOf(le => le.Tag == tag);
        }

        public float WeightAtWhichLoadoutDesires(Thing thing) {
            const float priorityMultiplier = 5;

            var tag = Tags.FirstOrDefault(t => t.ItemsMatching(thing).Any());
            if (tag == null) return 0;

            var tagIndex = IndexOf(tag);
            var tagPriority = Mathf.Pow(priorityMultiplier, elements.Count - tagIndex);
            return tagPriority;
        }

        public bool Desires(Thing thing) {
            return Tags.Any(t => t.ItemsMatching(item => item.Allows(thing)).Any());
        }

        public IEnumerable<Item> ItemsAccepting(Thing thing) {
            return Tags.SelectMany(t => t
                .ItemsMatching(item => item.Allows(thing)));
        }

        public IEnumerable<Item> HypotheticalWornApparel(LoadoutState state, BodyDef def) {
            var itemsWithPrios = TagsWith(state).SelectMany(t => t
                    .ItemsWithTagMatching(item => item.Def.IsApparel)
                    .Select(tuple => new Tuple<Item, Tag, int>(tuple.Item2, tuple.Item1, elements.FindIndex(e => e.Tag == tuple.Item1))))
                .ToList();

            var wornApparel = ApparelUtility.WornApparelFor(def, itemsWithPrios) ?? new List<Tuple<Item, Tag>>();
            return wornApparel.Select(tuple => tuple.Item1);
        }

        public IEnumerable<Tuple<Item, Tag>> HypotheticalWornApparelWithTag(LoadoutState state, BodyDef def) {
            var itemsWithPrios = TagsWith(state).SelectMany(t => t
                    .ItemsWithTagMatching(item => item.Def.IsApparel)
                    .Select(tuple => new Tuple<Item, Tag, int>(tuple.Item2, tuple.Item1, elements.FindIndex(e => e.Tag == tuple.Item1))))
                .ToList();

            var wornApparel = ApparelUtility.WornApparelFor(def, itemsWithPrios) ?? new List<Tuple<Item, Tag>>();
            return wornApparel;
        }

        public IEnumerable<Item> DesiredItems(List<Thing> heldThings) {
            var desiredThings = Items.Where(t => !t.Def.IsApparel);
            foreach (var thing in desiredThings) {
                var count = heldThings.Where(heldThing => thing.Allows(heldThing))
                    .Sum(heldThing => heldThing.stackCount);

                if (count >= thing.Quantity)
                    continue;

                yield return thing;
            }
        }

        public void ExposeData() {
            // backwards compatibility, todo remove
            if (Scribe.mode != LoadSaveMode.Saving) {
                Scribe_Collections.Look(ref tags, nameof(tags), LookMode.Reference);
            
                if (!tags.NullOrEmpty()) {
                    elements = tags.Select(t => new LoadoutElement(t, null)).ToList();
                }
            }
            
            Scribe_Collections.Look(ref elements, nameof(elements), LookMode.Deep);
            Scribe_Collections.Look(ref itemsToRemove, nameof(itemsToRemove), LookMode.Deep);
            Scribe_References.Look(ref currentState, nameof(currentState));
            
            itemsToRemove ??= new List<ThingCount>();
            elements ??= new List<LoadoutElement>();
        }

    }

}