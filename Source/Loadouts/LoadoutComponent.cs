using System.Collections.Generic;
using JetBrains.Annotations;
using Verse;

namespace Inventory
{
    public class LoadoutComponent : ThingComp
    {
        private Loadout loadout = new Loadout();

        public Loadout Loadout => loadout;

        public override void PostExposeData()
        {
            Scribe_Deep.Look(ref loadout, nameof(loadout));

            // for adding to an existing save
            loadout ??= new Loadout();
        }
    }
}