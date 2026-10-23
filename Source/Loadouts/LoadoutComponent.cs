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
        private Pawn Pawn => parent as Pawn;
        public Loadout Loadout => loadout;

        public override IEnumerable<Gizmo> CompGetGizmosExtra() {
            if ( Pawn.IsValidLoadoutHolder() && !ModBase.settings.hideGizmo ) {
                yield return new Command_Action {
                    action = () => Loadout.RequiresUpdate(),
                    defaultLabel = Strings.SatisfyLoadoutNow,
                    icon = Textures.HotSwapGizmoTex,
                    disabled = Loadout.NeedsUpdate,
                    disabledReason = Strings.SatisfyLoadoutNowFail(parent),
                    alsoClickIfOtherInGroupClicked = true
                };

                yield return new Command_Action {
                    action = () => Pawn.EnqueueEmptyInventory(),
                    defaultLabel = Strings.ClearInventoryNow,
                    icon = Textures.DropInventoryGizmoTex,
                    alsoClickIfOtherInGroupClicked = true
                };
            }
        }
        
        public void AddTag(Tag tag, LoadoutState state = null, ActiveCondition activeCondition = ActiveCondition.StateActive, bool insertAtStart = true) {
            if (!LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList)) {
                pList = new List<Pawn>();
                LoadoutManager.PawnsWithTags.Add(tag, pList);
            }

            pList.Add(Pawn);
            if (insertAtStart)
            {
                Loadout.elements.Insert(0, new LoadoutElement(tag, state, activeCondition));
            }
            else
            {
                Loadout.elements.Add(new LoadoutElement(tag, state, activeCondition));
            }

            foreach (var def in tag.requiredItems.Where(item => item.IsApparel).SelectMany(item => item.Defs)) {
                if (Pawn.outfits.CurrentApparelPolicy.filter.Allows(def)) continue;
                Messages.Message(Strings.OutfitDisallowsKit(Pawn, Pawn.outfits.CurrentApparelPolicy, def, tag), Pawn, MessageTypeDefOf.CautionInput, false);
                return;
            }
        }
        
        public void AddElement(LoadoutElement elem) {
            if (!LoadoutManager.PawnsWithTags.TryGetValue(elem.Tag, out var pList)) {
                pList = new List<Pawn>();
                LoadoutManager.PawnsWithTags.Add(elem.Tag, pList);
            }

            pList.Add(Pawn);
            Loadout.elements.Add(elem);

            foreach (var def in elem.Tag.requiredItems.Where(item => item.IsApparel).SelectMany(item => item.Defs)) {
                if (Pawn.outfits.CurrentApparelPolicy.filter.Allows(def)) continue;
                Messages.Message(Strings.OutfitDisallowsKit(Pawn, Pawn.outfits.CurrentApparelPolicy, def, elem.Tag), Pawn, MessageTypeDefOf.CautionInput, false);
                return;
            }
        }

        public void RemoveTag(LoadoutElement element) {
            Loadout.elements.Remove(element);

            if (LoadoutManager.PawnsWithTags.TryGetValue(element.Tag, out var pList)) {
                pList.Remove(Pawn);
            }
                    
            Loadout.UpdateState(element, false);
        }

        public override void PostExposeData() {
            Scribe_Deep.Look(ref loadout, nameof(loadout));

            // for adding to an existing save
            loadout ??= new Loadout();

            // Fix prior save-corruption related issues. 
            if (Scribe.mode == LoadSaveMode.PostLoadInit)  {
                loadout.elements.RemoveAll(e => e.Tag == null);   
            }
        }
    }

}