using System.Collections;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    // the base type, contains a `ThingDef`
    public class Item : IExposable
    {
        private ThingDef def;
        // should only be directly accessed in `Dialog_TagEditor.cs`
        internal Filter filter;
        private int quantity;
        // intentionally temporary + unsaved, only accessed in `Dialog_TagEditor.cs`
        public string quantityStr;

        public Filter Filter => filter;
        public ThingDef Def => def;
        public int Quantity => quantity;
        
        public ThingDef RandomStuff => !Def.MadeFromStuff ? null : filter.AllowedStuffs.Any() ? filter.AllowedStuffs.First() : GenStuff.AllowedStuffsFor(Def).First();
        public QualityCategory RandomQuality => (QualityCategory)Mathf.FloorToInt(((int)filter.QualityRange.max + (int)filter.QualityRange.min)/2.0f);
        public string Label => Print();
        
        // Stuff? Def.LabelCap Quality-Range
        public string Print() {
            var stringBuilder = new StringBuilder();

            if (Def.MadeFromStuff)
            {
                var count = filter.AllowedStuffs.Count;
                if (count == 0) stringBuilder.Append("Generic");
                else if (count == 1) stringBuilder.Append(filter.AllowedStuffs.First().LabelCap);
                else stringBuilder.Append("Generic*");
                stringBuilder.Append(" ");
            }

            stringBuilder.Append(Def.LabelCap);
            if (Def.HasComp(typeof(CompQuality)))
            {
                stringBuilder.Append(" (");

                if (filter.QualityRange.min != filter.QualityRange.max)
                    stringBuilder.Append(filter.QualityRange.ToString());
                else
                    stringBuilder.Append(filter.QualityRange.min.ToString());

                stringBuilder.Append(")");
            }

            return stringBuilder.ToString();
        }

        public Thing MakeDummyThingNoId()
        {
            return Utility.MakeThingWithoutID(Def, RandomStuff, RandomQuality);
        }

        public Item()
        {
            this.def = null;
            this.filter = null;
            this.quantity = 0;
        }
        public Item(ThingDef def)
        {
            this.def = def;
            this.filter = new Filter(def);
            this.quantity = 1;
        }

        public void SetQuantity(int quantity)
        {
            this.quantity = quantity;
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, nameof(def));
            Scribe_Deep.Look(ref filter, nameof(filter));
            Scribe_Values.Look(ref quantity, nameof(quantity));
        }
    }
}