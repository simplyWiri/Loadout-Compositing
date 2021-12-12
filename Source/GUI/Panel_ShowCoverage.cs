using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    public class Panel_ShowCoverage
    {
        private Dialog_LoadoutEditor parent;
        private Vector2 apparelSlotsScroll = Vector2.zero;
        private float apparelSlotsHeight = 9999f;
        private Pawn pawn;
        private LoadoutComponent component;

        public static List<Color> coloursByIdx = null;

        static Panel_ShowCoverage()
        {
            coloursByIdx = new List<Color>()
            {
                new Color(231/255.0f, 111/255.0f, 81/255.0f),
                new Color(244/255.0f, 162/255.0f, 97/255.0f),
                new Color(233/255.0f, 196/255.0f, 106/255.0f),
                new Color(42/255.0f, 157/255.0f, 143/255.0f),
                new Color(38/255.0f, 70/255.0f, 83/255.0f),
                Color.blue,
                Color.magenta,
                Color.white,
                Color.yellow,
                Color.cyan
            };
        }

        public static Color GetColorForTagAtIndex(int index)
        {
            return Panel_ShowCoverage.coloursByIdx[index % 10];
        }
        
        public Panel_ShowCoverage(Dialog_LoadoutEditor parent)
        {
            this.parent = parent;
            this.pawn = parent.pawn;
            this.component = parent.component;
        }

        public void Draw(Rect rect)
        {
            DrawApparelSlots(rect, pawn.RaceProps.body);
        }
        
        public void DrawApparelSlots(Rect rect, BodyDef def)
        {
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, apparelSlotsHeight);
            Widgets.BeginScrollView(rect, ref apparelSlotsScroll, viewRect);
            
            float curY = rect.y + GUIUtility.DEFAULT_HEIGHT;
            float beginY = curY;

            var wornApparel = component.Loadout.HypotheticalWornApparelWithTag(def).ToList();

            foreach (var category in ApparelUtility.GetBodyPartGroupFor(def).GetCategories().OrderByDescending(t => t.First().def.listOrder))
            {
                var layers = category.SelectMany(c => c.GetLayers()).Distinct().OrderByDescending(d => d.drawOrder).ToList();
                var cols = DrawHeader(new Rect(viewRect.x, curY, viewRect.width, GUIUtility.SPACED_HEIGHT), layers, category, curY).ToList();

                curY += GUIUtility.SPACED_HEIGHT + GUIUtility.DEFAULT_HEIGHT;

                // should prevent labels from being cut off on both x and y axis. Extends the window width if the
                // required summed width of the strings/rects does not fit in the current rect we have been given.
                var sumWidth = layers.Sum(layer => layer.LabelCap.GetWidthCached() + 5);
                var rectWidth = cols.Sum(rect => rect.width);
                var maxWidth = Mathf.Max(sumWidth, rectWidth);
                if (maxWidth > rect.width) {
                    var extraWidth = maxWidth + 32f;
                    parent.windowRect.width = Mathf.Max(parent.windowRect.width, (420 + (parent.drawPawnStats ? 210 : 0)) + extraWidth); // 16f for the scroll wheel
                }

                for (int i = 0; i < cols.Count; i++)
                {
                    var col = cols[i];
                    col.x = Mathf.Floor(col.x);
                    col.y = Mathf.Floor(col.y);
                    col.width = Mathf.Floor(col.width);
                    col.height = Mathf.Floor(col.height);
                    if (i == cols.Count - 1) continue;
                    
                    var next = cols[i + 1];
                    next.x = (col.x + col.width) + 1;
                }

                foreach (var bp in category)
                {
                    DrawBodyPartGroup(def, bp, layers, ref curY, cols, wornApparel);
                }
                
                curY += GUIUtility.DEFAULT_HEIGHT;
            }

            apparelSlotsHeight = (curY - beginY) + GUIUtility.DEFAULT_HEIGHT;
            
            Widgets.EndScrollView();
        }
        public static IEnumerable<Rect> DrawHeader(Rect rect, List<ApparelLayerDef> layers, List<BodyPartGroup> category, float curY)
        {
            var longestName = category.Max(c => c.def.LabelCap.GetWidthCached() + 15f);
            if (longestName < ("Body Group".GetWidthCached() + 15f)) {
                longestName = "Body Group".GetWidthCached() + 15f;
            }
            
            string GetHeader(int i)
            {
                return i == 0 ? "Body Group" : layers[i - 1].LabelCap; 
            }
            
            var numCols = layers.Count + 1;
            var width = Mathf.Max(longestName, rect.width / numCols);
            var curX = rect.x;
            
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect.RightPart(.70f), "Apparel Layers");
            curY += GUIUtility.DEFAULT_HEIGHT;
            Text.Anchor = TextAnchor.UpperLeft;
 
            for (int i = 0; i < numCols; i++)
            {
                var headerRect = new Rect(curX, curY, width, GUIUtility.SPACED_HEIGHT);

                if (i != 0) {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    GUI.color = Color.gray;
                }

                Widgets.Label(headerRect, GetHeader(i));
                Text.Anchor = TextAnchor.UpperLeft;
                
                yield return new Rect(curX, curY, width, rect.height - GUIUtility.SPACED_HEIGHT);
 
                curX += headerRect.width;
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
        
        public void DrawBodyPartGroup(BodyDef bodyDef, BodyPartGroup group, List<ApparelLayerDef> layers, ref float curY, List<Rect> columns, List<Tuple<Item, Tag>> wornApparel)
        {
            var groupRect = new Rect(columns[0].x, curY, columns[0].width, GUIUtility.SPACED_HEIGHT);
 
            var def = group.def;
 
            GUI.color = Color.gray;
            Widgets.Label(groupRect, def.LabelCap);
            GUI.color = Color.white;

            Text.Font = GameFont.Tiny;

            foreach (var column in group.layers.OrderByDescending(d => d.drawOrder))
            {
                var idx = layers.FirstIndexOf(c => c == column) + 1;
                var columnRect = new Rect(columns[idx].x, curY, columns[idx].width, GUIUtility.SPACED_HEIGHT);
                var loadoutTags = component.Loadout.tags;

                var (overlappingApparel, overlappingTag) = wornApparel
                    .FirstOrDefault(item => new ApparelSlots(bodyDef, item.Item1.Def).Intersects(def, column)) ?? new Tuple<Item, Tag>(null, null);

                var possibleDefs = loadoutTags
                        .SelectMany(t => 
                            t.ItemsWithTagMatching(item => item.Def.IsApparel && new ApparelSlots(bodyDef, item.Def).Intersects(def, column)))
                        .ToList();

                if (overlappingApparel != null) {
                    possibleDefs.RemoveAll(tup => tup.Item1 == overlappingTag);
                }
                
                if (overlappingApparel == null && possibleDefs.EnumerableNullOrEmpty())
                {
                    Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.gray);
                }
                else
                {
                    if (overlappingApparel != null)
                    {
                        var col = GetColorForTagAtIndex(loadoutTags.IndexOf(overlappingTag));
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, col);
                        Widgets.DefIcon(columnRect.ContractedBy(3f), overlappingApparel.Def, overlappingApparel.RandomStuff);
                        
                        if (possibleDefs.Any())
                        {
                            var str = "";
                            foreach (var (pTag, pApparel) in possibleDefs) {
                                str += $"\n - {pApparel.Def.LabelCap} from {pTag.name}";
                            }
                            
                            TooltipHandler.TipRegion(columnRect, $"{overlappingApparel.Def.LabelCap} from {overlappingTag.name} blocks:{str}");
                        }
                    } else
                    {
                        var nextHighestPrio = possibleDefs.OrderBy(tup => loadoutTags.IndexOf(tup.Item1)).First().Item2;
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.red);
                        Widgets.DefIcon(columnRect.ContractedBy(3f), 
                            nextHighestPrio.Def, 
                            color: new Color(0.5f, 0.5f, 0.5f, 0.3f));
                        
                        var str = "Blocked Tags:";
                        foreach (var (pTag, pApparel) in possibleDefs) {
                            str += $"\n - {pApparel.Def.LabelCap} from {pTag.name}";
                        }
                        TooltipHandler.TipRegion(columnRect, str);

                    }
                }
            }
 
            Text.Font = GameFont.Small;
 
            curY += GUIUtility.SPACED_HEIGHT;
 
            foreach (var child in group.children)
            {
                DrawBodyPartGroup(bodyDef, child, layers, ref curY, columns, wornApparel);
            }
        }
    }
}