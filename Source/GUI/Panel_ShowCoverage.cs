using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Panel_ShowCoverage {

        public enum CoverageTab { Apparel, BodyParts }

        // the least room the panel is given; the apparel grid widens the window further when its columns need it
        public const float MIN_WIDTH = 480f;

        private Dialog_LoadoutEditor parent;
        private Pawn pawn;
        private ApparelCoverage apparelCoverage;
        private BodyPartCoverage bodyPartCoverage;
        private static List<Color> coloursByIdx = null;

        public bool ShouldDraw { get; set; } = false;
        public CoverageTab Tab { get; set; } = CoverageTab.Apparel;

        static Panel_ShowCoverage() {
            Color HexColor(int hex) {
                return new Color {
                    r = ((hex >> 16) & 0xFF) / 255.0f,
                    g = ((hex >> 8) & 0xFF) / 255.0f,
                    b = ((hex) & 0xFF) / 255.0f,
                    a = 1
                };
            }
            
            coloursByIdx = new List<Color>() {
                HexColor(0x424C4C),
                HexColor(0x458EAB),
                HexColor(0x38B4BC),
                HexColor(0x2DD277),
                HexColor(0xDFD477),
                HexColor(0xE9B081),
                HexColor(0xCF636C),
                HexColor(0x93254B),
                HexColor(0xBF2290),
                HexColor(0xB46A8B)
            };
            
        }

        public static Color GetColorForTagAtIndex(int index) {
            return coloursByIdx[index % 10];
        }

        // the same colour for a tag wherever it is shown, unlike its index in a pawn's tags
        public static Color GetColorForTag(Tag tag) {
            return GetColorForTagAtIndex(Mathf.Max(0, LoadoutManager.Tags.IndexOf(tag)));
        }

        public Panel_ShowCoverage(Dialog_LoadoutEditor parent, bool shouldDraw = false) {
            this.parent = parent;
            this.pawn = parent.pawn;
            this.ShouldDraw = shouldDraw;
            this.apparelCoverage = new ApparelCoverage(parent);
            this.bodyPartCoverage = new BodyPartCoverage(parent);
        }

        public void Draw(Rect rect) {
            DrawInfoButton(rect.PopTopPartPixels(TabDrawer.TabHeight));

            var tabs = new List<TabRecord> {
                new TabRecord(Strings.ApparelTab, () => Tab = CoverageTab.Apparel, Tab == CoverageTab.Apparel),
                new TabRecord(Strings.BodyPartsTab, () => Tab = CoverageTab.BodyParts, Tab == CoverageTab.BodyParts),
            };

            Widgets.DrawMenuSection(rect);
            TabDrawer.DrawTabs(rect, tabs);

            var contents = rect.ContractedBy(GenUI.GapSmall);
            if (Tab == CoverageTab.Apparel) {
                apparelCoverage.Draw(contents, pawn.RaceProps.body);
            } else {
                bodyPartCoverage.Draw(contents, pawn.RaceProps.body);
            }
        }

        private void DrawInfoButton(Rect tabRow) {
            var bRect = tabRow.RightPartPixels(UIC.SMALL_ICON).MiddlePartPixels(UIC.SMALL_ICON);

            Widgets.ButtonImageFitted(bRect, TexButton.Info);
            TooltipHandler.TipRegion(bRect.ExpandedBy(5f), () => Tab == CoverageTab.Apparel ? Strings.CoverageExplanation : Strings.BodyPartCoverageExplanation, 489588371);
        }

    }

}