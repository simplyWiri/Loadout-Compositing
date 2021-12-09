using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using UnityEngine.UI;
using Verse;
using Text = Verse.Text;

namespace Inventory
{
    [HotSwappable]
    public class Dialog_LoadoutEditor : Window
    {
        private Pawn pawn;
        private LoadoutComponent component;
        private Vector2 tagScroll;
        private float tagsHeight = 9999f;
        
        private Vector2 wearableApparelScroll;

        private Vector2 apparelSlotsScroll;
        private float apparelSlotsHeight = 9999f;

        private Vector2 statsScroll;
        private float statsHeight = 9999f;
        public override Vector2 InitialSize
        {
            get
            {
                var width = 420;
                if (drawPawnStats) width += 210;
                if (drawShowCoverage) width += 420;
                return new Vector2(width, 640);
            }
        }

        public static List<Color> coloursByIdx = new List<Color>();
        
        private bool drawShowCoverage = false;
        private bool drawPawnStats = false;
        
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
            absorbInputAroundWindow = true;
            doCloseX = true;
            draggable = true;
        }
        
        public Dialog_LoadoutEditor(Pawn pawn, bool drawShowCoverage, bool drawPawnStats)
        {
            this.pawn = pawn;
            this.component = pawn.GetComp<LoadoutComponent>();
            this.drawShowCoverage = drawShowCoverage;
            this.drawPawnStats = drawPawnStats;
            absorbInputAroundWindow = true;
            doCloseX = true;
            draggable = true;
        }
        
        /* | Pawn Stats |  Apparel Slots | Add Tag     |
         * |            |                | Tag1 ^v     |
         * |            |                | Tag2 ^v     |
         * |            |                |------------ |
         * |            |                | Statistics  |
         * |            |                |             |
         */  
        public override void DoWindowContents(Rect inRect)
        {
            if (drawPawnStats)
            {
                var leftPanel = inRect.PopLeftPartPixels(210f);
                if (DrawPawnStats(leftPanel)) // returns true if selected another pawn
                {
                    return;
                }
            }
            
            var middlePanel = drawShowCoverage ? inRect.LeftPart(0.5f) : inRect;

            DrawTags(middlePanel.TopPartPixels(middlePanel.height / 2.0f));
            DrawStatistics(middlePanel.BottomPartPixels((middlePanel.height - Mathf.Min(middlePanel.height/2.0f, tagsHeight)) - GUIUtility.SPACED_HEIGHT * 2f));

            if (drawShowCoverage)
            {
                var apparelSlotRect = inRect.RightPart(0.50f);
                DrawApparelSlots(apparelSlotRect, pawn.RaceProps.body);
            }
        }

        public bool DrawPawnStats(Rect rect)
        {
            // header [ Prev ] [ Current Pawn Name ] [ Next ]
            //          Render of Pawn with Clothes
            var topPartRect = rect.PopTopPartPixels(GUIUtility.SPACED_HEIGHT);

            var lhs = topPartRect.PopLeftPartPixels(GUIUtility.SPACED_HEIGHT);
            var rhs = topPartRect.PopRightPartPixels(GUIUtility.SPACED_HEIGHT);
            if (Widgets.ButtonImageFitted(lhs, Textures.PlaceholderTex) || Input.GetKeyDown(KeyCode.LeftArrow)) {
                ThingSelectionUtility.SelectPreviousColonist();
                this.Close();
                Find.WindowStack.Add(new Dialog_LoadoutEditor(Find.Selector.SelectedPawns.First(), drawShowCoverage, drawPawnStats ));
                return true;
            }
            TooltipHandler.TipRegion(lhs, Strings.SelectPrevious);
            if (Widgets.ButtonImageFitted(rhs, Textures.PlaceholderTex) || Input.GetKeyDown(KeyCode.RightArrow)) {
                ThingSelectionUtility.SelectNextColonist();
                this.Close();
                Find.WindowStack.Add(new Dialog_LoadoutEditor(Find.Selector.SelectedPawns.First(), drawShowCoverage, drawPawnStats));
                return true;
            }
            TooltipHandler.TipRegion(rhs, Strings.SelectNext);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(topPartRect, $"{pawn.LabelShort}");
            Text.Anchor = TextAnchor.UpperLeft;
            
            rect.PopRightPartPixels(GenUI.GapTiny);
            rect.PopTopPartPixels(GenUI.GapTiny);

            GUIUtility.ListSeperator(ref rect, Strings.TopFourSkills);
            
            var skillList = pawn.skills.skills.OrderByDescending(skill => skill.Level).ToList();
            for (int i = 0; i < 4; i++)
            {
                var skillRect = rect.PopTopPartPixels(GUIUtility.SPACED_HEIGHT);
                var skill = skillList[i];
                
                if (skill.passion > Passion.None)
                {
                    Texture2D image = (skill.passion == Passion.Major) ? SkillUI.PassionMajorIcon : SkillUI.PassionMinorIcon;
                    Widgets.DrawTextureFitted(skillRect.LeftPartPixels(24), image, 1);
                }

                float fillPercent = Mathf.Max(0.01f, (float)skill.Level / 20f);
                Widgets.FillableBar(skillRect, fillPercent, SkillUI.SkillBarFillTex, null, false);

                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = Color.gray;
                Widgets.Label(skillRect.LeftPart(0.95f), $"{skill.def.skillLabel.CapitalizeFirst()} ({skill.Level})");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                
                TooltipHandler.TipRegion(skillRect, new TipSignal(SkillUI.GetSkillDescription(skill), skill.def.GetHashCode() * 397945));
            }
            
            GUIUtility.ListSeperator(ref rect, Strings.ApparelWhichCanBeWorn);
            var apparels = ApparelUtility.ApparelCanFitOnBody(pawn.RaceProps.body, component.Loadout.ThingsMatching(td => td.Def.IsApparel).Select(item => item.Def).ToList()).ToList();
            var allocatedHeight = apparels.Count * GUIUtility.DEFAULT_HEIGHT;

            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, allocatedHeight);
            
            Widgets.BeginScrollView(rect, ref wearableApparelScroll, viewRect);
            int aIdx = 0;
            foreach (var apparel in apparels)
            {
                var apparelRect = viewRect.PopTopPartPixels(GUIUtility.DEFAULT_HEIGHT);
                if ( aIdx++ % 2 == 0 )
                    Widgets.DrawLightHighlight(apparelRect);
                Widgets.DefIcon(apparelRect.LeftPart(.15f), apparel);
                Widgets.Label(apparelRect.RightPart(.85f), apparel.LabelCap);
            }

            Widgets.EndScrollView();
            
            return false;
        }

        public void DrawApparelSlots(Rect rect, BodyDef def)
        {
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, apparelSlotsHeight);
            Widgets.BeginScrollView(rect, ref apparelSlotsScroll, viewRect);
            
            float curY = GUIUtility.SPACED_HEIGHT;
            float beginY = curY;

            foreach (var category in ApparelUtility.GetBodyPartGroupFor(def).GetCategories().OrderByDescending(t => t.First().def.listOrder))
            {
                var layers = category.SelectMany(c => c.GetLayers()).Distinct().OrderByDescending(d => d.drawOrder).ToList();
                var cols = DrawHeader(new Rect(viewRect.x, curY, viewRect.width, GUIUtility.SPACED_HEIGHT), layers, curY).ToList();

                curY += GUIUtility.SPACED_HEIGHT + GUIUtility.DEFAULT_HEIGHT;

                foreach (var bp in category)
                {
                    DrawBodyPartGroup(def, bp, layers, ref curY,cols);
                }
                
                curY += GUIUtility.DEFAULT_HEIGHT;
            }

            apparelSlotsHeight = curY - beginY;
            
            Widgets.EndScrollView();
        }
        public static IEnumerable<Rect> DrawHeader(Rect rect, List<ApparelLayerDef> layers, float curY)
        {
            string GetHeader(int i)
            {
                return i == 0 ? "Body Group" : layers[i - 1].LabelCap; 
            }
            
            var numCols = layers.Count + 1;
            var width = rect.width / numCols;
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
 
                curX += width;
            }
            
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

        }
        
        public void DrawBodyPartGroup(BodyDef bodyDef, BodyPartGroup group, List<ApparelLayerDef> layers, ref float curY, List<Rect> columns)
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
            
            DrawHeaderButtons(ref rect, tags);
                
            rect.AdjVertBy(GenUI.GapTiny);
            height += GenUI.ListSpacing + GenUI.GapTiny;
            
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, tagsHeight);
            
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);
            
            GUIUtility.ListSeperator(ref viewRect, Strings.AppliedTags);
            height += 35;

            
            foreach (var tag in tags)
            {
                var tagIdx = tags.FindIndex(t => t == tag);
                var tagHeight = GenUI.ListSpacing * (Mathf.CeilToInt(tag.requiredItems.Count / 4.0f));
                var tagRect = viewRect.PopTopPartPixels(tagHeight );
                height += tagHeight;

                if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), Textures.PlaceholderTex)) {
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

        public void DrawHeaderButtons(ref Rect rect, List<Tag> tags)
        {
            var buttonRect = rect.PopTopPartPixels(GenUI.ListSpacing);

            if (Widgets.ButtonText(buttonRect.PopLeftPartPixels(rect.width / 3f), Strings.PawnStats))
            {
                this.windowRect.width = windowRect.width + (drawPawnStats ? -210f : 210f);
                drawPawnStats = !drawPawnStats;
            }
            
            if (Widgets.ButtonText(buttonRect.LeftHalf(), Strings.CreateNewTag))
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
                if(opts.Count == 0)
                {
                    Messages.Message(new Message(Strings.NoTagsYetWarning, MessageTypeDefOf.RejectInput));
                }
                else
                {
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }

            if (Widgets.ButtonText(buttonRect.RightHalf(), Strings.ShowCoverage))
            {
                this.windowRect.width = windowRect.width + (drawShowCoverage ? -420f : 420f);
                drawShowCoverage = !drawShowCoverage;
            }
        }

        public void DrawStatistics(Rect rect)
        {
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, statsHeight);
            
            //Widgets.DrawBoxSolid(rect, Color.green);

            var height = 0f;
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);
            
            GUIUtility.ListSeperator(ref viewRect, Strings.LoadoutStatistics);

            viewRect.AdjVertBy(GenUI.GapTiny);

            var loadoutItems = component.Loadout.AllItems.ToList();
            
            GUIUtility.BarWithOverlay(
                viewRect.TopPartPixels(GUIUtility.SPACED_HEIGHT),
                Utility.HypotheticalEncumberancePercent(pawn, loadoutItems),
                Utility.HypotheticalUnboundedEncumberancePercent(pawn, loadoutItems) > 1f ? GUIUtility.ValvetTex as Texture2D : GUIUtility.RWPrimaryTex as Texture2D,
                Strings.Weight,
                Utility.HypotheticalGearAndInventoryMass(pawn, loadoutItems).ToString("0.#") + "/" + MassUtility.Capacity(pawn).ToStringMass(),
                Strings.WeightOverCapacity);

            height += GenUI.GapTiny + GUIUtility.SPACED_HEIGHT;
            
            Widgets.EndScrollView();
            
            statsHeight = height;
        }
    }
}