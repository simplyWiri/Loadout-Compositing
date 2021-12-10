using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Inventory
{
    public class Loadout : IExposable
    {
        public List<Tag> tags;
        public Loadout()
        {
            tags = new List<Tag>();
        }

        public IEnumerable<Item> AllItems => tags.SelectMany(t => t.requiredItems);

        public IEnumerable<Tag> TagsMatching(Predicate<Item> predicate)
        {
            return tags.Where(tag => tag.ItemsMatching(predicate).Any());
        }
        public IEnumerable<Item> ThingsMatching(Predicate<Item> predicate)
        {
            return tags.SelectMany(tag => tag.ItemsMatching(predicate));
        }

        public float WeightAtWhichLoadoutDesires(Thing thing)
        {
            const float priorityMultiplier = 5;
            
            var tag = tags.FirstOrDefault(t => t.ItemsMatching(thing).Any());
            if (tag == null) return 0;
            
            var tagIndex = tags.IndexOf(tag);
            var tagPriority = Mathf.Pow(priorityMultiplier, tags.Count - tagIndex);
            return tagPriority;
        }
        
        public bool Desires(Thing thing)
        {
            return tags.Any(t => t.ItemsMatching(item => item.Def == thing.def && item.Filter.Allows(thing)).Any());
        }

        public IEnumerable<Item> DesiredItems(List<Thing> heldThings)
        {
            var desiredThings = AllItems.Where(t => !t.Def.IsApparel);
            foreach (var thing in desiredThings)
            {
                if (heldThings.Any(t => t.def == thing.Def && thing.Filter.Allows(t) && t.stackCount >= thing.Quantity))
                    continue;
                
                yield return thing;
            }
        }
        
        public void ExposeData()
        {
            Scribe_Collections.Look(ref tags, nameof(tags), LookMode.Reference);
        }
    }
}