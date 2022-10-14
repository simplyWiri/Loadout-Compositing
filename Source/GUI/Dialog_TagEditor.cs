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
            var pawns = Find.Maps.SelectMany(map => map.mapPawns.AllPawns.Where(p => p.IsValidLoadoutHolder())).ToList();

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
                if (Widgets.ButtonImageFitted(removeButton.ContractedBy(1f), TexButton.DeleteX)) {
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
                var optionRect = topRect.LeftPartPixels(UIC.DEFAULT_HEIGHT);
                GUI.DrawTexture(topRect.LeftPartPixels(UIC.DEFAULT_HEIGHT), tex, ScaleMode.ScaleToFit, true, 1f,
                    Color.white, 0f, 0f);
                TooltipHandler.TipRegion(optionRect, tooltip);
                if (Widgets.ButtonInvisible(optionRect)) {
                    curState = state;
                    defFilter = string.Empty;
                    lastDefFilter = defFilter;
                    lastDefList = null;
                    GUI.FocusControl("Def List Filter");
                }

                topRect.x += UIC.SPACED_HEIGHT;
            }

            r.AdjVertBy(UIC.DEFAULT_HEIGHT);

            DrawOptionButton(Textures.ApparelTex, "Apparel", State.Apparel);
            DrawOptionButton(Textures.MeleeTex, "Melee", State.Melee);
            DrawOptionButton(Textures.RangedTex, "Ranged", State.Ranged);
            DrawOptionButton(Textures.MedicalTex, "Medicine", State.Medicinal);
            DrawOptionButton(Textures.MiscItemsTex, "Items", State.Items);

            switch (curState) {
                case State.Apparel:
                    DrawDefList(r, Utility.apparelDefs);
                    break;
                case State.Melee:
                    DrawDefList(r, Utility.meleeWeapons);
                    break;
                case State.Ranged:
                    DrawDefList(r, Utility.rangedWeapons);
                    break;
                case State.Medicinal:
                    DrawDefList(r, Utility.medicinalDefs);
                    break;
                case State.Items:
                    DrawDefList(r, Utility.items);
                    break;
            }
        }

        private void DrawDefList(Rect r, IReadOnlyList<ThingDef> defList) {
            var itms = curTag.requiredItems.Select(it => it.Def).ToHashSet();
            List<ThingDef> defs = null;

            // todo: cleanup
            var slotsUsed = ApparelSlotMaker.Create(BodyDefOf.Human, curTag.requiredItems.Select(s => s.Def).Where(def => def.IsApparel).ToList());

            if (defFilter != string.Empty) {
                if (defFilter == lastDefFilter) {
                    defs = lastDefList.Where(t => !itms.Contains(t)).ToList();
                    defs = defs.Where(t => (t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, t)) || !t.IsApparel)).ToList();
                } else {
                    var filter = defFilter.ToLower();
                    var acceptedLayers = DefDatabase<ApparelLayerDef>.AllDefsListForReading.Where(l => l.LabelCap.ToString().ToLower().Contains(filter));
                    var stats = DefDatabase<StatDef>.AllDefsListForReading.Where(s => s.LabelCap.ToString()?.ToLowerInvariant().Contains(filter) ?? false).ToHashSet();

                    defs = defList.Where(t => !itms.Contains(t)).ToList();
                    defs = defs.Where(t => t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, t)) || !t.IsApparel).ToList();
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
                defs = defList.Where(t => !itms.Contains(t)).ToList();
                defs = defs.Where(t => (t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, t)) || !t.IsApparel)).ToList();
            }

            var inputFieldRect = r.PopTopPartPixels(UIC.SPACED_HEIGHT).ContractedBy(2f);
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

            for (int i = 0; i < defs.Count; i++) {
                if (!rect.Overlaps(viewFrustum)) {
                    rect.y += UIC.DEFAULT_HEIGHT;
                    continue;
                }

                var descRect = rect.LeftPart(0.85f);
                var def = defs[i];

                Widgets.DefIcon(descRect.LeftPart(.15f), def);
                Widgets.Label(descRect.RightPart(.85f), def.LabelCap);
                TooltipHandler.TipRegion(rect, def.DescriptionDetailed);

                if (Widgets.ButtonInvisible(descRect)) {
                    AddDefToTag(def);
                }

                if (Widgets.ButtonImageFitted(rect.RightPart(0.15f).ContractedBy(2f), TexButton.Info)) {
                    var stuff = def.MadeFromStuff ? GenStuff.AllowedStuffsFor(def).First() : null;
                    Find.WindowStack.Add(new Dialog_InfoCard(def, stuff));
                }

                if (i % 2 == 0)
                    Widgets.DrawLightHighlight(rect);

                Widgets.DrawHighlightIfMouseover(rect);

                rect.y += UIC.DEFAULT_HEIGHT;
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