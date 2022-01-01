using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Panel_ShowCoverage {

        private Dialog_LoadoutEditor parent;
        private Vector2 apparelSlotsScroll = Vector2.zero;
        private float apparelSlotsHeight = 9999f;
        private Pawn pawn;
        private LoadoutComponent component;
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

        public Panel_ShowCoverage(Dialog_LoadoutEditor parent, bool shouldDraw = false) {
            this.parent = parent;
            this.pawn = parent.pawn;
            this.ShouldDraw = shouldDraw;
            this.component = parent.component;
        }

        public void Draw(Rect rect) {
            DrawApparelSlots(rect, pawn.RaceProps.body);
            DrawPrelude(rect);
        }
        
        public void DrawPrelude(Rect rect) {
            var bRect = rect.TopPartPixels(UIC.SMALL_ICON);
            bRect = bRect.RightPartPixels(UIC.SMALL_ICON);

            Widgets.ButtonImageFitted(bRect, TexButton.Info);
            TooltipHandler.TipRegion(bRect.ExpandedBy(5f), () => Strings.CoverageExplanation, 489588371);
        }

        public void DrawApparelSlots(Rect rect, BodyDef def) {
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, apparelSlotsHeight);
            Widgets.BeginScrollView(rect, ref apparelSlotsScroll, viewRect);

            float curY = rect.y + UIC.DEFAULT_HEIGHT;
            float beginY = curY;

            var wornApparel = component.Loadout.HypotheticalWornApparelWithTag(parent.shownState, def).ToList();

            foreach (var category in ApparelUtility.GetBodyPartGroupFor(def).GetCategories()
                         .OrderByDescending(t => t.First().def.listOrder)) {
                var layers = category.SelectMany(c => c.GetLayers()).Distinct().OrderByDescending(d => d.drawOrder)
                    .ToList();
                var cols = DrawHeader(new Rect(viewRect.x, curY, viewRect.width, UIC.SPACED_HEIGHT), layers, category,
                    curY).ToList();

                curY += UIC.SPACED_HEIGHT + UIC.DEFAULT_HEIGHT;

                // should prevent labels from being cut off on both x and y axis. Extends the window width if the
                // required summed width of the strings/rects does not fit in the current rect we have been given.
                var sumWidth = layers.Sum(layer => layer.LabelCap.GetWidthCached() + 5);
                var rectWidth = cols.Sum(rect => rect.width);
                var maxWidth = Mathf.Max(sumWidth, rectWidth);
                if (maxWidth > rect.width) {
                    var extraWidth = maxWidth + UIC.SCROLL_WIDTH * 2;
                    parent.windowRect.width = Mathf.Max(parent.windowRect.width, parent.InitialSize.x + extraWidth);
                }

                for (int i = 0; i < cols.Count; i++) {
                    var col = cols[i];
                    col.x = Mathf.Floor(col.x);
                    col.y = Mathf.Floor(col.y);
                    col.width = Mathf.Floor(col.width);
                    col.height = Mathf.Floor(col.height);
                    if (i == cols.Count - 1) continue;

                    var next = cols[i + 1];
                    next.x = (col.x + col.width) + 1;
                }

                foreach (var bp in category) {
                    DrawBodyPartGroup(def, bp, layers, ref curY, cols, wornApparel);
                }

                curY += UIC.DEFAULT_HEIGHT;
            }

            apparelSlotsHeight = (curY - beginY) + UIC.DEFAULT_HEIGHT;

            Widgets.EndScrollView();
        }

        public static IEnumerable<Rect> DrawHeader(Rect rect, List<ApparelLayerDef> layers,
            List<BodyPartGroup> category, float curY) {
            var longestName = category.Max(c => c.def.LabelCap.GetWidthCached() + 15f);
            if (longestName < ("Body Group".GetWidthCached() + 15f)) {
                longestName = "Body Group".GetWidthCached() + 15f;
            }

            string GetHeader(int i) {
                return i == 0 ? "Body Group" : layers[i - 1].LabelCap;
            }

            var numCols = layers.Count + 1;
            var width = Mathf.Max(longestName, rect.width / numCols);
            var curX = rect.x;

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect.RightPart(.70f), "Apparel Layers");
            curY += UIC.DEFAULT_HEIGHT;
            Text.Anchor = TextAnchor.UpperLeft;

            for (int i = 0; i < numCols; i++) {
                var headerRect = new Rect(curX, curY, width, UIC.SPACED_HEIGHT);

                if (i != 0) {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.gray;
                }

                Widgets.Label(headerRect, GetHeader(i));
                Text.Anchor = TextAnchor.UpperLeft;

                yield return new Rect(curX, curY, width, rect.height - UIC.SPACED_HEIGHT);

                curX += headerRect.width;
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public void DrawBodyPartGroup(BodyDef bodyDef, BodyPartGroup group, List<ApparelLayerDef> layers,
            ref float curY, List<Rect> columns, List<Tuple<Item, Tag>> wornApparel) {
            var groupRect = new Rect(columns[0].x, curY, columns[0].width, UIC.SPACED_HEIGHT);

            var def = group.def;

            GUI.color = Color.gray;
            Widgets.Label(groupRect, def.LabelCap);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;

            foreach (var column in group.layers.OrderByDescending(d => d.drawOrder)) {
                var idx = layers.FirstIndexOf(c => c == column) + 1;
                var columnRect = new Rect(columns[idx].x, curY, columns[idx].width, UIC.SPACED_HEIGHT);
                var loadoutTags = component.Loadout.ElementsWith(parent.shownState).ToList();

                var (overlappingApparel, overlappingTag) = wornApparel
                                                               .FirstOrDefault(item =>
                                                                   ApparelSlotMaker.Create(bodyDef, item.Item1.Def)
                                                                       .Intersects(def, column)) ??
                                                           new Tuple<Item, Tag>(null, null);

                var possibleDefs = loadoutTags
                    .SelectMany(t =>
                        t.Tag.ItemsWithTagMatching(item =>
                            item.Def.IsApparel && ApparelSlotMaker.Create(bodyDef, item.Def).Intersects(def, column)))
                    .ToList();

                if (overlappingApparel != null) {
                    possibleDefs.RemoveAll(tup => tup.Item1 == overlappingTag);
                }

                if (overlappingApparel == null && possibleDefs.EnumerableNullOrEmpty()) {
                    Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.gray);
                }
                else {
                    if (overlappingApparel != null) {
                        var col = GetColorForTagAtIndex(component.Loadout.AllElements.FirstIndexOf(e => e.Tag == overlappingTag));
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, col);
                        Widgets.DefIcon(columnRect.ContractedBy(3f), overlappingApparel.Def,
                            overlappingApparel.RandomStuff);

                        if (possibleDefs.Any()) {
                            var str = "";
                            foreach (var (pTag, pApparel) in possibleDefs) {
                                str += $"\n - {pApparel.Def.LabelCap} from {pTag.name}";
                            }

                            TooltipHandler.TipRegion(columnRect,
                                $"{overlappingApparel.Def.LabelCap} from {overlappingTag.name} blocks:{str}");
                        }
                    }
                    else {
                        var nextHighestPrio = possibleDefs.OrderBy(tup => component.Loadout.ElementsWith(parent.shownState).FirstIndexOf(e => e.Tag == overlappingTag)).First().Item2;
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.red);
                        Widgets.DefIcon(columnRect.ContractedBy(3f), nextHighestPrio.Def, color: new Color(0.5f, 0.5f, 0.5f, 0.3f));

                        var str = "Blocked Tags:";
                        foreach (var (pTag, pApparel) in possibleDefs) {
                            str += $"\n - {pApparel.Def.LabelCap} from {pTag.name}";
                        }

                        TooltipHandler.TipRegion(columnRect, str);
                    }
                }
            }

            Text.Font = GameFont.Small;

            curY += UIC.SPACED_HEIGHT;

            foreach (var child in group.children) {
                DrawBodyPartGroup(bodyDef, child, layers, ref curY, columns, wornApparel);
            }
        }

    }

}