using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
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

            Loadout.TagsChanged();
            WarnAboutProsthetics(tag);

            foreach (var def in tag.requiredItems.Where(item => item.IsApparel).SelectMany(item => item.Defs)) {
                if (Pawn.outfits.CurrentApparelPolicy.filter.Allows(def)) continue;
                Messages.Message(Strings.OutfitDisallowsKit(Pawn, Pawn.outfits.CurrentApparelPolicy, def, tag), Pawn, MessageTypeDefOf.CautionInput, false);
                return;
            }
        }
        
        public void AddElement(LoadoutElement elem) {
            InsertElement(elem, Loadout.elements.Count);
        }

        // adds `elem` at `index` in the pawn's tags, the earlier a tag is the higher its priority
        public void InsertElement(LoadoutElement elem, int index) {
            if (!LoadoutManager.PawnsWithTags.TryGetValue(elem.Tag, out var pList)) {
                pList = new List<Pawn>();
                LoadoutManager.PawnsWithTags.Add(elem.Tag, pList);
            }

            pList.Add(Pawn);
            Loadout.elements.Insert(Mathf.Clamp(index, 0, Loadout.elements.Count), elem);

            Loadout.TagsChanged();
            WarnAboutProsthetics(elem.Tag);

            foreach (var def in elem.Tag.requiredItems.Where(item => item.IsApparel).SelectMany(item => item.Defs)) {
                if (Pawn.outfits.CurrentApparelPolicy.filter.Allows(def)) continue;
                Messages.Message(Strings.OutfitDisallowsKit(Pawn, Pawn.outfits.CurrentApparelPolicy, def, elem.Tag), Pawn, MessageTypeDefOf.CautionInput, false);
                return;
            }
        }

        // warns about the prosthetics `tag` asks for (only `def`, if given) which this pawn objects to, can never have,
        // or which can't go alongside the prosthetics of their other active tags
        public void WarnAboutProsthetics(Tag tag, ThingDef def = null) {
            var items = tag.requiredItems.Where(item => item.IsProsthetic && (def == null || item.RepresentativeDef == def)).ToList();
            // dead pawns remain tagged in case they are resurrected, but there's no need to warn about them
            if (items.Count == 0 || Pawn.Dead) return;

            void Warn(string text) => Messages.Message(text, Pawn, MessageTypeDefOf.CautionInput, false);

            var objection = ProstheticUtility.Objection(Pawn);
            if (objection != null) {
                Warn(Strings.ObjectsToProsthetics(Pawn, objection, tag));
            }

            foreach (var item in items.Where(item => !ProstheticUtility.CanEverInstall(Pawn, item.RepresentativeDef))) {
                Warn(Strings.CantEverInstall(Pawn, item.RepresentativeDef, tag));
            }

            // the tag counts as active, even if it isn't in the current state
            var elements = Loadout.elements.Where(e => e.Tag == tag || e.Active(Loadout.CurrentState)).ToList();
            var slots = new ProstheticSlots(Pawn.RaceProps.body, elements.SelectMany(e => e.Tag.ItemsWithTagMatching(item => item.IsProsthetic)));
            foreach (var element in elements) {
                foreach (var item in element.Tag.requiredItems) {
                    if (!slots.Conflicts.TryGetValue(item, out var blocker)) continue;
                    // conflicts this tag suffers, or causes
                    if (!items.Contains(item) && blocker != tag) continue;

                    Warn(blocker == null
                        ? Strings.NoRoomForProsthetic(Pawn, item.RepresentativeDef, element.Tag)
                        : Strings.ProstheticConflictsWith(Pawn, item.RepresentativeDef, element.Tag, blocker));
                }
            }
        }

        public void RemoveTag(LoadoutElement element) {
            Loadout.elements.Remove(element);
            Loadout.TagsChanged();

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