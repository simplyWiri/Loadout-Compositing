using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    public class Tag : IComparable<Tag>, IExposable, ILoadReferenceable
    {
        public List<Item> requiredItems = null;
        public string name = null;
        public int uniqueId = -1;

        public Tag()
        {
            this.requiredItems = new List<Item>();
            this.uniqueId = LoadoutManager.GetNextTagId();
            this.name = "Placeholder-" + this.uniqueId;
        }

        public Tag(string name)
        {
            this.name = name;
            this.requiredItems = new List<Item>();
            this.uniqueId = LoadoutManager.GetNextTagId();
        }

        public IEnumerable<Item> ItemsMatching(Thing thing)
        {
            return requiredItems.Where(item => thing.def == item.Def && item.Filter.Allows(thing));
        }
        public IEnumerable<Item> ItemsMatching(Predicate<Item> pred)
        {
            return requiredItems.Where(item => pred(item));
        }
        
        public IEnumerable<Tuple<Tag, Item>> ItemsWithTagMatching(Predicate<Item> pred)
        {
            return requiredItems.Where(item => pred(item)).Select(item => new Tuple<Tag, Item>(this, item));
        }

        public void Add(ThingDef thing)
        {
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
                if (i.Def == def)
                {
                    item = i;
                    return true;
                }
            }

            return false;
        }
        
        public int CompareTo(Tag other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            
            return string.Compare(name, other.name, StringComparison.Ordinal);
        }

        public void ExposeData()
        {
            Scribe_Collections.Look(ref requiredItems, nameof(requiredItems));
            Scribe_Values.Look(ref name, nameof(name));
            Scribe_Values.Look(ref uniqueId, nameof(uniqueId));

            var count = requiredItems.RemoveAll(item => item.Def == null);
            if (count != 0) {
                Log.Error($"Attempting to load a null def, have you removed a mod? - Removing item from {name} - ({GetUniqueLoadID()})");
            }
        }

        public string GetUniqueLoadID()
        {
            return "Tag_" + uniqueId;
        }
    }
}