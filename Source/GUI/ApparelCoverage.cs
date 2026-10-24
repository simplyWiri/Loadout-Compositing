using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // The apparel half of the coverage panel: a grid of the body part groups of a pawn against the apparel layers,
    // showing which of the loadout's apparel would be worn in each slot, and what it blocks
    public class ApparelCoverage {

        private Dialog_LoadoutEditor parent;
        private Vector2 apparelSlotsScroll = Vector2.zero;
        private float apparelSlotsHeight = 9999f;
        private LoadoutComponent component;

        public ApparelCoverage(Dialog_LoadoutEditor parent) {
            this.parent = parent;
            this.component = parent.component;
        }

        public void Draw(Rect rect, BodyDef def) {
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, apparelSlotsHeight);
            Widgets.BeginScrollView(rect, ref apparelSlotsScroll, viewRect);

            float curY = viewRect.y;

            var wornApparel = component.Loadout.HypotheticalWornApparel(parent.shownState, def).ToList();

            foreach (var category in ApparelUtility.GetBodyPartGroupFor(def).categories
                         .OrderByDescending(t => t.First().def.listOrder)) {
                var layers = category.SelectMany(c => c.GetLayers()).Distinct().OrderByDescending(d => d.drawOrder)
                    .ToList();
                var cols = DrawHeader(new Rect(viewRect.x, curY, viewRect.width, UIC.SPACED_HEIGHT), layers, category);

                curY += UIC.SPACED_HEIGHT;

                // widen the window when the columns don't fit, so no labels or slots are cut off
                var overflow = cols.Last().xMax - viewRect.xMax;
                if (overflow > 0) {
                    parent.windowRect.width += Mathf.Ceil(overflow);
                }

                foreach (var bp in category) {
                    DrawBodyPartGroup(def, bp, layers, ref curY, cols, wornApparel);
                }

                curY += UIC.DEFAULT_HEIGHT;
            }

            apparelSlotsHeight = curY - viewRect.y;

            Widgets.EndScrollView();
        }

        // draws the column headings (body group, then each layer), returning the columns. Columns are whole pixels
        // wide, so the slots' outlines are drawn crisply, and wide enough for the longest label
        public static List<Rect> DrawHeader(Rect rect, List<ApparelLayerDef> layers, List<BodyPartGroup> category) {
            var headers = new List<string> { Strings.BodyGroup };
            headers.AddRange(layers.Select(layer => layer.LabelCap.ToString()));

            var longestLabel = category.Select(c => c.def.LabelCap.ToString()).Concat(headers).Max(label => label.GetWidthCached());
            var width = Mathf.Floor(Mathf.Max(longestLabel + UIC.SMALL_GAP, rect.width / headers.Count));
            var x = Mathf.Floor(rect.x);

            var cols = new List<Rect>();
            for (int i = 0; i < headers.Count; i++) {
                var headerRect = new Rect(x + i * width, rect.y, width, UIC.SPACED_HEIGHT);
                GUIUtility.WithModifiers(headerRect, headers[i], color: Color.gray, anchor: i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter);
                cols.Add(headerRect);
            }

            return cols;
        }

        public void DrawBodyPartGroup(BodyDef bodyDef, BodyPartGroup group, List<ApparelLayerDef> layers,
            ref float curY, List<Rect> columns, List<Tuple<Item, Tag, ThingDef>> wornApparel) {
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

                var (overlappingApparel, overlappingTag, overlappingDef) = wornApparel
                                                               .FirstOrDefault(item =>
                                                                   ApparelSlotMaker.Create(bodyDef, item.Item3)
                                                                       .Intersects(def, column)) ??
                                                           new Tuple<Item, Tag, ThingDef>(null, null, null);

                var possibleDefs = loadoutTags
                    .SelectMany(t =>
                        t.Tag.ItemsWithTagMatching(item =>
                            item.IsApparel && item.Defs.Any(d => ApparelSlotMaker.Create(bodyDef, d).Intersects(def, column))))
                    .ToList();

                if (overlappingApparel != null) {
                    possibleDefs.RemoveAll(tup => tup.Item1 == overlappingTag);
                }

                if (overlappingApparel == null && possibleDefs.EnumerableNullOrEmpty()) {
                    Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.gray);
                }
                else {
                    if (overlappingApparel != null) {
                        var col = Panel_ShowCoverage.GetColorForTagAtIndex(component.Loadout.AllElements.FirstIndexOf(e => e.Tag == overlappingTag));
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.gray);
                        GUI.color = col;
                        GUI.DrawTexture(columnRect.LeftPartPixels(columnRect.height).ContractedBy(1f), Textures.CornerTex);
                        GUI.color = Color.white;
                        
                        GUIUtility.DrawItemIcon(columnRect.ContractedBy(3f), overlappingApparel, false, string.Empty, overlappingApparel.MemberFor(overlappingDef));

                        if (possibleDefs.Any()) {
                            var str = "";
                            foreach (var (pTag, pApparel) in possibleDefs) {
                                str += $"\n - {pApparel.ShortLabel} from {pTag.name}";
                            }

                            TooltipHandler.TipRegion(columnRect,
                                $"{overlappingDef.LabelCap} from {overlappingTag.name} blocks:{str}");
                        }
                    }
                    else {
                        var nextHighestPrio = possibleDefs.OrderBy(tup => component.Loadout.ElementsWith(parent.shownState).FirstIndexOf(e => e.Tag == overlappingTag)).First().Item2;
                        
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.red);
                        Widgets.DefIcon(columnRect.ContractedBy(3f), nextHighestPrio.RepresentativeDef, color: new Color(0.5f, 0.5f, 0.5f, 0.3f));

                        var str = "Blocked Tags:";
                        foreach (var (pTag, pApparel) in possibleDefs) {
                            str += $"\n - {pApparel.ShortLabel} from {pTag.name}";
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
