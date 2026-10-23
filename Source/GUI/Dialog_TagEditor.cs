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
            Items,
            Prosthetics

        }

        // which things the def list is limited to
        enum ShowOnly {
            All,
            Researched,
            // in storage, or carried, worn or equipped by a colonist
            InColony
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

        private ShowOnly showOnly = ShowOnly.All;
        private HashSet<ThingDef> colonyDefs = null;
        private float colonyDefsTime = -1;
        private static Dictionary<ThingDef, float> statCache = new Dictionary<ThingDef, float>();
        private SortBy sortedByKind = SortBy.Name;
        private bool ascending = false;
        private StatDef selectedStat = null;
        // a prosthetic's effect being sorted by instead of a stat, see `ProstheticUtility.EffectsOf`
        private string selectedEffect = null;
        private float statLength = UIC.DEFAULT_HEIGHT;
        private Color conflictingApparelColour = new Color(.75f, 0.2f, 0.0f, .8f);
        private ThingDef hoveredDef = null;
        // when set, clicking a def in the def list adds it as an alternative of this item instead of a new item
        private Item addingAlternativesTo = null;
        // the group whose alternative at `draggingMemberIdx` is being dragged to change its preference
        private Item draggingMembersOf = null;
        private int draggingMemberIdx = -1;

        private LoadoutElement massAssignElement = null;
        private LoadoutElement dummySavedElement = null;
        
        
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
            selectedEffect = null;
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
                if (!collapsedPanel && massAssignElement is null) {
                    massAssignElement = new LoadoutElement(curTag, null);
                }
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

            var height = (1 + pawns.Count) * UIC.DEFAULT_HEIGHT;
            var width = rect.width - (rect.height > height ? 0 : UIC.SCROLL_WIDTH);
            var viewRect = new Rect(rect.x, rect.y, width, height);
            
            Widgets.BeginScrollView(rect, ref pawnListScroll, viewRect);


            // We want to draw a little gizmo below the heading so people can select a `State` to assign, along with the
            // tag.
            var loadoutStateRect = viewRect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            
            GUI.color = Color.gray;
            Dialog_SetTagLoadoutState.Draw(loadoutStateRect, massAssignElement);
            GUI.color = Color.white;
            
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
                var hasThing = component.Loadout.AllElements.Any(e => e.Equivalent(massAssignElement));

                Widgets.Label(pRect, pawn.LabelShort);
                
                if (GUIUtility.DraggableCheckbox(pRect, pRect.PopRightPartPixels(UIC.DEFAULT_HEIGHT), ref hasThing)) {
                    if (!hasThing) {
                        component.RemoveTag(component.Loadout.AllElements.FirstOrDefault(elem => elem.Tag == curTag));
                    } else {
                        component.AddTag(curTag, massAssignElement.State, massAssignElement.ActiveCondition);  
                    }
                }
            }
            
            Widgets.EndScrollView();
        }


        public void DrawTagEditor(Rect r) {
            _ = r.PopRightPartPixels(this.Margin);
            var topRect = r.TopPartPixels(UIC.DEFAULT_HEIGHT);

            if (Widgets.ButtonText(topRect.LeftPart(0.33f), Strings.SelectTag)) {
                Find.WindowStack.Add(new Dialog_TagSelector(LoadoutManager.Tags.Except(curTag).ToList(), tag =>
                {
                    curTag = tag;
                    massAssignElement = new LoadoutElement(curTag, null);
                    dummySavedElement = null;
                }, false));
            }

            topRect.AdjHorzBy(topRect.width * 0.33f);
            if (Widgets.ButtonText(topRect.LeftHalf(), Strings.CreateNewTag)) {
                curTag = new Tag(string.Empty);
                LoadoutManager.AddTag(curTag);
            }

            if (curTag != null && Widgets.ButtonText(topRect.RightHalf(), Strings.DeleteTag)) {
                Find.WindowStack.Add(new Dialog_Confirm($"{Strings.DeleteTag} {curTag.name}", () => {
                    LoadoutManager.RemoveTag(curTag);
                    curTag = null;
                }));
            }

            r.AdjVertBy(UIC.DEFAULT_HEIGHT);

            if (curTag == null) return;

            Text.Anchor = TextAnchor.UpperRight;

            var defaultOnRect = r.TopPartPixels(30);
            defaultOnRect.PopTopPartPixels(3);
            if (curTag.defaultEnabled) {
                var existingState = LoadoutManager.States.FirstOrDefault(e => e.name == curTag.defaultState);
                dummySavedElement ??= new LoadoutElement(curTag, existingState, curTag.defaultCondition);
                
                GUI.color = Color.gray;
                var rightPart = defaultOnRect
                    .PopRightPartPixels(Dialog_SetTagLoadoutState.GetWidthAbsolute(dummySavedElement))
                    .TopPartPixels(20);
                Dialog_SetTagLoadoutState.Draw(rightPart, dummySavedElement);
                GUI.color = Color.white;
                
                curTag.defaultState = dummySavedElement.State?.name ?? null;
                curTag.defaultCondition = dummySavedElement.ActiveCondition;
            }
            
            var defaultOnRectButton = defaultOnRect.PopRightPartPixels(20);
            defaultOnRect.PopRightPartPixels(5);

            GUIUtility.DraggableCheckbox(defaultOnRectButton, defaultOnRectButton, ref curTag.defaultEnabled);
            GUI.color = Color.gray;
            TooltipHandler.TipRegion(defaultOnRect, Strings.EnableTagByDefaultDesc);
            Widgets.Label(defaultOnRect, Strings.EnableTagByDefault);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperRight;

            Widgets.ListSeparator(ref r.m_YMin, r.width, Strings.Modify + " " + curTag.name);

            // [ Tag Name ] [ Edit Name ] 
            var rect = r.PopTopPartPixels(UIC.DEFAULT_HEIGHT);

            GUIUtility.InputField(rect, Strings.ChangeTagName, ref curTag.name);
            curTag.name ??= " ";

            var viewRect = new Rect(r.x, r.y, rect.width - UIC.SCROLL_WIDTH,
                curTag.requiredItems.Sum(ItemRowHeight));
            Widgets.BeginScrollView(r, ref curItemScroll, viewRect);

            var visibilityRect = r;
            visibilityRect.y += curItemScroll.y;
            var baseRect = viewRect;

            var prostheticSlots = new ProstheticSlots(BodyDefOf.Human, curTag.ItemsWithTagMatching(item => item.IsProsthetic));

            var i = 0;
            // List each item in the currently required items
            // [ Info ] [ Icon ] [ Name ] [ Edit Filter ] [ Edit Quantity ] [ Remove ]  
            foreach (var item in curTag.requiredItems.ToList()) {
                var def = item.RepresentativeDef;
                var blockRect = baseRect.PopTopPartPixels(ItemRowHeight(item));

                if (!blockRect.Overlaps(visibilityRect)) {
                    continue;
                }

                if (item == addingAlternativesTo) {
                    Widgets.DrawHighlightSelected(blockRect);
                } else if (i % 2 == 0) {
                    Widgets.DrawLightHighlight(blockRect);
                }
                i++;

                var itemRect = blockRect.PopTopPartPixels(UIC.SPACED_HEIGHT * 2);
                if (ShowsMemberRows(item)) {
                    DrawMemberRows(blockRect, item);
                }

                // Info
                Rect infoRect = itemRect.PopLeftPartPixels(UIC.SMALL_ICON);
                if (Widgets.ButtonImageFitted(infoRect.ContractedBy(1f), TexButton.Info)) {
                    Find.WindowStack.Add(new Dialog_InfoCard(def, item.RepresentativeStuff));
                }

                // Icon
                var iconRect = itemRect.PopLeftPartPixels(UIC.SPACED_HEIGHT * 2);
                // a group's tooltip defaults to listing its alternatives
                var iconTooltip = item.HasAlternatives ? null : def.DescriptionDetailed;
                if (item.IsProsthetic) {
                    iconTooltip += "\n\n" + Strings.InstalledOn(ProstheticUtility.TargetPartsLabel(BodyDefOf.Human, def));
                }
                GUIUtility.DrawItemIcon(iconRect, item, tooltip: iconTooltip);

                // Remove
                var removeButton = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 1.5f);
                if (Widgets.ButtonImageFitted(removeButton.ContractedBy(1f), TexButton.Delete)) {
                    curTag.requiredItems.Remove(item);
                }

                TooltipHandler.TipRegion(removeButton, Strings.RemoveItemFromTag);

                // Copy, Paste
                var copyPasteButton = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 3);
                var pasteTarget = item;
                GUIUtility.DraggableCopyPaste(copyPasteButton, ref pasteTarget, Item.PasteFilter);
                TooltipHandler.TipRegion(copyPasteButton, Strings.CopyPasteExplain);

                // Add alternative, prosthetics can't have any. The space is kept so quantities line up
                var alternativeButton = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 1.5f);
                if (!item.IsProsthetic) {
                    if (Widgets.ButtonImageFitted(alternativeButton.ContractedBy(4f), TexButton.Plus)) {
                        addingAlternativesTo = item;
                    }
                    TooltipHandler.TipRegion(alternativeButton, Strings.AddAlternativeDesc);
                }

                // Edit, for a group only if one of its alternatives has a quality or stuff to pick. The space is kept
                // so quantities line up
                var constrainButton = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 1.5f);
                var editMember = SpecifiableMember(item);
                if (editMember != null) {
                    if (Widgets.ButtonImageFitted(constrainButton.ContractedBy(1f), Textures.EditTex))
                        Find.WindowStack.Add(new Dialog_ItemSpecifier(item, editMember));
                    TooltipHandler.TipRegion(constrainButton, Strings.SpecifyElementsToolTip);
                }

                var quantityFieldRect = itemRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 2f);
                item.quantityStr ??= item.Quantity.ToString();
                // no more prosthetics than a body has parts for
                var maxQuantity = item.IsProsthetic ? ProstheticUtility.MaxInstallable(BodyDefOf.Human, def) : 1E+09f;
                Widgets.TextFieldNumeric(quantityFieldRect.ContractedBy(0, quantityFieldRect.height / 4.0f), ref item.quantity, ref item.quantityStr, 1, maxQuantity);

                TooltipHandler.TipRegion(quantityFieldRect, Strings.EditQuantity);

                // Name, a group's alternatives are listed underneath its heading
                if (ShowsMemberRows(item)) {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    Widgets.Label(itemRect.RightPartPixels(itemRect.width - GenUI.GapSmall), Strings.OneOfHeading);
                } else {
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(itemRect, item.IsProsthetic ? Strings.InstalledLabel(item.Label) : item.Label);
                    if (prostheticSlots.Conflicting(item)) {
                        GUIUtility.ProstheticConflictOverlay(itemRect, prostheticSlots.Conflicts[item]);
                    }
                }
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            DraggableItems(viewRect, curTag.requiredItems);

            Widgets.EndScrollView();
        }

        private static bool ShowsMemberRows(Item item) => item.HasAlternatives;

        // the member the item's edit dialog opens on, preferring one made from stuff (stuff is set per member, the
        // dialog can switch between them); null if a group has nothing but hit points to pick
        private static ItemMember SpecifiableMember(Item item) {
            // nothing about a prosthetic changes once it is installed
            if (item.IsProsthetic) return null;
            if (item.Members.Count == 1) return item.Members[0];

            return item.Members.FirstOrDefault(m => m.Def.MadeFromStuff)
                   ?? item.Members.FirstOrDefault(m => m.Def.HasComp(typeof(CompQuality)));
        }

        private static float ItemRowHeight(Item item) {
            return UIC.SPACED_HEIGHT * 2 + (ShowsMemberRows(item) ? item.Members.Count * UIC.SPACED_HEIGHT : 0);
        }

        // one row per alternative, in order of preference, dragged to reorder
        // [ Priority ] [ Icon ] [ Name ] [ Remove ]
        private void DrawMemberRows(Rect rect, Item item) {
            var members = item.members;
            var rowRects = new List<Rect>();
            for (var idx = 0; idx < members.Count; idx++) {
                var member = members[idx];
                var rowRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
                rowRect.xMin += UIC.SMALL_ICON;
                rowRects.Add(rowRect);

                // priority, coloured as the loadout editor colours tags by priority
                var priorityRect = rowRect.PopLeftPartPixels(UIC.SMALL_GAP).ContractedBy(0, 1f);
                Widgets.DrawBoxSolid(priorityRect, Panel_ShowCoverage.GetColorForTagAtIndex(idx));
                if (Mouse.IsOver(rowRect)) {
                    var alpha = Mathf.Sin((Time.time % 2) / 2.0f * Mathf.PI) * 0.3f;
                    GUI.color = new Color(0, 0, 0, 0.7f + alpha);
                    Widgets.DrawTexturePart(priorityRect, new Rect(1 / 4f, 0, 1 / 2f, 1), Textures.DraggableTex);
                    GUI.color = Color.white;
                }
                rowRect.PopLeftPartPixels(GenUI.GapTiny);

                var iconRect = rowRect.PopLeftPartPixels(UIC.SPACED_HEIGHT);
                GUIUtility.DrawItemIcon(iconRect.ContractedBy(2f), item, false, member.Def.DescriptionDetailed, member, badge: false);

                // removing the last-but-one alternative leaves a plain item
                var removeRect = rowRect.PopRightPartPixels(UIC.SPACED_HEIGHT * 1.5f);
                TooltipHandler.TipRegion(removeRect, Strings.RemoveAlternative);
                if (Widgets.ButtonImageFitted(removeRect.ContractedBy(5f), TexButton.Delete)) {
                    members.RemoveAt(idx);
                    return;
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(rowRect, member.ListLabel);
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(rowRect, Strings.ReorderAlternativeDesc);
            }

            DraggableMembers(item, rowRects);
        }

        // as `DraggableItems`, swapping a dragged alternative with its neighbour as the mouse passes them
        private void DraggableMembers(Item item, List<Rect> rowRects) {
            var members = item.members;
            var cEvent = Event.current;

            // also stop if the group shrank under the drag
            if (cEvent.rawType == EventType.MouseUp || (draggingMembersOf == item && draggingMemberIdx >= rowRects.Count)) {
                draggingMembersOf = null;
                draggingMemberIdx = -1;
            }

            if (draggingMembersOf == item) {
                var area = new Rect(rowRects[0].x, rowRects[0].y, rowRects[0].width, rowRects[rowRects.Count - 1].yMax - rowRects[0].y);
                if (Mouse.IsOver(area.ExpandedBy(25f))) {
                    var tRect = rowRects[draggingMemberIdx];
                    var mPos = cEvent.mousePosition;

                    if (!tRect.ExpandedBy(5f).Contains(mPos)) {
                        if (draggingMemberIdx > 0 && mPos.y < tRect.y) {
                            (members[draggingMemberIdx], members[draggingMemberIdx - 1]) = (members[draggingMemberIdx - 1], members[draggingMemberIdx]);
                            draggingMemberIdx -= 1;
                        } else if (draggingMemberIdx < members.Count - 1 && mPos.y > tRect.y) {
                            (members[draggingMemberIdx], members[draggingMemberIdx + 1]) = (members[draggingMemberIdx + 1], members[draggingMemberIdx]);
                            draggingMemberIdx += 1;
                        }

                        SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                    }

                    Widgets.DrawLine(new Vector2(tRect.x, tRect.yMax), new Vector2(tRect.xMax, tRect.yMax), ReorderableWidget.LineColor, 2f);
                    Widgets.DrawHighlight(tRect);
                } else {
                    draggingMembersOf = null;
                    draggingMemberIdx = -1;
                }
            }

            if (cEvent.rawType != EventType.MouseDown) return;

            for (var idx = 0; idx < rowRects.Count; idx++) {
                // the remove button stays clickable
                var grabRect = rowRects[idx];
                grabRect.xMax -= UIC.SPACED_HEIGHT * 1.5f;
                if (!grabRect.Contains(cEvent.mousePosition)) continue;

                draggingMembersOf = item;
                draggingMemberIdx = idx;
                break;
            }
        }

        private void DraggableItems(Rect rect, List<Item> items) {
            if (items.Count == 1) return;
            
            // only the item's own row (not its alternatives) starts a drag, the full block is highlighted
            Rect RectForTag(int tIdx, bool full = false) 
                => new Rect(rect.x, rect.y + items.Take(tIdx).Sum(ItemRowHeight), rect.width - (full ? 0 : 9.5f * UIC.SPACED_HEIGHT),
                    full ? ItemRowHeight(items[tIdx]) : UIC.SPACED_HEIGHT * 2);

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
            var topRect = r.PopTopPartPixels(UIC.DEFAULT_HEIGHT * 2);

            void DrawOptionButton(Texture2D tex, string tooltip, State state) {
                var optionRect = topRect.PopLeftPartPixels(UIC.DEFAULT_HEIGHT * 2);
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

            DrawOptionButton(Textures.ApparelTex, "Apparel", State.Apparel);
            DrawOptionButton(Textures.MeleeTex, "Melee", State.Melee);
            DrawOptionButton(Textures.RangedTex, "Ranged", State.Ranged);
            DrawOptionButton(Textures.MedicalTex, "Medicine", State.Medicinal);
            DrawOptionButton(Textures.MiscItemsTex, "Items", State.Items);
            DrawOptionButton(Textures.ProstheticsTex, Strings.Prosthetics, State.Prosthetics);

            var sortLabel = selectedStat?.LabelCap.ToString() ?? selectedEffect;
            if (sortLabel != null) {
                Text.Font = GameFont.Tiny;
                Text.WordWrap = false;
                var strLen = sortLabel.GetWidthCached();
                topRect.width = Mathf.Max(topRect.width, strLen);
                GUIUtility.WithModifiers(topRect, sortLabel, color: Color.gray, anchor: TextAnchor.MiddleRight);
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
                    DrawDefList(r, Utility.medicinalDefs, pinned: ItemPreset.Medicine);
                    break;
                case State.Items:
                    DrawDefList(r, Utility.items, pinned: ItemPreset.Meals);
                    break;
                case State.Prosthetics:
                    DrawDefList(r, Utility.prostheticDefs);
                    break;
            }
        }

        // `pinned` is a preset shown above the def list
        private void DrawDefList(Rect r, IReadOnlyList<ThingDef> defList, List<StatDef> extraStats = null, ItemPreset pinned = null) {
            if (addingAlternativesTo != null && !curTag.requiredItems.Contains(addingAlternativesTo)) {
                addingAlternativesTo = null;
            }

            var itms = curTag.requiredItems.SelectMany(it => it.Defs).ToHashSet();
            List<ThingDef> defs = null;

            // a def is hidden if it conflicts with any member of another item in the tag, alternatives may overlap the
            // item they are an alternative of, as only one of them is worn
            var otherItems = curTag.requiredItems.Where(s => s != addingAlternativesTo);
            var slotsUsed = ApparelSlotMaker.Create(BodyDefOf.Human, otherItems.Where(s => s.IsApparel).SelectMany(s => s.Defs).ToList());

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
                               || (td.IsApparel && td.apparel.layers.Intersect(acceptedLayers).Any())
                               // e.g. "food poisoning", or "infection" for any prosthetic which prevents one
                               || ProstheticUtility.EffectsOf(td).Keys.Any(effect => effect.ToLowerInvariant().Contains(filter));
                    }).ToList();

                    lastDefFilter = defFilter;
                    lastDefList = defs;
                }
            }
            else {
                defs = defList.Where(t => (t.IsApparel && !slotsUsed.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, t)) || !t.IsApparel)).ToList();
            }
            
            defs = defs.Where(t => !itms.Contains(t)).ToList();

            // as with apparel, a prosthetic is hidden if it overlaps any of the tag's other prosthetics, e.g. any other leg
            // once the tag has a bionic leg, or a field hand once it has a bionic arm
            var prostheticSlots = new ProstheticSlots(BodyDefOf.Human, curTag.ItemsWithTagMatching(item => item.IsProsthetic));
            defs = defs.Where(t => !ProstheticUtility.IsProsthetic(t) || prostheticSlots.Fits(t)).ToList();

            if (addingAlternativesTo != null) {
                // alternatives must be the same kind of thing, and prosthetics can't be alternatives
                defs = defs.Where(t => t.IsApparel == addingAlternativesTo.IsApparel && !ProstheticUtility.IsProsthetic(t)).ToList();

                // [ Adding alternatives to X ] [ Done ], on one line, named by the item's best alternative
                var bannerRect = r.PopTopPartPixels(UIC.SPACED_HEIGHT);
                Widgets.DrawHighlightSelected(bannerRect);
                var label = Strings.AddingAlternativesTo(addingAlternativesTo.RepresentativeDef.LabelCap);

                var doneRect = bannerRect.PopRightPartPixels(Strings.Done.GetWidthCached() + UIC.SMALL_GAP * 2);
                if (Widgets.ButtonText(doneRect.ContractedBy(2f), Strings.Done)) {
                    addingAlternativesTo = null;
                }

                bannerRect.xMin += GenUI.GapTiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(bannerRect, label.Truncate(bannerRect.width));
                Text.WordWrap = true;
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(bannerRect, label);
            }
            
            if (showOnly == ShowOnly.Researched) {
                var recipes = DefDatabase<RecipeDef>.AllDefs.Where(recipe => recipe.AvailableNow).ToList();
                
                defs = defs.Where(def => def.IsBuildingArtificial || recipes.Any(recipe => recipe.products.Any(pc => pc.thingDef == def))).ToList();
                defs = defs.Where(def => !def.IsBuildingArtificial || def.IsResearchFinished).ToList();
            } else if (showOnly == ShowOnly.InColony) {
                var inColony = ColonyDefs();
                defs = defs.Where(inColony.Contains).ToList();
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
            
            var showOnlyRect = topBarRect.PopRightPartPixels(UIC.DEFAULT_HEIGHT).MiddlePartPixels(Text.LineHeight);
            showOnlyRect.CenterWithWidth(Text.LineHeight);
            // highlighted while it hides anything
            if (Widgets.ButtonImage(showOnlyRect, Textures.FilterByResearchedTex, showOnly == ShowOnly.All ? Color.white : GenUI.MouseoverColor)) {
                FloatMenuOption ShowOnlyOption(ShowOnly mode, string label) {
                    return new FloatMenuOption(label, () => showOnly = mode);
                }

                var opts = new List<FloatMenuOption> {
                    ShowOnlyOption(ShowOnly.All, Strings.ShowAllThings),
                    ShowOnlyOption(ShowOnly.Researched, Strings.ShowResearchedThings),
                };
                // the colony's things are only known in a game, e.g. not when editing tags from the main menu
                if (Current.Game != null) {
                    opts.Add(ShowOnlyOption(ShowOnly.InColony, Strings.ShowThingsInColony));
                }
                Find.WindowStack.Add(new FloatMenu(opts));
            }
            TooltipHandler.TipRegion(showOnlyRect, Strings.ShowOnlyDesc);

            topBarRect.PopRightPartPixels(3f);
            
            var sortByFilterRect = topBarRect.PopRightPartPixels(UIC.DEFAULT_HEIGHT).MiddlePartPixels(Text.LineHeight);
            sortByFilterRect.CenterWithWidth(Text.LineHeight);
            if (Widgets.ButtonImage(sortByFilterRect, ascending ? Textures.SortByStatAscTex : Textures.SortByStatDscTex)) {
                if (Event.current.button == 1) { // right click
                    var stats = defs
                                .SelectMany(td => td.statBases.Select(sm => sm.stat)
                                                    .Union(td.equippedStatOffsets?.Select(sm => sm.stat) ?? new List<StatDef>()))
                                .ToHashSet();

                    stats.AddRange(extraStats ?? new List<StatDef>());
                    
                    var opts = new List<FloatMenuOption>() {
                        new FloatMenuOption("Name", () => {
                            sortedByKind = SortBy.Name;
                            selectedStat = null;
                            selectedEffect = null;
                            statLength = UIC.DEFAULT_HEIGHT;
                        })
                    };

                    // stats and prosthetics' effects, sorted together by label
                    var statOpts = new List<FloatMenuOption>();
                    foreach (var stat in stats) {
                        statOpts.Add(new FloatMenuOption(stat.LabelCap, () => {
                            sortedByKind = SortBy.Stat;
                            selectedStat = stat;
                            selectedEffect = null;
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

                    var effects = defs.SelectMany(td => ProstheticUtility.EffectsOf(td).Keys).ToHashSet();
                    foreach (var effect in effects) {
                        statOpts.Add(new FloatMenuOption(effect, () => {
                            sortedByKind = SortBy.Stat;
                            selectedStat = null;
                            selectedEffect = effect;
                            statLength = UIC.DEFAULT_HEIGHT;

                            foreach (var def in defList) {
                                statCache[def] = ProstheticUtility.EffectsOf(def).TryGetValue(effect, out var value) ? value : INVALID_STAT_VALUE;
                            }
                        }));
                    }

                    opts.AddRange(statOpts.OrderBy(opt => opt.Label));
                    Find.WindowStack.Add(new FloatMenu(opts));
                } else {
                    ascending = !ascending;
                }
            }
            
            TooltipHandler.TipRegion(sortByFilterRect, Strings.SortThingsByDesc);

            var inputFieldRect = topBarRect.ContractedBy(2f);
            GUIUtility.InputField(inputFieldRect, "Def List Filter", ref defFilter);
            TooltipHandler.TipRegion(inputFieldRect, Strings.SearchBarDesc);

            // hidden while any of its defs are already in the tag, as a def can only be in one item
            if (pinned != null && addingAlternativesTo == null && pinned.Defs.Any() && !pinned.Defs.Any(itms.Contains)) {
                DrawPinnedPreset(r.PopTopPartPixels(UIC.DEFAULT_HEIGHT), pinned);
            }

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

                if (Mouse.IsOver(rowRect) && (def.IsApparel || ProstheticUtility.IsProsthetic(def)))
                {
                    hoveredDef = def;
                    conflictingApparelColour.a = (Mathf.Sin((float)(DateTime.Now.TimeOfDay.TotalMilliseconds % 3000) / 3000 * Mathf.PI * 2) * 0.25f) + 0.75f;
                }
                
                TooltipHandler.TipRegion(rowRect, () =>
                {
                    if (def.IsApparel)
                    {
                        return $"{def.DescriptionDetailed}\n\n" + Strings.ApparelOverlapFlavour(def.LabelCap);
                    }

                    if (ProstheticUtility.IsProsthetic(def))
                    {
                        return $"{def.DescriptionDetailed}\n\n" + Strings.InstalledOn(ProstheticUtility.TargetPartsLabel(BodyDefOf.Human, def))
                               + "\n\n" + Strings.ProstheticOverlapFlavour(def.LabelCap);
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
                var coveringSlots = hoveredDef.IsApparel ? ApparelSlotMaker.Create(BodyDefOf.Human, hoveredDef) : null;
                
                // apparel covering the same layers, or prosthetics replacing the same body parts
                bool Conflicts(ThingDef otherDef) {
                    if (coveringSlots != null) return otherDef.IsApparel && coveringSlots.Intersects(ApparelSlotMaker.Create(BodyDefOf.Human, otherDef));
                    return ProstheticUtility.IsProsthetic(otherDef) && ProstheticSlots.Overlaps(BodyDefOf.Human, hoveredDef, otherDef);
                }

                void DrawHighlightIfConflicts(ThingDef otherDef, Rect rect)
                {
                    if (!Conflicts(otherDef))
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

        // the defs of things in storage, or carried, worn or equipped by colonists, on any map. Looked up at most once a
        // second, as there may be thousands of things
        private HashSet<ThingDef> ColonyDefs() {
            if (colonyDefs != null && Time.realtimeSinceStartup - colonyDefsTime < 1f) return colonyDefs;

            colonyDefs = new HashSet<ThingDef>();
            colonyDefsTime = Time.realtimeSinceStartup;
            if (Current.Game == null) return colonyDefs;

            foreach (var map in Find.Maps) {
                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.HaulableEver).Where(t => t.IsInAnyStorage())) {
                    colonyDefs.Add(thing.def);
                }

                foreach (var pawn in map.mapPawns.FreeColonistsSpawned) {
                    colonyDefs.AddRange(pawn.inventory?.innerContainer.Select(t => t.def) ?? Enumerable.Empty<ThingDef>());
                    colonyDefs.AddRange(pawn.apparel?.WornApparel.Select(t => t.def) ?? Enumerable.Empty<ThingDef>());
                    colonyDefs.AddRange(pawn.equipment?.AllEquipmentListForReading.Select(t => t.def) ?? Enumerable.Empty<ThingDef>());
                }
            }

            return colonyDefs;
        }

        private void DrawPinnedPreset(Rect rowRect, ItemPreset preset) {
            var descRect = rowRect.LeftPart(0.85f);
            Widgets.DefIcon(descRect.LeftPart(.15f), preset.Defs.First());
            Widgets.Label(descRect.RightPart(.85f), preset.Label);

            Widgets.DrawLightHighlight(rowRect);
            Widgets.DrawHighlightIfMouseover(rowRect);
            TooltipHandler.TipRegion(rowRect, () => Strings.ItemPresetDesc + "\n\n" + preset.MakeItem().RankedMembersDescription(), preset.GetHashCode());

            if (Widgets.ButtonInvisible(rowRect)) {
                curTag.requiredItems.Add(preset.MakeItem());
            }
        }

        private void AddDefToTag(ThingDef def) {
            if (addingAlternativesTo != null) {
                addingAlternativesTo.AddAlternative(def);
                return;
            }

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