﻿using System;
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
            coveragePanel = new Panel_ShowCoverage(this);
            pawnStatPanel = new Panel_PawnStats(this);

            // parent fields
            doCloseX = true;
            draggable = true;
        }

        public Dialog_LoadoutEditor(Pawn pawn, Dialog_LoadoutEditor old) {
            this.pawn = pawn;
            this.component = pawn.GetComp<LoadoutComponent>();
            coveragePanel = new Panel_ShowCoverage(this, old.coveragePanel.ShouldDraw);
            pawnStatPanel = new Panel_PawnStats(this, old.pawnStatPanel.ShouldDraw);

            doCloseX = true;
            draggable = true;

            fromOtherRect = new Rect(old.windowRect);
        }

        // does nothing
        // public virtual void SetInitialSizeAndPosition()
        // {
        //     if (fromOtherRect.HasValue) {
        //         this.windowRect = fromOtherRect.Value;
        //         this.windowRect = this.windowRect.Rounded();
        //     }
        // }

        public override void DoWindowContents(Rect inRect) {
            Text.Font = GameFont.Small;
            // hack because `SetInitialSizeAndPosition` wasn't playing ball
            if (fromOtherRect.HasValue) {
                this.windowRect.x = fromOtherRect.Value.x;
                this.windowRect.y = fromOtherRect.Value.y;
                fromOtherRect = null;
            }

            if (Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None) {
                if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Comma)) {
                    ThingSelectionUtility.SelectPreviousColonist();
                    this.Close();
                    Find.WindowStack.Add(new Dialog_LoadoutEditor(Find.Selector.SelectedPawns.First(), this));
                    Event.current.Use();
                    return;
                }

                if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.Period)) {
                    ThingSelectionUtility.SelectNextColonist();
                    this.Close();
                    Find.WindowStack.Add(new Dialog_LoadoutEditor(Find.Selector.SelectedPawns.First(), this));
                    Event.current.Use();
                    return;
                }
            }

            if (pawnStatPanel.ShouldDraw) {
                // returns true if we pressed the button which selects a new pawn, in which case, this window has been closed
                // and drawing any further content is useless.
                if (pawnStatPanel.Draw(inRect.PopLeftPartPixels(Panel_PawnStats.WIDTH))) {
                    return;
                }
            }

            var middlePanel = inRect.PopLeftPartPixels(WIDTH - Margin);

            // 45 = 35 + 10, 35 = `ListSeperator` height, 10 = arbitrary buffer
            var tagsRect = middlePanel.PopTopPartPixels(Mathf.Min(45 + UIC.SPACED_HEIGHT + tagsHeight, middlePanel.height / 2.0f));
            DrawTags(tagsRect);
            DrawStatistics(middlePanel);

            if (coveragePanel.ShouldDraw) {
                coveragePanel.Draw(inRect);
            }
        }

        public void DrawTags(Rect rect) {
            var elements = component.Loadout.AllElements.ToList();

            DrawHeaderButtons(ref rect, elements);
            rect.AdjVertBy(GenUI.GapTiny);

            GUIUtility.ListSeperator(ref rect, Strings.AppliedTags);

            rect.PopRightPartPixels(UIC.SCROLL_WIDTH);
            tagsHeight = elements.Sum(elem => UIC.SPACED_HEIGHT * Mathf.Max(1, (Mathf.CeilToInt(elem.Tag.requiredItems.Count / 4.0f))));
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, tagsHeight);
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);

            DraggableTags(viewRect, elements);

            foreach (var element in elements.ToList()) {
                var tag = element.Tag;
                var tagIdx = elements.FindIndex(t => t == element);
                var tagHeight = UIC.SPACED_HEIGHT * Mathf.Max(1, (Mathf.CeilToInt(tag.requiredItems.Count / 4.0f)));
                var tagRect = viewRect.PopTopPartPixels(tagHeight);
                var hovering = Mouse.IsOver(tagRect);

                var editButtonRect = tagRect.PopRightPartPixels(UIC.SPACED_HEIGHT).TopPartPixels(UIC.SPACED_HEIGHT);
                if (Widgets.ButtonImageFitted(editButtonRect, Textures.EditTex)) {
                    Find.WindowStack.Add(new Dialog_TagEditor(tag));
                }

                TooltipHandler.TipRegion(editButtonRect, $"Edit {tag.name}");

                if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(UIC.SPACED_HEIGHT).TopPartPixels(UIC.SPACED_HEIGHT), TexButton.DeleteX)) {
                    component.Loadout.elements.Remove(element);

                    if (LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList)) {
                        pList.pawns.Remove(pawn);
                    }
                    
                    component.Loadout.UpdateState(element, false);
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
                
                tagRect.AdjHorzBy(3f);
                Widgets.Label(tagRect.PopLeftPartPixels(tag.name.GetWidthCached() + UIC.SMALL_GAP), tag.name);

                var elemName = $" {element.StateName} ";
                if (Widgets.ButtonText(tagRect.PopRightPartPixels(elemName.GetWidthCached() + 5).TopPartPixels(UIC.SPACED_HEIGHT), elemName)) {
                    Find.WindowStack.Add(new Dialog_SetTagLoadoutState(pawn.GetComp<LoadoutComponent>().Loadout, element));
                }

                var y = tagRect.y;

                // draw required items in blocks of 4
                for (int i = 0; i < tag.requiredItems.Count; i += 4) {
                    for (int j = 0; j < 4; j++) {
                        var drawRect = new Rect(tagRect.x + UIC.SPACED_HEIGHT * j, y + (i / 4.0f) * UIC.SPACED_HEIGHT, UIC.SPACED_HEIGHT, UIC.SPACED_HEIGHT);
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
            viewRect.width -= 2 * UIC.SPACED_HEIGHT;

            Rect RectForTag(int tIdx) {
                var elem = elements[tIdx];
                var offset = elements
                    .GetRange(0, tIdx)
                    .Sum(element => UIC.SPACED_HEIGHT * Mathf.Max(1, Mathf.CeilToInt(element.Tag.requiredItems.Count / 4.0f)));

                return new Rect(viewRect.x, viewRect.y + offset, viewRect.width - $" {elem.StateName} ".GetWidthCached() + 8, UIC.SPACED_HEIGHT * Mathf.Max(1, (Mathf.CeilToInt(elem.Tag.requiredItems.Count / 4.0f))));
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
                    if (Prefs.data.customCursorEnabled) {
                        Cursor.SetCursor(CustomCursor.CursorTex, CustomCursor.CursorHotspot, CursorMode.Auto);
                    } else {
                        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
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
                    Cursor.SetCursor(Textures.DragCursorTex, CustomCursor.CursorHotspot, CursorMode.ForceSoftware);
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
                Find.WindowStack.Add(new Dialog_TagSelector(LoadoutManager.Tags.Except(elements.Select(elem => elem.Tag)).ToList(), tag => {
                    if (!LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList)) {
                        pList = new SerializablePawnList(new List<Pawn>());
                        LoadoutManager.PawnsWithTags.Add(tag, pList);
                    }

                    pList.pawns.Add(pawn);
                    component.Loadout.elements.Add(new LoadoutElement(tag, null));
                }));
            }

            if (Widgets.ButtonText(buttonRect.RightHalf(), coveragePanel.ShouldDraw ? Strings.HideCoverage : Strings.ShowCoverage)) {
                // Active -> Inactive, reset back to default widths
                if (coveragePanel.ShouldDraw) {
                    this.windowRect.width = (pawnStatPanel.ShouldDraw ? Panel_PawnStats.WIDTH : 0) + WIDTH;
                }

                coveragePanel.ShouldDraw = !coveragePanel.ShouldDraw;
            }
        }

        public void DrawStatistics(Rect rect) {
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, statsHeight);

            //Widgets.DrawBoxSolid(rect, Color.green);

            var height = 0f;
            Widgets.BeginScrollView(rect, ref statsScroll, viewRect);

            GUIUtility.ListSeperator(ref viewRect, Strings.LoadoutStatistics);

            viewRect.AdjVertBy(GenUI.GapTiny);

            var loadoutItems = component.Loadout.AllItems.ToList();

            GUIUtility.BarWithOverlay(
                viewRect.PopTopPartPixels(UIC.SPACED_HEIGHT),
                Utility.HypotheticalEncumberancePercent(pawn, loadoutItems),
                Utility.HypotheticalUnboundedEncumberancePercent(pawn, loadoutItems) > 1f
                    ? Textures.ValvetTex as Texture2D
                    : Textures.RWPrimaryTex as Texture2D,
                Strings.Weight,
                Utility.HypotheticalGearAndInventoryMass(pawn, loadoutItems).ToString("0.#") + "/" +
                MassUtility.Capacity(pawn).ToStringMass(),
                Strings.WeightOverCapacity);

            height += GenUI.GapTiny + UIC.SPACED_HEIGHT;

            // var statBonuses = new Dictionary<StatDef, List<Item>>();
            // var wornApparel = component.Loadout.HypotheticalWornApparel(pawn.RaceProps.body).ToList();
            // var heldItems = loadoutItems.Where(item => !item.Def.IsApparel).ToList();
            //
            // foreach (var potentialStatBoost in wornApparel.Union(heldItems))
            // {
            //     if (!potentialStatBoost.Def?.equippedStatOffsets?.Any() ?? true) continue;
            //
            //     foreach (var mod in potentialStatBoost.Def.equippedStatOffsets.Select(m => m.stat)) {
            //         if (!statBonuses.TryGetValue(mod, out var list)) {
            //             statBonuses.Add(mod, new List<Item> { potentialStatBoost });
            //             continue;
            //         }
            //         list.Add(potentialStatBoost);
            //     }
            // }
            //
            // var apparelBonuses = new List<StatDef> {
            //     StatDefOf.SharpDamageMultiplier, StatDefOf.ArmorRating_Blunt, StatDefOf.ArmorRating_Heat, 
            //     StatDefOf.Insulation_Cold, StatDefOf.Insulation_Heat
            // };
            //
            // foreach (var stat in apparelBonuses) {
            //     statBonuses.Add(stat, wornApparel);
            // }
            //
            // foreach (var statBonus in statBonuses) {
            //     TryDrawSpoofStatCalculations(ref viewRect, statBonus.Key, statBonus.Value);
            //     height += GUIUtility.SPACED_HEIGHT;
            // }

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