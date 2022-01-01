using System.Text;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Settings : ModSettings {

        public bool immediatelyResolveLoadout = false;

        public void DoSettingsWindow(Rect rect) {
            var tRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.CheckboxLabeled(tRect, Strings.ImmediatelyResolveLoadout, ref immediatelyResolveLoadout);
            TooltipHandler.TipRegion(tRect, Strings.ImmediatelyResolveLoadoutDesc);
        }

        public override void ExposeData() {
            Scribe_Values.Look(ref immediatelyResolveLoadout, nameof(immediatelyResolveLoadout), false);
        }

    }

}