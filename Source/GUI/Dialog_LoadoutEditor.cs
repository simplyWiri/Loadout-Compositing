using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    [HotSwappable]
    public class Dialog_LoadoutEditor : Window
    {
        private Pawn pawn;
        private LoadoutComponent component;
        private Vector2 tagScroll;
        private float tagsHeight = 9999f;

        private Vector2 statsScroll;
        private float statsHeight = 9999f;
        public override Vector2 InitialSize => new Vector2(420, 640);
        public static List<Color> coloursByIdx = new List<Color>();
        private bool drawShowCoverage = false;

        static Dialog_LoadoutEditor()
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

        public Dialog_LoadoutEditor(Pawn pawn)
        {
            this.pawn = pawn;
            this.component = pawn.GetComp<LoadoutComponent>();
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
        }
        
        /*   |  Apparel Slots | Add Tag     |
         *   |                | Tag1 ^v     |
         *   |                | Tag2 ^v     |
         *   |                |------------ |
         *   |                | Statistics  |
         *   |                |             |
         */  
        public override void DoWindowContents(Rect inRect)
        {
            var leftPanel = drawShowCoverage ? inRect.LeftPart(0.5f) : inRect;

            DrawTags(leftPanel.TopPartPixels(leftPanel.height / 2.0f));
            DrawStatistics(leftPanel.BottomPartPixels(leftPanel.height - Mathf.Min(leftPanel.height/2.0f, tagsHeight)));

            if (drawShowCoverage)
            {
                var apparelSlotRect = inRect.RightPart(0.50f);
                DrawApparelSlots(apparelSlotRect, pawn.RaceProps.body);
            }
        }

        public void DrawApparelSlots(Rect rect, BodyDef def)
        {
            float curY = GUIUtility.SPACED_HEIGHT;

            foreach (var category in ApparelUtility.GetBodyPartGroupFor(def).GetCategories().OrderByDescending(t => t.First().def.listOrder))
            {
                var layers = category.SelectMany(c => c.GetLayers()).Distinct().OrderByDescending(d => d.drawOrder).ToList();
                var cols = DrawHeader(new Rect(rect.x, curY, rect.width, GUIUtility.SPACED_HEIGHT), layers, curY).ToList();

                curY += GUIUtility.SPACED_HEIGHT;

                foreach (var bp in category)
                {
                    DrawBodyPartGroup(def, bp, layers, ref curY,cols);
                }
            }
        }
        public static IEnumerable<Rect> DrawHeader(Rect rect, List<ApparelLayerDef> layers, float curY)
        {
            string GetHeader(int i)
            {
                return i == 0 ? "Group Def" : layers[i - 1].LabelCap; 
            }
            
            var numCols = layers.Count + 1;
            var width = rect.width / numCols;
            var curX = rect.x;
 
            for (int i = 0; i < numCols; i++)
            {
                var headerRect = new Rect(curX, curY, width, GUIUtility.SPACED_HEIGHT);
 
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(headerRect, GetHeader(i));
                Text.Anchor = TextAnchor.UpperLeft;
                
                yield return new Rect(curX, GUIUtility.SPACED_HEIGHT, width, rect.height - GUIUtility.SPACED_HEIGHT);
 
                curX += width;
            }
        }
        
        public void DrawBodyPartGroup(BodyDef bodyDef, BodyPartGroup group, List<ApparelLayerDef> layers, ref float curY, List<Rect> columns)
        {
            var groupRect = new Rect(columns[0].x, curY, columns[0].width, GUIUtility.SPACED_HEIGHT);
 
            var def = group.def;
 
            Widgets.Label(groupRect, def.LabelCap);
 
            Text.Font = GameFont.Tiny;

            foreach (var column in group.layers.OrderByDescending(d => d.drawOrder))
            {
                var idx = layers.FirstIndexOf(c => c == column) + 1;
                var columnRect = new Rect(columns[idx].x, curY, columns[idx].width, GUIUtility.SPACED_HEIGHT);


                var tags = component.Loadout
                    .TagsMatching(item => item.Def.IsApparel 
                                       && new ApparelSlots(bodyDef, item.Def).Intersects(def, column)).ToList();
                
                var overlappingDefs = tags
                    .SelectMany(t => 
                        t.ItemsMatching(item => item.Def.IsApparel && new ApparelSlots(bodyDef, item.Def).Intersects(def, column)))
                    .ToList();
 
                if (tags.EnumerableNullOrEmpty())
                {
                    Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, Color.gray);
                }
                else
                {
                    // Multiple tags on a single overlapping spot
                    if (tags.Count == 1)
                    {
                        var col = coloursByIdx[component.Loadout.tags.FindIndex(t => t == tags.First())];
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, col);
                        Widgets.DefIcon(columnRect.ContractedBy(3f), overlappingDefs[0].Def, overlappingDefs[0].RandomStuff);
                    } else {
                        // todo:
                        var minIdx = tags.Select(t => component.Loadout.tags.FindIndex(t => t == tags.First())).Min();
                        var col = coloursByIdx[minIdx];
                        Widgets.DrawBoxSolidWithOutline(columnRect, Widgets.WindowBGFillColor, col);
                        Widgets.DefIcon(columnRect.ContractedBy(3f), overlappingDefs[0].Def, overlappingDefs[0].RandomStuff);
                    }
                }
            }
 
            Text.Font = GameFont.Small;
 
            curY += GUIUtility.SPACED_HEIGHT;
 
            foreach (var child in group.children)
            {
                DrawBodyPartGroup(bodyDef, child, layers, ref curY, columns);
            }
        }

        public void DrawTags(Rect rect)
        {
            var tags = component.Loadout.tags.ToList();
            var height = 0f;

            var buttonRect = rect.PopTopPartPixels(GenUI.ListSpacing);
            if (Widgets.ButtonText(buttonRect.LeftHalf(), "Add Tag"))
            {
                var opts = LoadoutManager.Tags.Except(tags).Select(tag =>
                    new FloatMenuOption(tag.name, () =>
                    {
                        if ( !LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList))
                        {
                            pList = new SerializablePawnList(new List<Pawn>());
                            LoadoutManager.PawnsWithTags.Add(tag, pList);
                        }
                        pList.pawns.Add(pawn);
                        component.Loadout.tags.Add(tag);
                    })).ToList();
                if(!opts.NullOrEmpty())
                    Find.WindowStack.Add(new FloatMenu(opts));
            }

            if (Widgets.ButtonText(buttonRect.RightHalf(), "Show Coverage"))
            {
                this.windowRect.width = windowRect.width + (drawShowCoverage ? -420f : 420f);
                drawShowCoverage = !drawShowCoverage;
            }

            rect.AdjVertBy(GenUI.GapTiny);
            height += GenUI.ListSpacing + GenUI.GapTiny;
            
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, tagsHeight);
            
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);
            
            GUIUtility.ListSeperator(ref viewRect, "Applied Tags: Priority High to Low");
            height += 35;

            
            foreach (var tag in tags)
            {
                var tagIdx = tags.FindIndex(t => t == tag);
                var tagHeight = GenUI.ListSpacing * (Mathf.CeilToInt(tag.requiredItems.Count / 4.0f));
                var tagRect = viewRect.PopTopPartPixels(tagHeight );
                height += tagHeight;

                if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), TexButton.IconBook)) {
                    Find.WindowStack.Add(new Dialog_TagEditor(tag));
                }
                
                if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), TexButton.DeleteX)) {
                    component.Loadout.tags.Remove(tag);
                    if ( LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList))
                    {
                        pList.pawns.Remove(pawn);
                    }
                }

                if (tagIdx != 0) {
                    if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), TexButton.ReorderUp))
                    {
                        var tmp = component.Loadout.tags[tagIdx - 1];
                        component.Loadout.tags[tagIdx - 1] = tag;
                        component.Loadout.tags[tagIdx] = tmp;
                    }
                }
                else
                {
                    tagRect.PopRightPartPixels(GenUI.ListSpacing);
                }

                if (tagIdx != tags.Count - 1) {
                    if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), TexButton.ReorderDown)) {
                        var tmp = component.Loadout.tags[tagIdx + 1];
                        component.Loadout.tags[tagIdx + 1] = tag;
                        component.Loadout.tags[tagIdx] = tmp;
                    }
                }
                else
                {
                    tagRect.PopRightPartPixels(GenUI.ListSpacing);
                }


                Widgets.DrawBoxSolid(tagRect.PopLeftPartPixels(10.0f), coloursByIdx[tagIdx]);
                Widgets.Label(tagRect.PopLeftPartPixels(tag.name.GetWidthCached() + 10f), tag.name);

                
                var y = tagRect.y;
                
                // draw required items in blocks of 3
                for (int i = 0; i < tag.requiredItems.Count; i+= 4) {
                    for (int j = 0; j < 4; j++) {
                        var drawRect = new Rect(tagRect.x + GenUI.ListSpacing * j, y + (i/4.0f) * GenUI.ListSpacing, GenUI.ListSpacing, GenUI.ListSpacing);
                        var idx = i + j;
                        if (idx >= tag.requiredItems.Count) break;
                        var item = tag.requiredItems[idx];
                        Widgets.DefIcon(drawRect, item.Def, item.RandomStuff);
                    }
                }
            }

            tagsHeight = height;
            
            Widgets.EndScrollView();
        }

        public void DrawStatistics(Rect rect)
        {
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, statsHeight);
            
            //Widgets.DrawBoxSolid(rect, Color.green);

            var height = 0;
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);
            
            
            Widgets.EndScrollView();
            statsHeight = height;
        }
    }
}