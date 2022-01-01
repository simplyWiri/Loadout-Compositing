using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Settings : ModSettings {

        public bool immediatelyResolveLoadout = false;
        public FloatRange defaultHitpoints = FloatRange.ZeroToOne;
        public QualityRange defaultQualityRange = QualityRange.All;

        public void DoSettingsWindow(Rect rect) {
            var tRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.CheckboxLabeled(tRect, Strings.ImmediatelyResolveLoadout, ref immediatelyResolveLoadout);
            TooltipHandler.TipRegion(tRect, Strings.ImmediatelyResolveLoadoutDesc);


            var strRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            Widgets.Label(strRect, Strings.ChangeDefaults);
            
            var defaultHpRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.FloatRange(defaultHpRect, Rand.Int, ref defaultHitpoints, 0f, 1f, Strings.HitPointsAmount, ToStringStyle.PercentZero);
            
            var qualRangeRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.QualityRange(qualRangeRect, Rand.Int, ref defaultQualityRange);
        }

        public override void ExposeData() {
            Scribe_Values.Look(ref immediatelyResolveLoadout, nameof(immediatelyResolveLoadout), false);
            Scribe_Values.Look(ref defaultHitpoints, nameof(defaultHitpoints), FloatRange.ZeroToOne);
            Scribe_Values.Look(ref defaultQualityRange, nameof(defaultQualityRange), QualityRange.All);
        }

    }

}