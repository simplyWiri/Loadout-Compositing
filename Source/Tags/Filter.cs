using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // the quality and hit point requirements shared by every member of an `Item`
    public class Filter : IExposable {

        private FloatRange allowedHpRange;
        private QualityRange allowedQualities;

        public QualityRange QualityRange => allowedQualities;
        public FloatRange HpRange => allowedHpRange;

        public bool SpecificQualityRange => QualityRange != QualityRange.All;
        public bool SpecificHitpointRange => HpRange != FloatRange.ZeroToOne;

        public Filter() {
            // todo: right this wrong.
            this.allowedQualities = ModBase.settings?.defaultQualityRange ?? QualityRange.All;
            this.allowedHpRange = ModBase.settings?.defaultHitpoints ?? FloatRange.ZeroToOne;
        }

        public void SetQualityRange(QualityRange range) {
            this.allowedQualities = range;
        }

        public void SetHpRange(FloatRange range) {
            this.allowedHpRange = range;
        }

        public bool SubsetOf(Bill_Production other)
        {
            var quality = other.qualityRange.max >= QualityRange.max && other.qualityRange.min <= QualityRange.min;
            var hp = other.hpRange.max >= HpRange.max && other.hpRange.min <= HpRange.min;

            return quality && hp;
        }

        public void ExposeData() {
            Scribe_Values.Look(ref allowedHpRange, nameof(allowedHpRange));
            Scribe_Values.Look(ref allowedQualities, nameof(allowedQualities));
        }

        private static QualityCategory GetQuality(Thing thing) {
            if (thing.TryGetQuality(out var quality))
                return quality;

            return QualityCategory.Normal;
        }

        // Very similar to `ThingFilter:Allows`
        public bool Allows(Thing thing) {
            thing = thing.GetInnerIfMinified();
            // does it have the correct number of hit points
            if (thing.def.useHitPoints && (!allowedHpRange.IncludesEpsilon(Mathf.Clamp01(thing.HitPoints / (float)thing.MaxHitPoints)))) return false;
            // does it fall within the quality range?
            if (thing.def.HasComp(typeof(CompQuality)) && allowedQualities != QualityRange.All && !allowedQualities.Includes(GetQuality(thing))) return false;

            return true;
        }

    }

}