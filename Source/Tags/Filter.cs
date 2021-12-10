using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    public class Filter : IExposable
    {
        private ThingDef forThing;
        
        private HashSet<ThingDef> stuffs;
        private FloatRange allowedHpRange;
        private QualityRange allowedQualities;

        public ThingDef Thing => forThing;
        public HashSet<ThingDef> AllowedStuffs => stuffs;
        public QualityRange QualityRange => allowedQualities;
        public FloatRange HpRange => allowedHpRange;

        public bool Generic => AllowedStuffs.Count == 0;
        public bool SpecificQualityRange => QualityRange != QualityRange.All;
        public bool SpecificHitpointRange => HpRange != FloatRange.ZeroToOne;

        public Filter()
        {
            this.forThing = null;
            this.stuffs = new HashSet<ThingDef>();
            
            this.allowedQualities = QualityRange.All;
            this.allowedHpRange = FloatRange.ZeroToOne;
        }
        public Filter(ThingDef thing)
        {
            this.forThing = thing;
            this.stuffs = new HashSet<ThingDef>();
            
            this.allowedQualities = QualityRange.All;
            this.allowedHpRange = FloatRange.ZeroToOne;
        }

        public void SetQualityRange(QualityRange range)
        {
            this.allowedQualities = range;
        }

        public void SetHpRange(FloatRange range)
        {
            this.allowedHpRange = range;
        }

        public void CopyTo(ThingFilter thingFilter)
        {
            thingFilter.allowedDefs = stuffs.Count == 0 ? GenStuff.AllowedStuffsFor(forThing).ToHashSet() : stuffs.ToHashSet();
        }
        public static Filter CopyFrom(Filter from, Filter to)
        {
            var newFilter = new Filter(to.Thing);
            if (newFilter.Thing.MadeFromStuff && from.Thing.MadeFromStuff)
            {
                var validStuffs = GenStuff.AllowedStuffsFor(newFilter.Thing).ToHashSet();
                var newStuffs = from.AllowedStuffs.Where(allowedStuff => validStuffs.Contains(allowedStuff)).ToList();
                newFilter.AllowedStuffs.AddRange(newStuffs.Any() ? newStuffs : to.AllowedStuffs);
            }
            if (newFilter.Thing.HasComp(typeof(CompQuality)) && from.Thing.HasComp(typeof(CompQuality)))
            {
                newFilter.SetQualityRange(from.QualityRange);
            }
            if (newFilter.Thing.useHitPoints && from.Thing.useHitPoints)
            {
                newFilter.SetHpRange(from.HpRange);
            }

            return newFilter;
        }
        
        public void ExposeData()
        {
            Scribe_Defs.Look(ref forThing, nameof(forThing));
            Scribe_Collections.Look(ref stuffs, nameof(stuffs));
            Scribe_Values.Look(ref allowedHpRange, nameof(allowedHpRange));
            Scribe_Values.Look(ref allowedQualities, nameof(allowedQualities));
            
            var count = stuffs.RemoveWhere(thing => thing is null);
            if (count != 0) {
                Log.Error($"Attempted to load a null stuff from a filter, have you removed a mod?");
            }
        }

        private static QualityCategory GetQuality(Thing thing)
        {
            if ( thing.TryGetQuality(out var quality) )
                return quality;
            
            return QualityCategory.Normal;
        }

        // Very similar to `ThingFilter:Allows`
        public bool Allows(Thing thing)
        {
            thing = thing.GetInnerIfMinified();
            // Check the thing is equal to `forThing`
            if (thing.def != forThing) return false;
            // is it made from the correct stuff
            if (!stuffs.EnumerableNullOrEmpty() && !stuffs.Contains(thing.Stuff)) return false;
            // does it have the correct number of hit points
            if (!allowedHpRange.IncludesEpsilon(Mathf.Clamp01(thing.HitPoints / (float)thing.MaxHitPoints))) return false;
            // does it fall within the quality range?
            if (allowedQualities != QualityRange.All && !allowedQualities.Includes(GetQuality(thing))) return false;

            return true;
        }
    }
}