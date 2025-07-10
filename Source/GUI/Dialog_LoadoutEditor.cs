using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.UI;
using Verse;
using Verse.Sound;
using Text = Verse.Text;

namespace Inventory {

    public class Dialog_LoadoutEditor : Window {

        public const float WIDTH = 420f;
        public const float HEIGHT = 640f;

        internal Pawn pawn;
        internal LoadoutComponent component;
        internal LoadoutState shownState;
        private Vector2 tagScroll;
        private float tagsHeight = 9999f;


        private Vector2 statsScroll;
        private float statsHeight = 9999f;

        private Panel_ShowCoverage coveragePanel = null;
        private Panel_PawnStats pawnStatPanel = null;

        private bool dragging = false;
        private int curTagIdx = -1;

        public override Vector2 InitialSize {
            get {
                var width = WIDTH;
                if (pawnStatPanel.ShouldDraw) width += Panel_PawnStats.WIDTH;
                return new Vector2(width, HEIGHT);
            }
        }

        private Rect? fromOtherRect = null;

        public Dialog_LoadoutEditor(Pawn pawn) {
            this.pawn = pawn;
            this.component = pawn.GetComp<LoadoutComponent>();
            this.shownState = component.Loadout.CurrentState;
            coveragePanel = new Panel_ShowCoverage(this);
            pawnStatPanel = new Panel_PawnStats(this);

            // parent fields
            doCloseX = true;
            draggable = true;
        }

        public Dialog_LoadoutEditor(Pawn pawn, Dialog_LoadoutEditor old) {
            this.pawn = pawn;
            this.component = pawn.GetComp<LoadoutComponent>();
            this.shownState = component.Loadout.CurrentState;
            coveragePanel = new Panel_ShowCoverage(this, old.coveragePanel.ShouldDraw);
            pawnStatPanel = new Panel_PawnStats(this, old.pawnStatPanel.ShouldDraw);

            doCloseX = true;
            draggable = true;

            fromOtherRect = new Rect(old.windowRect);
        }

        // does nothing
        public override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            if (!fromOtherRect.HasValue) return;
            
            windowRect.x = fromOtherRect.Value.x;
            windowRect.y = fromOtherRect.Value.y;
            fromOtherRect = null;
        }

        public override void DoWindowContents(Rect inRect) {
            Text.Font = GameFont.Small;

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None) {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Comma)) {
                    Event.current.Use();
                    ThingSelectionUtility.SelectPreviousColonist();
                    return;
                }

                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Period)) {
                    Event.current.Use();
                    ThingSelectionUtility.SelectNextColonist();
                    return;
                }
            }

            inRect.PopTopPartPixels(UIC.SMALL_GAP);
            
            if (pawnStatPanel.ShouldDraw) {
                pawnStatPanel.Draw(inRect.PopLeftPartPixels(Panel_PawnStats.WIDTH));
            }

            var middlePanel = inRect.PopLeftPartPixels(WIDTH - Margin);

            var maxStatHeight = Mathf.Min(150f, statsHeight);
            var tagsRect = middlePanel.PopTopPartPixels(Mathf.Min(45 + UIC.SPACED_HEIGHT * 2 + tagsHeight, middlePanel.height - (maxStatHeight + UIC.LIST_SEP_HEIGHT)));
            DrawTags(tagsRect);
            
            middlePanel.AdjVertBy(GenUI.GapTiny);

            DrawStatistics(middlePanel);

            if (coveragePanel.ShouldDraw) {
                coveragePanel.Draw(inRect);
            }
        }

        public void DrawTags(Rect rect) {
            var elements = component.Loadout.AllElements.ToList();

            DrawHeaderButtons(ref rect, elements);
            rect.AdjVertBy(GenUI.GapTiny);
            rect.PopRightPartPixels(UIC.SCROLL_WIDTH);

            GUIUtility.ListSeperator(ref rect, Strings.AppliedTags); 
            
            tagsHeight = elements.Sum(elem => UIC.SPACED_HEIGHT * Mathf.Max(1, (Mathf.CeilToInt(elem.Tag.requiredItems.Count / 4.0f))));
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, tagsHeight);
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);

            DraggableTags(viewRect, elements);

            var frustum = rect;
            frustum.y += Mathf.FloorToInt(tagScroll.y);

            foreach (var element in elements.ToList()) {
                var tag = element.Tag;
                var tagIdx = elements.FindIndex(t => t == element);
                var tagHeight = UIC.SPACED_HEIGHT * Mathf.Max(1, Mathf.CeilToInt(tag.requiredItems.Count / 4.0f));
                var tagRect = viewRect.PopTopPartPixels(tagHeight);
                var hovering = Mouse.IsOver(tagRect);

                if (!tagRect.Overlaps(frustum)) {
                    continue;
                }

                var editButtonRect = tagRect.PopRightPartPixels(UIC.SPACED_HEIGHT).TopPartPixels(UIC.SPACED_HEIGHT);
                if (Widgets.ButtonImageFitted(editButtonRect, Textures.EditTex)) {
                    Find.WindowStack.Add(new Dialog_TagEditor(tag));
                }

                TooltipHandler.TipRegion(editButtonRect, $"Edit {tag.name}");

                if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(UIC.SPACED_HEIGHT).TopPartPixels(UIC.SPACED_HEIGHT), TexButton.Delete)) {
                    component.RemoveTag(element);
                }

                var dRect = tagRect.PopLeftPartPixels(UIC.SMALL_GAP);
                Widgets.DrawBoxSolid(dRect, Panel_ShowCoverage.GetColorForTagAtIndex(tagIdx));
                if (hovering) {
                    var center = dRect.center;
                    var hRect = new Rect(center.x - dRect.width / 2.0f, center.y - UIC.SPACED_HEIGHT / 2.0f, UIC.SMALL_GAP, UIC.SPACED_HEIGHT);
                    var alpha = Mathf.Sin((Time.time % 2)/2.0f * Mathf.PI) * 0.3f;
                    var col = Color.black;
                    col.a = 0.7f + alpha;
                    GUI.color = col;
                    Widgets.DrawTexturePart(hRect, new Rect(1/4f, 0, 1/2f, 1), Textures.DraggableTex);
                    GUI.color = Color.white;
                }
                
                var elemName = $" {element.StateName} ";
                var width = Mathf.Min(elemName.GetWidthCached() + 5, tagRect.width - UIC.SPACED_HEIGHT * 4);
                if (Widgets.ButtonText(tagRect.PopRightPartPixels(width).TopPartPixels(UIC.SPACED_HEIGHT), elemName)) {
                    Find.WindowStack.Add(new Dialog_SetTagLoadoutState(pawn.GetComp<LoadoutComponent>().Loadout, element));
                }

                var iconsRect = tagRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 4);

                tagRect.AdjHorzBy(3f);
                if(tag.name.GetWidthCached() + UIC.SMALL_GAP > tagRect.width) {
                    int idx = tag.name.Length;
                    var truncatedName = tag.name;
                    do {
                        idx -= 1;
                        truncatedName = truncatedName.Substring(0, idx);
                    }
                    while (truncatedName.GetWidthCached() + UIC.SMALL_GAP > tagRect.width);

                    Widgets.Label(tagRect, truncatedName);
                    TooltipHandler.TipRegion(tagRect, tag.name);
                } else {
                    Widgets.Label(tagRect.PopLeftPartPixels(tag.name.GetWidthCached() + UIC.SMALL_GAP), tag.name);
                }

                // draw required items in blocks of 4
                for (int i = 0; i < tag.requiredItems.Count; i += 4) {
                    for (int j = 0; j < 4; j++) {
                        var drawRect = new Rect(iconsRect.x + UIC.SPACED_HEIGHT * j, iconsRect.y + (i / 4.0f) * UIC.SPACED_HEIGHT, UIC.SPACED_HEIGHT, UIC.SPACED_HEIGHT);
                        var expFrustum = frustum.ExpandedBy(UIC.SPACED_HEIGHT * 1/2f);
                        if (!expFrustum.Contains(drawRect.min) || !expFrustum.Contains(drawRect.max)) {
                            continue;
                        }
                        
                        var idx = i + j;
                        if (idx >= tag.requiredItems.Count) break;
                        var item = tag.requiredItems[idx];
                        if (item.Quantity > 1) {
                            GUIUtility.FittedDefIconCount(drawRect, item.Def, item.RandomStuff, item.Quantity);
                        }
                        else {
                            Widgets.DefIcon(drawRect, item.Def, item.RandomStuff);
                        }

                        TooltipHandler.TipRegion(drawRect, item.Def.LabelCap);
                    }
                }
            }

            Widgets.EndScrollView();
        }
        
        private void DraggableTags(Rect viewRect, List<LoadoutElement> elements) {
            if (elements.Count == 1) return;
            
            viewRect.width -= 2 * UIC.SPACED_HEIGHT;

            Rect RectForTag(int tIdx) {
                var elem = elements[tIdx];
                var offset = elements
                    .GetRange(0, tIdx)
                    .Sum(element => UIC.SPACED_HEIGHT * Mathf.Max(1, Mathf.CeilToInt(element.Tag.requiredItems.Count / 4.0f)));

                return new Rect(viewRect.x, viewRect.y + offset, viewRect.width - ($" {elem.StateName} ".GetWidthCached() + 5), UIC.SPACED_HEIGHT * Mathf.Max(1, (Mathf.CeilToInt(elem.Tag.requiredItems.Count / 4.0f))));
            }

            var cEvent = Event.current;

            if (cEvent.rawType == EventType.MouseUp) {
                if (Prefs.data.customCursorEnabled) {
                    Cursor.SetCursor(CustomCursor.CursorTex, CustomCursor.CursorHotspot, CursorMode.Auto);
                } else {
                    Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                }
                
                dragging = false;
                curTagIdx = -1;
            }

            if (dragging) {
                if (Mouse.IsOver(viewRect.ExpandedBy(25f))) {
                    var tRect = RectForTag(curTagIdx);
                    var mPos = cEvent.mousePosition;

                    if (!tRect.ExpandedBy(5f).Contains(mPos)) {
                        if (curTagIdx > 0 && mPos.y < tRect.y) {
                            (component.Loadout.elements[curTagIdx], component.Loadout.elements[curTagIdx - 1]) = (component.Loadout.elements[curTagIdx - 1], component.Loadout.elements[curTagIdx]);
                            curTagIdx -= 1;
                        }
                        else if (curTagIdx < elements.Count - 1 && mPos.y > tRect.y) {
                            (component.Loadout.elements[curTagIdx], component.Loadout.elements[curTagIdx + 1]) = (component.Loadout.elements[curTagIdx + 1], component.Loadout.elements[curTagIdx]);
                            curTagIdx += 1;
                        }

                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    }

                    GUI.color = ReorderableWidget.LineColor;
                    Widgets.DrawLine(new Vector2(tRect.x, tRect.yMax), new Vector2(tRect.xMax, tRect.yMax), ReorderableWidget.LineColor, 2f);
                    Widgets.DrawHighlight(tRect);
                    GUI.color = Color.white;
                }
                else {
                    if (!ModBase.settings.disableCustomScroll) {
                        if (Prefs.data.customCursorEnabled) {
                            Cursor.SetCursor(CustomCursor.CursorTex, CustomCursor.CursorHotspot, CursorMode.Auto);
                        } else {
                            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                        }
                    }


                    dragging = false;
                    curTagIdx = -1;
                }
            }

            if (cEvent.rawType != EventType.MouseDown || !Mouse.IsOver(viewRect)) return;
            
            for (int i = 0; i < elements.Count; i++) {
                var rect = RectForTag(i);

                if (rect.Contains(cEvent.mousePosition)) {
                    curTagIdx = i;
                    dragging = true;
                    if (!ModBase.settings.disableCustomScroll) {
                        Cursor.SetCursor(Textures.DragCursorTex, CustomCursor.CursorHotspot, CursorMode.ForceSoftware);
                    }

                    Event.current.Use();
                    break;
                }
            }
        }

        public void DrawHeaderButtons(ref Rect rect, List<LoadoutElement> elements) {
            var buttonRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            buttonRect.PopRightPartPixels(Margin);

            if (Widgets.ButtonText(buttonRect.PopLeftPartPixels(rect.width / 3f), Strings.PawnStats)) {
                // We pop back the panel's x value because we want the 'central' panel to stay in the same position
                this.windowRect.x += Panel_PawnStats.WIDTH * (pawnStatPanel.ShouldDraw ? 1 : -1);
                this.windowRect.width += Panel_PawnStats.WIDTH * (pawnStatPanel.ShouldDraw ? -1 : 1);
                pawnStatPanel.ShouldDraw = !pawnStatPanel.ShouldDraw;
            }

            if (Widgets.ButtonText(buttonRect.LeftHalf(), Strings.AddTag)) {
                Find.WindowStack.Add(new Dialog_TagSelector(LoadoutManager.Tags.Except(elements.Select(elem => elem.Tag)).ToList(), component.AddTag));
            }

            if (Widgets.ButtonText(buttonRect.RightHalf(), coveragePanel.ShouldDraw ? Strings.HideCoverage : Strings.ShowCoverage)) {
                // Active -> Inactive, reset back to default widths
                if (coveragePanel.ShouldDraw) {
                    this.windowRect.width = (pawnStatPanel.ShouldDraw ? Panel_PawnStats.WIDTH : 0) + WIDTH;
                }

                coveragePanel.ShouldDraw = !coveragePanel.ShouldDraw;
            }

            var selectStateButtonRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            selectStateButtonRect.PopRightPartPixels(Margin);
            selectStateButtonRect.PopTopPartPixels(5f);

            var fStr = Strings.ViewAsIf;
            var mStr = $" { (shownState == null ? Strings.DefaultStateNameInUse : shownState.name)} ";
            var sStr = Strings.WereActive;
            var sRect = selectStateButtonRect.PopRightPartPixels(sStr.GetWidthCached() + 5);
            var mRect = selectStateButtonRect.PopRightPartPixels(mStr.GetWidthCached() + 5);
            selectStateButtonRect.width -= 5f;
            var fRect = selectStateButtonRect.PopRightPartPixels(fStr.GetWidthCached() + 5);

            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Dropdown(mRect.TopPartPixels(Text.LineHeight), this, (editor) => editor.shownState, States, mStr);

            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = Color.gray;
            Widgets.Label(sRect, sStr);
            Widgets.Label(fRect, fStr);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }   

        private static IEnumerable<Widgets.DropdownMenuElement<LoadoutState>> States(Dialog_LoadoutEditor editor) {
            yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                option = new FloatMenuOption(Strings.Create, () => Find.WindowStack.Add(new Dialog_LoadoutStateEditor(LoadoutManager.States))),
                payload = null,
            };
            
            if (editor.shownState != null) {
                yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                    option = new FloatMenuOption(Strings.DefaultStateNameInUse, () => editor.shownState = null),
                    payload = null
                };
            }

            foreach (var state in LoadoutManager.States.Except(editor.shownState)) {
                yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                    option = new FloatMenuOption(state.name, () => editor.shownState = state),
                    payload = state
                };
            }
        }

        public void DrawStatistics(Rect rect) {
            rect.PopRightPartPixels(UIC.SCROLL_WIDTH);
            rect.height -= UIC.SMALL_GAP;
            
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, statsHeight);

            var height = 0f;
            Widgets.BeginScrollView(rect, ref statsScroll, viewRect);

            GUIUtility.ListSeperator(ref viewRect, Strings.LoadoutStatistics);

            viewRect.AdjVertBy(GenUI.GapTiny);

            var loadoutItems = component.Loadout.ItemsWith(shownState).ToList();

            GUIUtility.BarWithOverlay(
                viewRect.PopTopPartPixels(UIC.SPACED_HEIGHT),
                Utility.HypotheticalEncumberancePercent(pawn, loadoutItems),
                Utility.HypotheticalUnboundedEncumberancePercent(pawn, loadoutItems) > 1f
                    ? Textures.ValvetTex as Texture2D
                    : Textures.RWPrimaryTex as Texture2D,
                Strings.Weight,
                Utility.HypotheticalGearAndInventoryMass(pawn, loadoutItems).ToString("0.#") + "/" +
                Utility.HypotheticalCapacity(pawn, loadoutItems).ToStringMass(),
                Strings.WeightOverCapacity);

            // Needs more work to get this done. 
            
            //
            // var apparelBonuses = new List<StatDef> {
            //     StatDefOf.ArmorRating_Sharp, StatDefOf.ArmorRating_Blunt, StatDefOf.ArmorRating_Heat, 
            //     StatDefOf.Insulation_Cold, StatDefOf.Insulation_Heat, StatDefOf.Flammability
            // };
            //
            // foreach (var apparelStat in apparelBonuses) {
            //     var totalScore = 0.0f;
            //     foreach (var apparel in hypotheticalApparel) {
            //         totalScore += apparel.Def.GetStatValueAbstract(apparelStat, apparel.RandomStuff);
            //     }
            //     
            //     var statRect = viewRect.PopTopPartPixels(Text.LineHeight);
            //     
            //     string statNameStr = $"{apparelStat.LabelCap} "
            //         , statValueStr = $"{apparelStat.Worker.ValueToString(totalScore, false)}";
            //     
            //     GUIUtility.WithModifiers(statRect.PopLeftPartPixels(statNameStr.GetWidthCached()), statNameStr, color: Color.gray);
            //     Widgets.Label(statRect.PopLeftPartPixels(statValueStr.GetWidthCached() + UIC.SMALL_GAP), statValueStr);
            // }

            var hypotheticalApparel = component.Loadout.HypotheticalWornApparel(shownState, pawn.def.race.body).ToList();
            var relevantItems = hypotheticalApparel.Union(loadoutItems.Where(item => !item.Def.IsApparel)).ToList();

            if ((hypotheticalApparel.Count + relevantItems.Count) < 0) {
                statsHeight = GenUI.GapTiny + UIC.SPACED_HEIGHT;
                return;
            } 
                
            Text.Anchor = TextAnchor.MiddleRight;
            GUIUtility.ListSeperator(ref viewRect, Strings.EquippedStatOffsets + " ", tooltip: Strings.EquippedStatOffsetsDesc);
            Text.Anchor = TextAnchor.UpperLeft;
            
            var statBonuses = new Dictionary<StatDef, List<Item>>();

            foreach (var potentialStatBoostingItem in relevantItems) {
                foreach (var mod in potentialStatBoostingItem.Def.equippedStatOffsets ?? new List<StatModifier>()) {
                    if (!statBonuses.TryGetValue(mod.stat, out var list)) {
                        statBonuses.Add(mod.stat, new List<Item> { potentialStatBoostingItem });
                        continue;
                    }
                    list.Add(potentialStatBoostingItem);
                }
            }

            foreach (var (stat, items) in statBonuses) {
                var totalScore = items.Sum(item => item.GetStatOffset(stat));

                var statRect = viewRect.PopTopPartPixels(Text.LineHeight);
                
                string statNameStr = $"{stat.LabelCap} "
                    , statValueStr = $"{stat.Worker.ValueToString(totalScore, false)}";
                
                GUIUtility.WithModifiers(statRect.PopLeftPartPixels(statNameStr.GetWidthCached()), statNameStr, color: Color.gray);
                Widgets.Label(statRect.PopLeftPartPixels(statValueStr.GetWidthCached() + UIC.SMALL_GAP), statValueStr);
                
                foreach (var item in items.OrderBy(it => it.GetStatOffset(stat))) {
                    var iconRect = statRect.PopRightPartPixels(Text.LineHeight);
                    Widgets.DefIcon(iconRect, item.Def, item.RandomStuff);
                    TooltipHandler.TipRegion(iconRect, item.Label + " " + stat.Worker.ValueToString(item.GetStatOffset(stat), false));
                }
            }

            height += GenUI.GapTiny + UIC.SPACED_HEIGHT + (2 * UIC.LIST_SEP_HEIGHT) + (statBonuses.Count * Text.LineHeight);
            
            Widgets.EndScrollView();

            statsHeight = height;
        }

        // private void TryDrawSpoofStatCalculations(ref Rect rect, StatDef stat, List<Item> items)
        // {
        //     var statRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
        //
        //     float statValue = stat.defaultBaseValue;
        //     
        //     
        //     foreach (var thing in items) {
        //         statValue += thing.MakeDummyThingNoId().GetStatValue(stat);
        //         // statValue += StatWorker.StatOffsetFromGear(thing.MakeDummyThingNoId(), stat);
        //     }
        //
        //     Widgets.Label(statRect, stat.LabelForFullStatList);
        //     Text.Anchor = TextAnchor.UpperRight;
        //     Widgets.Label(statRect, statValue.ToString());
        //     Text.Anchor = TextAnchor.UpperLeft;
        // }

    }

}