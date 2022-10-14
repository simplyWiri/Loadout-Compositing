using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // the base type, contains a `ThingDef`
    public class Item : IExposable {
        private SafeDef def;

        // should only be directly accessed in `Dialog_TagEditor.cs`
        internal Filter filter;

        // should only be directly accessed in `Dialog_TagEditor.cs`
        internal int quantity;

        // intentionally temporary + unsaved, only accessed in `Dialog_TagEditor.cs`
        public string quantityStr;

        public Filter Filter => filter;
        public ThingDef Def => def;
        public int Quantity => quantity;

        public SafeDef WrappedDef => def;

        public ThingDef RandomStuff => !Def.MadeFromStuff ? null : filter.AllowedStuffs.Any() ? filter.AllowedStuffs.First() : GenStuff.AllowedStuffsFor(Def).First();
        public QualityCategory RandomQuality => (QualityCategory)Mathf.FloorToInt(((int)filter.QualityRange.max + (int)filter.QualityRange.min) / 2.0f);
        public string Label => Print();

        public bool Allows(Thing thing) => thing.def == Def && Filter.Allows(thing);
        public int CountIn(List<Thing> things) => things.Where(Allows).Sum(s => s.stackCount);

        // Stuff? Def.LabelCap Quality-Range?
        public string Print() {
            var stringBuilder = new StringBuilder();

            if (Def.MadeFromStuff) {
                var count = filter.AllowedStuffs.Count;
                if (count == 0) stringBuilder.Append(Strings.Generic);
                else if (count == 1) stringBuilder.Append(filter.AllowedStuffs.First().Def.LabelCap);
                else stringBuilder.Append(Strings.Generic + "*");
                stringBuilder.Append(" ");
            }

            stringBuilder.Append(Def.LabelCap);
            if (Def.HasComp(typeof(CompQuality))) {
                stringBuilder.Append(" (");

                if (filter.QualityRange.min != filter.QualityRange.max)
                    stringBuilder.Append(filter.QualityRange.ToString());
                else
                    stringBuilder.Append(filter.QualityRange.min.ToString());

                stringBuilder.Append(")");
            }

            return stringBuilder.ToString();
        }

        //public Thing MakeDummyThingNoId() {
        //    return Utility.MakeThingWithoutID(Def, RandomStuff, RandomQuality);
        //}

        public Item() {
            this.def = null;
            this.filter = null;
            this.quantity = 0;
        }

        public Item(ThingDef def) {
            this.def = new SafeDef(def);
            this.filter = new Filter(def);
            this.quantity = 1;
        }

        // exists to make generic `Items` easier
        public IEnumerable<Thing> ThingsOnMap(Map map) {
            var things = map.listerThings.ThingsOfDef(this.def);
            if (things.NullOrEmpty()) yield break;
            foreach (var thing in things.Where(Allows)) {
                yield return thing;
            }
        }
        
        public void SetQuantity(int quantity) {
            this.quantity = quantity;
            if (quantity <= 0) {
                this.quantity = 1;
            }
        }

        public void ExposeData() {
            Scribe_Values.Look(ref def, nameof(def));
            Scribe_Deep.Look(ref filter, nameof(filter));
            Scribe_Values.Look(ref quantity, nameof(quantity));
        }

    }

}