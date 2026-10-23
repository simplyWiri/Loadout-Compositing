using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Loadout : IExposable {
        public List<LoadoutElement> elements;
        private List<Item> itemsToRemove;
        private LoadoutState currentState;
        private bool needsUpdate = false;

        private bool panicMode = false;
        private LoadoutState priorState = null;

        public IEnumerable<Tag> AllTags => elements.Select(elem => elem.Tag);
        public IEnumerable<Item> AllItems => AllTags.SelectMany(t => t.requiredItems);
        public IEnumerable<Tag> Tags => TagsWith(currentState);

        public bool NeedsUpdate => needsUpdate;
        public bool InPanicMode => panicMode;
        public LoadoutState CurrentState => currentState;
        public List<Item> ThingsToRemove => itemsToRemove;

        public IEnumerable<LoadoutElement> AllElements => elements;
        public IEnumerable<LoadoutElement> Elements => elements.Where(e => e.Active(currentState));
        public IEnumerable<LoadoutElement> ElementsWith(LoadoutState state) => elements.Where(e => e.Active(state));

        public IEnumerable<Tag> TagsWith(LoadoutState state) => elements.Where(e => e.Active(state)).Select(e => e.Tag);
        public IEnumerable<Item> Items => Tags.SelectMany(t => t.requiredItems);
        public IEnumerable<Item> ItemsWith(LoadoutState state) => TagsWith(state).SelectMany(t => t.requiredItems);

        // the items which are worn or held, i.e. everything but prosthetics, which are installed instead
        public IEnumerable<Item> CarriedItems => Items.Where(item => !item.IsProsthetic);
        public IEnumerable<Item> CarriedItemsWith(LoadoutState state) => ItemsWith(state).Where(item => !item.IsProsthetic);

        public Loadout() {
            elements = new List<LoadoutElement>();
            itemsToRemove = new List<Item>();
        }

        public void SetState(LoadoutState state) {
            if ( panicMode ) {
                Messages.Message(Strings.CantOverridePanicMode, MessageTypeDefOf.CautionInput, false);
                return;
            }

            var changedElems = new List<LoadoutElement>();
            foreach (var elem in AllElements) {
                if ( elem.Active(CurrentState) != elem.Active(state)) {
                    changedElems.Add(elem);
                }
            }

            currentState = state;
            
            foreach (var elem in changedElems) {
                UpdateState(elem, elem.Active(state));
            }
        }

        public void UpdateState(LoadoutElement element, bool active) {  
            foreach (var item in element.Tag.requiredItems.Where(item => !item.IsApparel && !item.IsProsthetic)) {
                if (!active) {
                    AddItemToRemove(item);
                }
            }

            if (ModBase.settings.immediatelyResolveLoadout) {
                needsUpdate = true;
            }
        }

        public void RequiresUpdate() {
            needsUpdate = true;
        }
        public void Updated() {
            needsUpdate = false;
        }

        private void AddItemToRemove(Item item) {
            itemsToRemove.Add(item);
        }

        public int IndexOf(Tag tag) {
            return elements.FirstIndexOf(le => le.Tag == tag);
        }

        public float WeightAtWhichLoadoutDesires(Thing thing) {
            const float priorityMultiplier = 5;

            var tag = Tags.FirstOrDefault(t => t.requiredItems.Any(item => item.Allows(thing)));
            if (tag == null) return 0;

            var tagIndex = IndexOf(tag);
            var tagPriority = Mathf.Pow(priorityMultiplier, elements.Count - tagIndex);
            return tagPriority;
        }

        // strict preference between the members of an apparel item: 1 for the best rank, falling towards 1/2 for worse
        // ranks (staying within the tag's priority band), or 0 if a better ranked member of the same item is worn
        public float RankWeight(Pawn pawn, Apparel apparel) {
            var item = ItemsAccepting(apparel).FirstOrDefault(i => i.IsApparel);
            if (item == null || item.Members.Count == 1) return 1f;

            var rank = item.RankOf(apparel);
            foreach (var worn in pawn.apparel.WornApparel) {
                if (worn != apparel && item.Allows(worn) && item.RankOf(worn) < rank) return 0f;
            }

            return 1f - 0.5f * rank / item.Members.Count;
        }

        // which of `pawnGear` counts towards each of the active items, and which is surplus
        public GearAssignment AssignGear(Pawn pawn, List<Thing> pawnGear) {
            return new GearAssignment(pawn, CarriedItems, pawnGear);
        }

        public bool Desires(Thing thing, bool allTags = false) {
            return (allTags ? AllTags : Tags).Any(t => t.ItemsMatching(item => !item.IsProsthetic && item.Allows(thing)).Any());
        }

        public IEnumerable<Item> ItemsAccepting(Thing thing) {
            return Tags.SelectMany(t => t
                .ItemsMatching(item => !item.IsProsthetic && item.Allows(thing)));
        }

        // (item, tag it came from, def chosen to be worn for the item)
        public IEnumerable<Tuple<Item, Tag, ThingDef>> HypotheticalWornApparel(LoadoutState state, BodyDef def) {
            var itemsWithPrios = TagsWith(state).SelectMany(t => t
                    .ItemsWithTagMatching(item => item.IsApparel)
                    .Select(tuple => new Tuple<Item, Tag, int>(tuple.Item2, tuple.Item1, elements.FindIndex(e => e.Tag == tuple.Item1))))
                .ToList();

            var wornApparel = ApparelUtility.WornApparelFor(def, itemsWithPrios) ?? new List<Tuple<Item, Tag, ThingDef>>();
            return wornApparel;
        }

        public IEnumerable<Item> DesiredItems(List<Thing> heldThings) {
            var desiredThings = CarriedItems.Where(t => !t.IsApparel);
            foreach (var thing in desiredThings) {
                var count = heldThings.Where(heldThing => thing.Allows(heldThing))
                    .Sum(heldThing => heldThing.stackCount);

                if (count >= thing.Quantity)
                    continue;

                yield return thing;
            }
        }

        public void ActivatePanicMode(LoadoutState newState) {
            priorState = currentState;
            SetState(newState);

            panicMode = true;
            RequiresUpdate();
        }

        public void DeactivatePanicMode() {
            panicMode = false;
            SetState(priorState);
            priorState = null;
        }

        public void ExposeData() {
            Scribe_Values.Look(ref needsUpdate, nameof(needsUpdate), false);
            Scribe_Collections.Look(ref elements, nameof(elements), LookMode.Deep);
            Scribe_Collections.Look(ref itemsToRemove, nameof(itemsToRemove), LookMode.Deep);
            Scribe_References.Look(ref currentState, nameof(currentState));

            Scribe_Values.Look(ref panicMode, nameof(panicMode));
            Scribe_References.Look(ref priorState, nameof(priorState));

            itemsToRemove = new List<Item>();
            elements ??= new List<LoadoutElement>();
        }
    }

}