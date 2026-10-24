using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Panel_ShowCoverage {

        private Dialog_LoadoutEditor parent;
        private Pawn pawn;
        private ApparelCoverage apparelCoverage;
        private static List<Color> coloursByIdx = null;

        public bool ShouldDraw { get; set; } = false;

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
        }

        public void Draw(Rect rect) {
            apparelCoverage.Draw(rect, pawn.RaceProps.body);
            DrawPrelude(rect);
        }
        
        public void DrawPrelude(Rect rect) {
            var bRect = rect.TopPartPixels(UIC.SMALL_ICON);
            bRect = bRect.RightPartPixels(UIC.SMALL_ICON);

            Widgets.ButtonImageFitted(bRect, TexButton.Info);
            TooltipHandler.TipRegion(bRect.ExpandedBy(5f), () => Strings.CoverageExplanation, 489588371);
        }

    }

}