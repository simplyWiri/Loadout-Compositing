using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;
using Color = UnityEngine.Color;

namespace Inventory {

    public class LoadoutComponent : ThingComp {

        private Loadout loadout = new Loadout();
        public Loadout Loadout => loadout;

        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            var action = new Command_Action {
                action = () => Loadout.RequiresUpdate(),
                defaultLabel = Strings.SatisfyLoadoutNow,
                icon = Textures.EditTex,
                disabled = Loadout.NeedsUpdate,
                disabledReason = Strings.SatisfyLoadoutNowFail(parent),
                alsoClickIfOtherInGroupClicked = true
            };
            yield return action;
        }

        public override void PostExposeData() {
            Scribe_Deep.Look(ref loadout, nameof(loadout));

            // for adding to an existing save
            loadout ??= new Loadout();
        }
    }

}