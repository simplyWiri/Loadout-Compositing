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

                yield return new Command_AddTag();
                yield return new Command_RemoveTag();
            }
        }
        
        public void AddTag(Tag tag) {
            if (!LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList)) {
                pList = new SerializablePawnList(new List<Pawn>());
                LoadoutManager.PawnsWithTags.Add(tag, pList);
            }

            pList.pawns.Add(Pawn);
            Loadout.elements.Add(new LoadoutElement(tag, null));

            foreach (var item in tag.requiredItems.Where(item => item.Def.IsApparel)) {
                if (Pawn.outfits.CurrentOutfit.filter.Allows(item.Def)) continue;
                
                Messages.Message(Strings.OutfitDisallowsKit(Pawn, Pawn.outfits.CurrentOutfit, item.Def, tag), Pawn, MessageTypeDefOf.CautionInput, false);
                return;
            }
        }

        public void RemoveTag(LoadoutElement element) {
            Loadout.elements.Remove(element);

            if (LoadoutManager.PawnsWithTags.TryGetValue(element.Tag, out var pList)) {
                pList.pawns.Remove(Pawn);
            }
                    
            Loadout.UpdateState(element, false);
        }

        public void RemoveTag(Tag tag) {
            var element = Loadout.elements.FirstOrDefault(element => element.Tag == tag);
            if (element == null) return;
            RemoveTag(element);
        }

        public bool HasTag(Tag tag) {
            if (!LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList)) return false;
            return pList.pawns.Contains(Pawn);
        }

        public List<Tag> Tags {
            get {
                var result = new List<Tag>();
                LoadoutManager.Tags.ForEach(tag => {
                    if (!HasTag(tag)) return;
                    result.Add(tag);
                });
                return result;
            }
        }
           

        public override void PostExposeData() {
            Scribe_Deep.Look(ref loadout, nameof(loadout));

            // for adding to an existing save
            loadout ??= new Loadout();
        }
    }

}
