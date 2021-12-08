using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
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
            this.name = string.Empty;
            this.requiredItems = new List<Item>();
        }
        public Tag(string name)
        {
            this.name = name;
            this.requiredItems = new List<Item>();
            this.uniqueId = LoadoutManager.GetNextTagId();
        }

        public IEnumerable<Item> ItemsMatching(Predicate<Item> pred)
        {
            return requiredItems.Where(item => pred(item));
        }

        public void Add(ThingDef thing)
        {
            requiredItems.Add(new Item(thing));
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
        }

        public string GetUniqueLoadID()
        {
            return "Tag_" + uniqueId;
        }
    }
}