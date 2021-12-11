using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace Inventory
{
    public class LoadoutComponent : ThingComp
    {
        private Loadout loadout = new Loadout();

        public Loadout Loadout => loadout;
        
        public bool ShouldDropSomething()
        {
            var pawn = parent as Pawn;
            var comp = pawn.GetComp<LoadoutComponent>();
            var items = pawn.InventoryAndEquipment().ToList();
            
            foreach (var heldThing in items)
            {
                var itemsAcceptingThing = comp.Loadout.ItemsAccepting(heldThing).ToList();
                if (!itemsAcceptingThing.Any()) continue;

                var desiredQuantity = itemsAcceptingThing.Sum(item => item.Quantity);
                int currentQuantity = heldThing.stackCount;
            
                foreach (var otherThing in items.Except(heldThing))
                {
                    if (itemsAcceptingThing.Any(item => item.Allows(otherThing))) {
                        currentQuantity += otherThing.stackCount;
                    }
                }
            
                if (currentQuantity > desiredQuantity) {
                    return true;
                }
            }

            return false;
        }

        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref loadout, nameof(loadout));

            // for adding to an existing save
            loadout ??= new Loadout();
        }
    }
}