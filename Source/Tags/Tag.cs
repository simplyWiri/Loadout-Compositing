using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Tag : IComparable<Tag>, IExposable, ILoadReferenceable {
        public static bool GenericLoad = false;
        public static int FakeId = 0;

        public List<Item> requiredItems = null;
        public string name = null;
        public bool defaultEnabled = false;
        public int uniqueId = -1;

        // What & Why?
        // It is a signifier of the context in which we are loading this tag, if this is being loaded in the context of the 
        // settings menu, this will be `GenericTag_`, otherwise for real uses in game saved in the GameComponent, this will just 
        // be `Tag_`. This is used by the ILoadReferenceable interface to scribe references to this unit, tags which get saved in
        // the generic (settings) menu should never be referenced inside an actual game (a copy should be made), and as such we could
        // set the unique id returned by these tags to be garbage (in `GetUniqueLoadID`)
        public string idType = "Tag_";

        public Tag() {
            this.requiredItems = new List<Item>();
            this.uniqueId = -1;
            this.name = "";
        }

        public Tag(string name) {
            this.name = name;
            this.requiredItems = new List<Item>();
            this.uniqueId = LoadoutManager.GetNextTagId();
            if (name == "") {
                this.name = "Placeholder-" + uniqueId;
            }
        }

        public Tag MakeCopy() {
            var tag = new Tag();
            tag.name = this.name;
            tag.requiredItems = this.requiredItems.ListFullCopy();
            tag.uniqueId = this.uniqueId;
            return tag;
        }

        public IEnumerable<Item> ItemsMatching(Thing thing) {
            return requiredItems.Where(item => thing.def == item.Def && item.Filter.Allows(thing));
        }

        public IEnumerable<Item> ItemsMatching(Predicate<Item> pred) {
            return requiredItems.Where(item => pred(item));
        }

        public IEnumerable<Tuple<Tag, Item>> ItemsWithTagMatching(Predicate<Item> pred) {
            return requiredItems.Where(item => pred(item)).Select(item => new Tuple<Tag, Item>(this, item));
        }

        public void Add(ThingDef thing) {
            requiredItems.Add(new Item(thing));
        }

        // for a given list of items, return the number of elements which this tag can claim ownership of
        // I.e.
        // Tag: 50 wood, 5 herbal meds,
        // Things: Wood x 75, Wood x 75, Herbal Meds x3
        // Result: 50 wood, 3 herbal meds 
        public IEnumerable<ThingCount> ThingsAcceptedInList(List<Thing> things) {
            foreach (var thing in things) {
                var items = ItemsMatching(thing).ToList();
                if (items.Count == 0) continue;

                var expectedQuantity = items.Sum(i => i.Quantity);

                yield return new ThingCount(thing, Mathf.Max(thing.stackCount, thing.stackCount - expectedQuantity));
            }
        }

        public bool HasThingDef(ThingDef def, out Item item) {
            item = null;
            foreach (var i in requiredItems) {
                if (i.Def == def) {
                    item = i;
                    return true;
                }
            }

            return false;
        }

        public int CompareTo(Tag other) {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;

            return string.Compare(name, other.name, StringComparison.Ordinal);
        }

        public void ExposeData() {
            Scribe_Collections.Look(ref requiredItems, nameof(requiredItems));
            Scribe_Values.Look(ref name, nameof(name));
            Scribe_Values.Look(ref uniqueId, nameof(uniqueId));
            Scribe_Values.Look(ref defaultEnabled, nameof(defaultEnabled), false);

            if (!GenericLoad && idType != "GenericTag_") {
                var count = requiredItems.RemoveAll(item => item.Def == null);
                if (count != 0) {
                    Log.Error($"Attempting to load a null def, have you removed a mod? - Removing item from {name} - ({GetUniqueLoadID()})");
                }

                foreach(var item in requiredItems) {
                    count = item.filter.stuffs.RemoveWhere(thing => thing.Def is null);
                    if (count != 0) {
                        Log.Error($"Attempted to load a null stuff in a filter, have you removed a mod?");
                    }
                }

                idType = "Tag_";
            } else {
                idType = "GenericTag_";
            }
        }

        public string GetUniqueLoadID() {
            return idType + uniqueId;
        }
    }

}