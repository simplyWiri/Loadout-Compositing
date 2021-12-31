using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace Inventory {

    public class LoadoutComponent : ThingComp {

        private Loadout loadout = new Loadout();
        public Loadout Loadout => loadout;

        public override void PostExposeData() {
            Scribe_Deep.Look(ref loadout, nameof(loadout));

            // for adding to an existing save
            loadout ??= new Loadout();
        }
    }

}