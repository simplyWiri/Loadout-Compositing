using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Inventory {

    public class Dialog_TagEditor : Window {

        enum State {

            Apparel,
            Melee,
            Ranged,
            Medicinal,
            Items

        }

        enum SortBy
        {
            Name,
            Stat
        }

        private Vector2 curScroll = Vector2.zero;
        private Vector2 curItemScroll = Vector2.zero;
        private Vector2 pawnListScroll = Vector2.zero;

        private float rightColumnHeight = 320;
        private bool collapsedPanel = true;
        private bool dragging = false;

        private bool draggingItems = false;
        private int draggingItemIdx = -1;
        
        private State curState = State.Apparel;
        private string defFilter = string.Empty;
        private string lastDefFilter = string.Empty;
        private List<ThingDef> lastDefList = null;

        private bool onlyResearchedItems = false;
        private static Dictionary<ThingDef, float> statCache = new Dictionary<ThingDef, float>();
        private SortBy sortedByKind = SortBy.Name;
        private bool ascending = false;
        private StatDef selectedStat = null;
        private float statLength = UIC.DEFAULT_HEIGHT;
        private Color conflictingApparelColour = new Color(.75f, 0.2f, 0.0f, .8f);
        private ThingDef hoveredDef = null;
        
        private const float INVALID_STAT_VALUE = -10000;

        public Tag curTag = null;

        public override Vector2 InitialSize => new Vector2(840, 640);

        public Dialog_TagEditor() {
            closeOnClickedOutside = true;
            doCloseX = true;
        }

        public Dialog_TagEditor(Tag tag) {
            curTag = tag;
            closeOnClickedOutside = true;
            doCloseX = true;
            resizeable = true;
        }

        void ResetTabState() {
            sortedByKind = SortBy.Name;
            selectedStat = null;
            statLength = UIC.DEFAULT_HEIGHT;

            statCache = new Dictionary<ThingDef, float>();
        }

        public void Draw(Rect rect) {
            DrawTagEditor(rect.LeftPart(.65f).TopPart(0.95f));

            if (curTag != null) {
                DrawRightColumn(rect.RightPart(0.35f));                
            }
        }

        public void DrawRightColumn(Rect rect) {

            var topContentHeight = rect.height - (collapsedPanel ? UIC.DEFAULT_HEIGHT : rightColumnHeight);
            var topContent = rect.PopTopPartPixels(topContentHeight);
            DrawItemColumns(topContent);

            var draggableBarRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            ResizeableBar(draggableBarRect);

            if ( !collapsedPanel ) {
                DrawPawnList(rect);
            }
        }

        private void ResizeableBar(Rect grabRect) {
            if (!collapsedPanel && Mouse.IsOver(grabRect.LeftPartPixels(grabRect.width - grabRect.height))) {
                
                if (Input.GetMouseButtonDown(0)) {
                    dragging = true;
                }

                if (!ModBase.settings.disableCustomScroll) {
                    Cursor.SetCursor(Textures.DragCursorTex, CustomCursor.CursorHotspot, CursorMode.ForceSoftware);
                }
            } else {
                if (!ModBase.settings.disableCustomScroll) {
                    if (Prefs.data.customCursorEnabled) {
                        Cursor.SetCursor(CustomCursor.CursorTex, CustomCursor.CursorHotspot, CursorMode.Auto);
                    }
                    else {
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                    }
                }
            }
            
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(grabRect.x, grabRect.y, grabRect.width);
            Widgets.DrawLightHighlight(grabRect);
            
            Widgets.Label(grabRect, Strings.MassAssign);
            GUI.color = Color.white;

            if (Widgets.ButtonImageFitted(grabRect.RightPartPixels(grabRect.height), collapsedPanel ? TexButton.Plus : TexButton.Minus)) {
                collapsedPanel = !collapsedPanel;
            }

            var mousePos = Event.current.mousePosition;

            if (mousePos.x < grabRect.x - 25 || mousePos.x > grabRect.xMax + 25) {
                dragging = false;
                return;
            }
            
            if (dragging) {
                rightColumnHeight = (windowRect.height - mousePos.y) - UIC.DEFAULT_HEIGHT;
                rightColumnHeight = Mathf.Clamp(rightColumnHeight, UIC.DEFAULT_HEIGHT, windowRect.height - (UIC.DEFAULT_HEIGHT * 8));
            }

            if (Input.GetMouseButtonUp(0)) {
                dragging = false;
            }
        }

        private void DrawPawnList(Rect rect) {
            var pawns = Find.Maps.SelectMany(map => map.mapPawns.AllPawns.Where(p => p.IsValidLoadoutHolder())).ToList().OrderByDescending(p => p.Name.ToString()).ToList() ;

            var height = pawns.Count * UIC.DEFAULT_HEIGHT;
            var width = rect.width - (rect.height > height ? 0 : UIC.SCROLL_WIDTH);
            var viewRect = new Rect(rect.x, rect.y, width, height);
            
            Widgets.BeginScrollView(rect, ref pawnListScroll, viewRect);
            
            rect.y += pawnListScroll.y;
            var i = 0;
            
            foreach (var pawn in pawns) {
                var pRect = viewRect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);

                if (!pRect.Overlaps(rect)) {
                    continue;
                }

                if (i % 2 == 1) {
                    Widgets.DrawLightHighlight(pRect);
                }
                i++;
                
                var component = pawn.GetComp<LoadoutComponent>();
                var hasThing = component.Loadout.AllTags.Contains(curTag);

                Widgets.Label(pRect, pawn.LabelShort);
                
                if (GUIUtility.DraggableCheckbox(pRect, pRect.PopRightPartPixels(UIC.DEFAULT_HEIGHT), ref hasThing)) {
                    if (!hasThing) {
                        component.RemoveTag(component.Loadout.AllElements.FirstOrDefault(elem => elem.Tag == curTag));
                    } else {
                        component.AddTag(curTag);  
                    }
                }
            }
            
            Widgets.EndScrollView();
        }


        public void DrawTagEditor(Rect r) {
            _ = r.PopRightPartPixels(this.Margin);
            var topRect = r.TopPartPixels(UIC.DEFAULT_HEIGHT);

            if (Widgets.ButtonText(topRect.LeftPart(0.33f), Strings.SelectTag)) {
                Find.WindowStack.Add(new Dialog_TagSelector(LoadoutManager.Tags.Except(curTag).ToList(), tag => curTag = tag, false));
            }

            topRect.AdjHorzBy(topRect.width * 0.33f);
            if (Widgets.ButtonText(topRect.LeftHalf(), Strings.CreateNewTag)) {
                curTag = new Tag(string.Empty);
                LoadoutManager.AddTag(curTag);
            }

            if (Widgets.ButtonText(topRect.RightHalf(), Strings.DeleteTag)) {
                Find.WindowStack.Add(new Dialog_TagSelector( new List<Tag>{ curTag }, tag => {
                    LoadoutManager.RemoveTag(tag);
                    curTag = null;
                }, false));
            }

            r.AdjVertBy(UIC.DEFAULT_HEIGHT);

            if (curTag == null) return;

            var defaultOnRect = r.TopPartPixels(30).RightHalf();
            TooltipHandler.TipRegion(defaultOnRect, Strings.EnableTagByDefaultDesc);
            defaultOnRect.PopTopPartPixels(3);
            var defaultOnRectButton = defaultOnRect.PopRightPartPixels(20).TopPartPixels(20);
            defaultOnRect.PopRightPartPixels(5);

            GUIUtility.DraggableCheckbox(defaultOnRectButton, defaultOnRectButton, ref curTag.defaultEnabled);
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = Color.gray;
            Widgets.Label(defaultOnRect, Strings.EnableTagByDefault);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperRight;

            Widgets.ListSeparator(ref r.m_YMin, r.width, Strings.Modify + " " + curTag.name);

            // [ Tag Name ] [ Edit Name ] 
            var rect = r.PopTopPartPixels(UIC.DEFAULT_HEIGHT);

            GUIUtility.InputField(rect, Strings.ChangeTagName, ref curTag.name);
            curTag.name ??= " ";

            var viewRect = new Rect(r.x, r.y, rect.width - UIC.SCROLL_WIDTH,
                (curTag.requiredItems.Count * UIC.SPACED_HEIGHT * 2));
            Widgets.BeginScrollView(r, ref curItemScroll, viewRect);

            var visibilityRect = r;
            visibilityRect.y += curItemScroll.y;
            var baseRect = viewRect;

            var i = 0;
            // List each item in the currently required items
            // [ Info ] [ Icon ] [ Name ] [ Edit Filter ] [ Edit Quantity ] [ Remove ]  
            foreach (var item in curTag.requiredItems.ToList()) {
                var def = item.Def;
                var itemRect = baseRect.PopTopPartPixels(UIC.SPACED_HEIGHT * 2);

                if (!itemRect.Overlaps(visibilityRect)) {
                    continue;
                }

                if (i % 2 == 0) {
                    Widgets.DrawLightHighlight(itemRect);
                }
                i++;

                // Info
                Rect infoRect = itemRect.PopLeftPartPixels(UIC.SMALL_ICON);
                if (Widgets.ButtonImageFitted(infoRect.ContractedBy(1f), TexButton.Info)) {
                    Find.WindowStack.Add(new Dialog_InfoCard(def, item.RandomStuff));
                }

                // Icon
                var iconRect = itemRect.PopLeftPartPixels(UIC.SPACED_HEIGHT * 2);
                if (item.Quantity > 1) {
                    GUIUtility.FittedDefIconCount(iconRect, def, item.RandomStuff, item.Quantity);
                }
                else {
                    Widgets.DefIcon(iconRect, def, item.RandomStuff);
                }

                TooltipHandler.TipRegion(iconRect, item.Def.DescriptionDetailed);

                // Remove
                var removeButton = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 1.5f);
                if (Widgets.ButtonImageFitted(removeButton.ContractedBy(1f), TexButton.Delete)) {
                    curTag.requiredItems.Remove(item);
                }

                TooltipHandler.TipRegion(removeButton, Strings.RemoveItemFromTag);

                // Copy, Paste
                var copyPasteButton = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 3);
                GUIUtility.DraggableCopyPaste(copyPasteButton, ref item.filter, Filter.CopyFrom);
                TooltipHandler.TipRegion(copyPasteButton, Strings.CopyPasteExplain);

                // Edit 
                var constrainButton = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 1.5f);
                if (Widgets.ButtonImageFitted(constrainButton.ContractedBy(1f), Textures.EditTex))
                    Find.WindowStack.Add(new Dialog_ItemSpecifier(item.Filter));
                TooltipHandler.TipRegion(constrainButton, Strings.SpecifyElementsToolTip);

                var quantityFieldRect = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 2f);
                item.quantityStr ??= item.Quantity.ToString();
                Widgets.TextFieldNumeric(quantityFieldRect.ContractedBy(0, quantityFieldRect.height / 4.0f), ref item.quantity, ref item.quantityStr, 1);

                TooltipHandler.TipRegion(quantityFieldRect, Strings.EditQuantity);

                // Name
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(itemRect, item.Label);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            DraggableItems(viewRect, curTag.requiredItems);

            Widgets.EndScrollView();
        }

        private void DraggableItems(Rect rect, List<Item> items) {
            if (items.Count == 1) return;
            
            Rect RectForTag(int tIdx, bool full = false) 
                => new Rect(rect.x, rect.y + tIdx * UIC.SPACED_HEIGHT * 2, rect.width - (full ? 0 : 6 * UIC.SPACED_HEIGHT), UIC.SPACED_HEIGHT * 2);

            var cEvent = Event.current;

            if (cEvent.rawType == EventType.MouseUp) {
                draggingItems = false;
                draggingItemIdx = -1;
            }

            if (draggingItems) {
                if (Mouse.IsOver(rect.ExpandedBy(25f))) {
                    var tRect = RectForTag(draggingItemIdx, true);
                    var mPos = cEvent.mousePosition;

                    if (!tRect.ExpandedBy(5f).Contains(mPos)) {
                        if (draggingItemIdx > 0 && mPos.y < tRect.y) {
                            (curTag.requiredItems[draggingItemIdx], curTag.requiredItems[draggingItemIdx - 1]) = (curTag.requiredItems[draggingItemIdx - 1], curTag.requiredItems[draggingItemIdx]);
                            draggingItemIdx -= 1;
                        }
                        else if (draggingItemIdx < items.Count - 1 && mPos.y > tRect.y) {
                            (curTag.requiredItems[draggingItemIdx], curTag.requiredItems[draggingItemIdx + 1]) = (curTag.requiredItems[draggingItemIdx + 1], curTag.requiredItems[draggingItemIdx]);
                            draggingItemIdx += 1;
                        }

                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    }

                    GUI.color = ReorderableWidget.LineColor;
                    Widgets.DrawLine(new Vector2(tRect.x, tRect.yMax), new Vector2(tRect.xMax, tRect.yMax), ReorderableWidget.LineColor, 2f);
                    Widgets.DrawHighlight(tRect);
                    GUI.color = Color.white;
                } else {
                    draggingItems = false;
                    draggingItemIdx = -1;
                }
            }

            if (cEvent.rawType != EventType.MouseDown || !Mouse.IsOver(rect)) {
                return;
            }
            
            for (var i = 0; i < items.Count; i++) {
                var tRect = RectForTag(i);

                if (!tRect.Contains(cEvent.mousePosition)) continue;
                
                draggingItemIdx = i;
                draggingItems = true;
                
                Event.current.Use();
                
                return;
            }
        }

        // [ Apparel ] [ Melee ] [ Ranged ] [ Medical / Drugs ] 
        public void DrawItemColumns(Rect r) {
            var topRect = r.TopPartPixels(UIC.DEFAULT_HEIGHT);

            void DrawOptionButton(Texture2D tex, string tooltip, State state) {
                var optionRect = topRect.PopLeftPartPixels(UIC.DEFAULT_HEIGHT);
                GUI.DrawTexture(optionRect, tex, ScaleMode.ScaleToFit, true, 1f,
                                curState == state ? GenUI.MouseoverColor : Color.white, 0f, 0f);
                TooltipHandler.TipRegion(optionRect, tooltip);
                if (Widgets.ButtonInvisible(optionRect)) {
                    curState = state;
                    lastDefFilter = string.Empty;
                    lastDefList = null;
                    ResetTabState();
                    GUI.FocusControl("Def List Filter");
                }
            }

            r.AdjVertBy(UIC.DEFAULT_HEIGHT);

            DrawOptionButton(Textures.ApparelTex, "Apparel", State.Apparel);
            DrawOptionButton(Textures.MeleeTex, "Melee", State.Melee);
            DrawOptionButton(Textures.RangedTex, "Ranged", State.Ranged);
            DrawOptionButton(Textures.MedicalTex, "Medicine", State.Medicinal);
            DrawOptionButton(Textures.MiscItemsTex, "Items", State.Items);

            if (selectedStat != null) {
                Text.Font = GameFont.Tiny;
                Text.WordWrap = false;
                var strLen = selectedStat.LabelCap.GetWidthCached();
                topRect.width = Mathf.Max(topRect.width, strLen);
                GUIUtility.WithModifiers(topRect, selectedStat.LabelCap, color: Color.gray, anchor: TextAnchor.MiddleRight);
                Text.Font = GameFont.Small;
                Text.WordWrap = true;
            }
            
            switch (curState) {
                case State.Apparel:
                    DrawDefList(r, Utility.apparelDefs);
                    break;
                case State.Melee:
                    DrawDefList(r, Utility.meleeWeapons, Dialog_ItemSpecifier.baseWeaponStats.Union(Dialog_ItemSpecifier.meleeWeaponStats).ToList());
                    break;
                case State.Ranged:
                    DrawDefList(r, Utility.rangedWeapons, Dialog_ItemSpecifier.baseWeaponStats.Union(Dialog_ItemSpecifier.rangedWeaponStats).ToList());
                    break;
                case State.Medicinal:
                    DrawDefList(r, Utility.medicinalDefs);
                    break;
                case State.Items:
                    DrawDefList(r, Utility.items);
                    break;
            }
        }

        private void DrawDefList(Rect r, IReadOnlyList<ThingDef> defList, List<StatDef> extraStats = null) {
            var itms = curTag.requiredItems.Select(it => it.Def).ToHashSet();
            List<ThingDef> defs = null;

            // todo: cleanup
            var slotsUsed = ApparelSlotMaker.Create(BodyDefOf.Human, curTag.requiredItems.Select(s => s.Def).Where(def => def.IsApparel).ToList());

            if (defFilter != string.Empty) {
                if (defFilter == lastDefFilter) {
                    defs = lastDefList.Where(t => (t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, t)) || !t.IsApparel)).ToList();
                } else {
                    var filter = defFilter.ToLower();
                    var acceptedLayers = DefDatabase<ApparelLayerDef>.AllDefsListForReading.Where(l => l.LabelCap.ToString().ToLower().Contains(filter));
                    var stats = DefDatabase<StatDef>.AllDefsListForReading.Where(s => s.LabelCap.ToString()?.ToLowerInvariant().Contains(filter) ?? false).ToHashSet();

                    defs = defList.Where(t => t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, t)) || !t.IsApparel).ToList();
                    defs = defs.Where(td => {
                        return (td.LabelCap.ToString()?.ToLowerInvariant().Contains(filter) ?? false)
                               || (td.modContentPack?.Name.ToLowerInvariant().Contains(filter) ?? false)
                               || ((td.IsApparel || td.IsWeapon) && (td.statBases?.Any(s => stats.Contains(s.stat)) ?? false))
                               || ((td.IsApparel || td.IsWeapon) && (td.equippedStatOffsets?.Any(s => stats.Contains(s.stat)) ?? false))
                               || (td.IsApparel && td.apparel.layers.Intersect(acceptedLayers).Any());
                    }).ToList();

                    lastDefFilter = defFilter;
                    lastDefList = defs;
                }
            }
            else {
                defs = defList.Where(t => (t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, t)) || !t.IsApparel)).ToList();
            }
            
            defs = defs.Where(t => !itms.Contains(t)).ToList();
            
            if (onlyResearchedItems) {
                var recipes = DefDatabase<RecipeDef>.AllDefs.Where(recipe => recipe.AvailableNow).ToList();
                
                defs = defs.Where(def => def.IsBuildingArtificial || recipes.Any(recipe => recipe.products.Any(pc => pc.thingDef == def))).ToList();
                defs = defs.Where(def => !def.IsBuildingArtificial || def.IsResearchFinished).ToList();
            }

            string NameSelector(ThingDef td) => td.LabelCap.RawText;
            float StatSelector(ThingDef td) => statCache[td];

            if (ascending) {
                if (sortedByKind == SortBy.Name) {
                    defs.SortBy(NameSelector);
                } else {
                    defs.SortBy(StatSelector);
                }
            } else {
                if (sortedByKind == SortBy.Name) {
                    defs.SortByDescending(NameSelector);
                } else {
                    defs.SortByDescending(StatSelector);
                }
            }

            var topBarRect = r.PopTopPartPixels(UIC.SPACED_HEIGHT);
            
            var onlyResearchedRect = topBarRect.PopRightPartPixels(UIC.DEFAULT_HEIGHT).MiddlePartPixels(Text.LineHeight);
            onlyResearchedRect.CenterWithWidth(Text.LineHeight);
            if (Widgets.ButtonImage(onlyResearchedRect, Textures.FilterByResearchedTex)) {
                onlyResearchedItems = !onlyResearchedItems;
            }
            TooltipHandler.TipRegion(onlyResearchedRect, Strings.ResearchedItemsDesc);

            topBarRect.PopRightPartPixels(3f);
            
            var sortByFilterRect = topBarRect.PopRightPartPixels(UIC.DEFAULT_HEIGHT).MiddlePartPixels(Text.LineHeight);
            sortByFilterRect.CenterWithWidth(Text.LineHeight);
            if (Widgets.ButtonImage(sortByFilterRect, ascending ? Textures.SortByStatAscTex : Textures.SortByStatDscTex)) {
                if (Event.current.button == 1) { // right click
                    // Collate all the set of stats in this list.
                    var stats = defList
                                .SelectMany(td => td.statBases.Select(sm => sm.stat)
                                                    .Union(td.equippedStatOffsets?.Select(sm => sm.stat) ?? new List<StatDef>()))
                                .ToHashSet();

                    stats.AddRange(extraStats ?? new List<StatDef>());
                    
                    var opts = new List<FloatMenuOption>() {
                        new FloatMenuOption("Name", () => {
                            sortedByKind = SortBy.Name;
                            selectedStat = null;
                            statLength = UIC.DEFAULT_HEIGHT;
                        })
                    };
                    
                    foreach (var stat in stats.OrderBy(st => st.LabelCap.RawText)) {
                        opts.Add(new FloatMenuOption(stat.LabelCap, () => {
                            sortedByKind = SortBy.Stat;
                            selectedStat = stat;
                            statLength = UIC.DEFAULT_HEIGHT;
                            
                            foreach(var def in defList) {
                                var stuff = GenStuff.AllowedStuffsFor(def).FirstOrDefault();
                                var cachedDrawEntries = def.SpecialDisplayStats(StatRequest.For(def, stuff)).ToList();
                                cachedDrawEntries.AddRange(StatsReportUtility.StatsToDraw(def, stuff).Where(r => r.ShouldDisplay()));

                                var relevant = cachedDrawEntries.FirstOrDefault(s => s.stat == selectedStat);
                                statCache[def] = relevant?.value ?? INVALID_STAT_VALUE;
                            }
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(opts));
                } else {
                    ascending = !ascending;
                }
            }
            
            TooltipHandler.TipRegion(sortByFilterRect, Strings.SortThingsByDesc);

            var inputFieldRect = topBarRect.ContractedBy(2f);
            GUIUtility.InputField(inputFieldRect, "Def List Filter", ref defFilter);
            TooltipHandler.TipRegion(inputFieldRect, Strings.SearchBarDesc);

            var height = defs.Count * UIC.DEFAULT_HEIGHT;
            var width = r.width - (r.height > height ? 0 : UIC.SCROLL_WIDTH);
            var viewRect = new Rect(r.x, r.y, width, height);
            Widgets.BeginScrollView(r, ref curScroll, viewRect);
            GUI.BeginGroup(viewRect);

            var rect = new Rect(0, 0, viewRect.width, UIC.DEFAULT_HEIGHT);

            var viewFrustum = r.AtZero();
            viewFrustum.y += curScroll.y;

            hoveredDef = null;
            var rectCopy = rect;

            for (int i = 0; i < defs.Count; i++) {
                if (!rect.Overlaps(viewFrustum)) {
                    rect.y += UIC.DEFAULT_HEIGHT;
                    continue;
                }

                var rowRect = rect;
                rect.y += UIC.DEFAULT_HEIGHT;

                if (sortedByKind == SortBy.Stat) {
                    var statRect = rowRect.PopLeftPartPixels(statLength);

                    if (StatSelector(defs[0]) == StatSelector(defs[defs.Count - 1])) {
                        var statValue = StatSelector(defs[0]);
                        if (statValue != INVALID_STAT_VALUE) {
                            Widgets.Label(statRect, $"{statValue:0.##}");
                        }
                    } else {
                        var max = StatSelector(defs[ascending ? defs.Count - 1 : 0]);
                        var min = defs.Min(d => {
                            // Mathf.Max(max,)don't count invalid things when picking out the min.
                            var sv = StatSelector(d);
                            return sv == INVALID_STAT_VALUE ? max : sv;
                        });
                        
                        // max == min may occur if there is only one value, i.e. a stat possessed by only one of the 
                        // items in the list
                        float inRange(float x) => (max - min) == 0 ? 1 : (x - min) / (max - min);

                        var statValue = StatSelector(defs[i]);
                        if (statValue != INVALID_STAT_VALUE) {
                            var txt = $"{statValue:0.##}";
                            var textWidth = txt.GetWidthCached();

                            if (textWidth > statLength) {
                                statLength = textWidth;
                            }

                            GUI.color = new Color(1 - inRange(statValue), inRange(statValue), 0);
                            Widgets.Label(statRect, txt);
                            GUI.color = Color.white;
                        }
                    }
                }

                var descRect = rowRect.LeftPart(0.85f);
                var def = defs[i];

                Widgets.DefIcon(descRect.LeftPart(.15f), def);
                Widgets.Label(descRect.RightPart(.85f), def.LabelCap);

                if (Mouse.IsOver(rowRect) && def.IsApparel)
                {
                    hoveredDef = def;
                    conflictingApparelColour.a = (Mathf.Sin((float)(DateTime.Now.TimeOfDay.TotalMilliseconds % 3000) / 3000 * Mathf.PI * 2) * 0.25f) + 0.75f;
                }
                
                TooltipHandler.TipRegion(rowRect, () =>
                {
                    if (def.IsApparel)
                    {
                        return $"{def.DescriptionDetailed}\n\n" + Strings.ApparelOverlapFlavour(def.LabelCap, Strings.Highlighted.Colorize(conflictingApparelColour));
                    }
                    
                    return def.DescriptionDetailed;
                }, def.GetHashCode());

                if (Widgets.ButtonInvisible(descRect)) {
                    AddDefToTag(def);
                }

                if (Widgets.ButtonImageFitted(rowRect.RightPart(0.15f).ContractedBy(2f), TexButton.Info)) {
                    var stuff = def.MadeFromStuff ? GenStuff.AllowedStuffsFor(def).First() : null;
                    Find.WindowStack.Add(new Dialog_InfoCard(def, stuff));
                }

                if (i % 2 == 0)
                    Widgets.DrawLightHighlight(rowRect);

                Widgets.DrawHighlightIfMouseover(rowRect);
            }

            if (hoveredDef != null)
            {
                var coveringSlots = ApparelSlotMaker.Create(BodyDefOf.Human, hoveredDef);
                
                void DrawHighlightIfConflicts(ThingDef otherDef, Rect rect)
                {
                    if (!otherDef.IsApparel || !coveringSlots.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, otherDef)))
                        return;

                    if (otherDef == hoveredDef)
                        return;
                    
                    GUI.color = conflictingApparelColour;
                    GUI.DrawTexture(rect, TexUI.RectHighlight);
                    GUI.color = Color.white;
                }
                
                // Do a second pass where we draw a highlight over the "conflicting" apparel;
                foreach (var t in defs)
                {
                    if (!rectCopy.Overlaps(viewFrustum)) {
                        rectCopy.y += UIC.DEFAULT_HEIGHT;
                        continue;
                    }

                    DrawHighlightIfConflicts(t, rectCopy);
                    rectCopy.y += UIC.DEFAULT_HEIGHT;
                }                
            }

            GUI.EndGroup();
            Widgets.EndScrollView();
        }

        private void AddDefToTag(ThingDef def) {
            curTag.Add(def);
        }

        public override void DoWindowContents(Rect inRect) {
            if (Event.current.type == EventType.Layout)
                return;

            Text.Font = GameFont.Small;

            Draw(inRect);
        }

    }

}