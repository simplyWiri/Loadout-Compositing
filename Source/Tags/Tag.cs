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