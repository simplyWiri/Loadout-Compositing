using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Inventory {

    // Every colonist's tags in one table, for managing the tags of a large colony
    // [ Search ] [ Maps ]                          View as if [ State ] were active
    // [ Name ] [ State ] [ Tag ] [ Tag ] [ Tag ] [ + ]            [ Copy ] [ Paste ] [ Edit ]
    public class MainTabWindow_LoadoutOverview : MainTabWindow {

        private const float NAME_WIDTH = 130f;
        private const float STATE_WIDTH = 110f;
        private const float CHIP_HEIGHT = 24f;
        private const float CHIP_GAP = 4f;
        private const float CHIP_PADDING = 6f;
        private const float STRIP_WIDTH = 5f;
        private const float REMOVE_WIDTH = 16f;
        // copy, paste & edit
        // the width of the strip which shows or hides the pawn stats
        private const float COLLAPSE_WIDTH = 16f;
        // how often (in frames) the list of pawns is found again
        private const int REFRESH_INTERVAL = 60;
        // how far the mouse moves before a press on a tag starts dragging it
        private const float DRAG_THRESHOLD = 5f;
        // how close to the top or bottom of the table a dragged tag scrolls it, and how fast
        private const float SCROLL_MARGIN = 30f;
        private const float SCROLL_SPEED = 600f;
        // the colour of the overlay on conflicting prosthetics, see `GUIUtility.ProstheticConflictOverlay`
        private static readonly Color WarningColour = new Color(.75f, 0.2f, 0.0f, .35f);

        // which pawns are listed
        private enum Scope {
            CurrentMap,
            AllMaps,
            // all maps, and caravans
            Everywhere
        }

        private enum SortBy {
            Name,
            State,
            Tags
        }

        // what's shown for a pawn which takes a while to work out, see `InfoFor`
        private class RowInfo {
            public int signature;
            // why a tag is marked, e.g. its prosthetics conflicting with another tag's
            public Dictionary<LoadoutElement, string> warnings = new Dictionary<LoadoutElement, string>();
        }

        // a tag being dragged from a pawn's tags
        private class TagDrag {
            public Pawn fromPawn;
            public LoadoutElement element;
            public Vector2 start;
            public bool started;
        }

        private List<Pawn> pawns = new List<Pawn>();
        private int pawnsFoundFrame = -1;
        private Pawn focused = null;
        // the pawns acted on together, see `ActedOn`
        private HashSet<Pawn> selected = new HashSet<Pawn>();
        private Dictionary<Pawn, RowInfo> infos = new Dictionary<Pawn, RowInfo>();

        private QuickSearchWidget search = new QuickSearchWidget();
        private Scope scope = Scope.AllMaps;
        private SortBy sortBy = SortBy.Name;
        private bool descending = false;
        // shows tags as if this state were active, rather than each pawn's current state
        private bool overrideState = false;
        private LoadoutState shownState = null;

        private Vector2 tableScroll = Vector2.zero;
        private float tableHeight = 0f;

        private readonly Panel_PawnStats statsPanel;
        private bool showStats = false;

        private TagDrag drag = null;
        // where the dragged tag would be dropped, found while drawing the rows
        private Pawn dropPawn = null;
        private int dropIndex = -1;
        private Vector2 mousePosition;

        private bool Dragging => drag is { started: true };

        private IEnumerable<Pawn> Selected => pawns.Where(selected.Contains);

        public override Vector2 RequestedTabSize => new Vector2(1500f, Mathf.Max(480f, 140f + pawns.Count * UIC.SPACED_HEIGHT));

        public MainTabWindow_LoadoutOverview() {
            statsPanel = new Panel_PawnStats(() => focused, () => ShownStateFor(focused), () => StepFocus(-1), () => StepFocus(1), true);
        }

        public override void PreOpen() {
            FindPawns();
            focused = Find.Selector.SelectedPawns.FirstOrDefault(pawns.Contains) ?? focused;
            if (!pawns.Contains(focused)) {
                focused = pawns.FirstOrDefault();
            }
            selected.Clear();
            if (focused != null) {
                selected.Add(focused);
            }

            // sets the size, so after the pawns are found
            base.PreOpen();
        }

        private void FindPawns() {
            pawnsFoundFrame = Time.frameCount;
            // also catches changes the signatures don't, e.g. to an apparel policy
            infos.Clear();

            var maps = scope == Scope.CurrentMap ? new List<Map> { Find.CurrentMap }.Where(m => m != null) : Find.Maps;
            var found = maps.SelectMany(map => map.mapPawns.AllPawns);
            if (scope == Scope.Everywhere) {
                found = found.Concat(Find.WorldObjects.Caravans.SelectMany(caravan => caravan.PawnsListForReading));
            }

            found = found
                .Where(p => p.IsValidLoadoutHolder())
                .Distinct()
                .Where(p => !search.filter.Active || search.filter.Matches(p.LabelShort)
                    || p.GetComp<LoadoutComponent>().Loadout.AllTags.Any(t => search.filter.Matches(t.name)));

            IOrderedEnumerable<Pawn> sorted = sortBy switch {
                SortBy.State => found.OrderBy(p => p.GetActiveState()?.name ?? string.Empty),
                SortBy.Tags => found.OrderBy(p => p.GetComp<LoadoutComponent>().Loadout.elements.Count),
                _ => found.OrderBy(p => p.LabelShort)
            };
            pawns = sorted.ThenBy(p => p.LabelShort).ToList();
            if (descending) {
                pawns.Reverse();
            }

            if (!pawns.Contains(focused)) {
                focused = pawns.FirstOrDefault();
            }
            selected.RemoveWhere(p => !pawns.Contains(p));
        }

        // a change to `pawn` applies to every selected pawn, if it is one of them
        private IEnumerable<Pawn> ActedOn(Pawn pawn) {
            return selected.Contains(pawn) ? pawns.Where(selected.Contains) : new[] { pawn };
        }

        private void Select(Pawn pawn) {
            if (Event.current.control) {
                if (!selected.Remove(pawn)) {
                    selected.Add(pawn);
                }
            } else if (Event.current.shift && focused != null) {
                var from = pawns.IndexOf(focused);
                var to = pawns.IndexOf(pawn);
                for (int i = Mathf.Min(from, to); i <= Mathf.Max(from, to); i++) {
                    selected.Add(pawns[i]);
                }
            } else {
                selected.Clear();
                selected.Add(pawn);
                if (Event.current.clickCount == 2) {
                    CameraJumper.TryJumpAndSelect(pawn);
                }
            }

            focused = pawn;
        }

        // the state the pawn's tags are shown as if it were active
        private LoadoutState ShownStateFor(Pawn pawn) => overrideState ? shownState : pawn?.GetActiveState();

        // worked out again once the pawn's tags, or the state they're shown in, change
        private RowInfo InfoFor(Pawn pawn) {
            var loadout = pawn.GetComp<LoadoutComponent>().Loadout;
            var state = ShownStateFor(pawn);
            var signature = loadout.elements.Aggregate(state?.GetHashCode() ?? 17,
                (hash, e) => (hash * 31 + e.Tag.GetHashCode()) * 31 + (e.State?.GetHashCode() ?? 0) + (int)e.ActiveCondition);

            if (infos.TryGetValue(pawn, out var info) && info.signature == signature) return info;

            info = new RowInfo { signature = signature };
            infos[pawn] = info;

            var prosthetics = loadout.HypotheticalProsthetics(state, pawn.RaceProps.body);
            var policy = pawn.outfits?.CurrentApparelPolicy;
            foreach (var element in loadout.elements.Where(e => e.Active(state))) {
                var reasons = new List<string>();
                foreach (var item in element.Tag.requiredItems.Where(prosthetics.Conflicting)) {
                    var blocker = prosthetics.Conflicts[item];
                    reasons.Add(blocker == null ? Strings.ProstheticNoRoom : Strings.ProstheticConflict(blocker.name));
                }

                var disallowed = element.Tag.requiredItems.Where(item => item.IsApparel).SelectMany(item => item.Defs)
                    .FirstOrDefault(def => policy != null && !policy.filter.Allows(def));
                if (disallowed != null) {
                    reasons.Add(Strings.OutfitDisallowsKit(pawn, policy, disallowed, element.Tag));
                }

                if (reasons.Any()) {
                    info.warnings[element] = string.Join("\n", reasons);
                }
            }

            return info;
        }

        private void StepFocus(int by) {
            if (pawns.Count == 0) return;

            var idx = pawns.IndexOf(focused);
            focused = pawns[GenMath.PositiveMod(idx + by, pawns.Count)];
            selected.Clear();
            selected.Add(focused);
        }

        public override void DoWindowContents(Rect inRect) {
            // not while dragging, as rows moving around under the mouse would be confusing
            if (!Dragging && Time.frameCount - pawnsFoundFrame > REFRESH_INTERVAL) {
                FindPawns();
            }

            Text.Font = GameFont.Small;
            mousePosition = Event.current.mousePosition;
            UpdateDrag();

            // [ Stats ] [ < ] [ Toolbar, table ]
            if (showStats) {
                statsPanel.Draw(inRect.PopLeftPartPixels(Panel_PawnStats.WIDTH));
                inRect.PopLeftPartPixels(GenUI.GapTiny);
            }
            DrawCollapseStrip(inRect.PopLeftPartPixels(COLLAPSE_WIDTH));
            inRect.PopLeftPartPixels(GenUI.GapTiny);

            DrawToolbar(inRect.PopTopPartPixels(UIC.SPACED_HEIGHT));
            inRect.AdjVertBy(GenUI.GapTiny);

            DrawTable(inRect);

            if (Dragging) {
                DrawDragged();
            }
            FinishDrag();
        }

        public override void OnCancelKeyPressed() {
            if (drag != null) {
                Event.current.Use();
                EndDrag();
                return;
            }

            base.OnCancelKeyPressed();
        }

        public override void PostClose() {
            base.PostClose();
            EndDrag();
        }

        private void StartDrag(Pawn fromPawn, LoadoutElement element) {
            drag = new TagDrag { fromPawn = fromPawn, element = element, start = mousePosition };
            Event.current.Use();
        }

        private void UpdateDrag() {
            dropPawn = null;
            dropIndex = -1;
            if (drag == null || drag.started || (mousePosition - drag.start).magnitude < DRAG_THRESHOLD) return;

            drag.started = true;
            GUIUtility.SetDragCursor();
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
        }

        // drops the tag once the mouse is let go, even outside of the window
        private void FinishDrag() {
            if (drag == null) return;
            if (Event.current.rawType != EventType.MouseUp && Input.GetMouseButton(0)) return;

            if (Dragging) {
                Drop();
            }
            EndDrag();
        }

        private void EndDrag() {
            if (Dragging) {
                GUIUtility.ResetCursor();
            }
            drag = null;
        }

        private void Drop() {
            if (dropPawn == null) return;

            var copy = Event.current.control;
            // moving a tag within a pawn's tags, to change its priority
            if (drag.fromPawn == dropPawn) {
                if (copy) return;

                var elements = dropPawn.GetComp<LoadoutComponent>().Loadout.elements;
                var from = elements.IndexOf(drag.element);
                var to = dropIndex > from ? dropIndex - 1 : dropIndex;
                if (from == to) return;

                elements.RemoveAt(from);
                elements.Insert(to, drag.element);
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
                return;
            }

            var added = false;
            foreach (var pawn in ActedOn(dropPawn).Where(p => p != drag.fromPawn)) {
                if (!CanDropOn(pawn)) continue;

                var element = new LoadoutElement(drag.element.Tag, drag.element.State, drag.element.ActiveCondition);
                pawn.GetComp<LoadoutComponent>().InsertElement(element, dropIndex);
                added = true;
            }

            if (!added) {
                Messages.Message(Strings.OverviewAlreadyHas(dropPawn, drag.element.Tag.name), MessageTypeDefOf.RejectInput, false);
                return;
            }

            // dragging moves a tag, unless it's copied with ctrl held
            if (!copy) {
                drag.fromPawn.GetComp<LoadoutComponent>().RemoveTag(drag.element);
            }
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        private bool CanDropOn(Pawn pawn) {
            return pawn == drag.fromPawn || !pawn.GetComp<LoadoutComponent>().Loadout.AllTags.Contains(drag.element.Tag);
        }

        // where the dragged tag would go in the row's tags, and the line showing it
        private void FindDropTarget(Rect rowRect, Rect chipsRect, Pawn pawn, List<Rect> chips) {
            dropPawn = pawn;
            var mouse = Event.current.mousePosition;
            var mouseLine = Mathf.FloorToInt((mouse.y - rowRect.y) / UIC.SPACED_HEIGHT);

            // the last rect is the button to add a tag, dropping by it puts the tag last
            dropIndex = chips.Count - 1;
            for (int i = 0; i < chips.Count - 1; i++) {
                var chipLine = Mathf.FloorToInt(chips[i].y / UIC.SPACED_HEIGHT);
                if (chipLine > mouseLine || (chipLine == mouseLine && mouse.x < chipsRect.x + chips[i].center.x)) {
                    dropIndex = i;
                    break;
                }
            }

            if (!CanDropOn(pawn)) {
                Widgets.DrawBoxSolid(rowRect, new Color(0.6f, 0.1f, 0.1f, 0.15f));
                return;
            }

            Widgets.DrawHighlight(rowRect);
            var at = chips[dropIndex];
            var x = chipsRect.x + at.x - CHIP_GAP / 2f;
            var y = chipsRect.y + at.y;
            Widgets.DrawLine(new Vector2(x, y), new Vector2(x, y + at.height), ReorderableWidget.LineColor, 2f);
        }

        // the dragged tag, following the mouse
        private void DrawDragged() {
            var rect = new Rect(mousePosition.x + 8f, mousePosition.y - CHIP_HEIGHT / 2f, ChipWidth(drag.element, 300f), CHIP_HEIGHT);

            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            DrawChipBody(rect, drag.element.Tag, ChipLabel(drag.element), 0.8f);
            if (Event.current.control) {
                Widgets.DrawTextureFitted(rect.RightPartPixels(REMOVE_WIDTH).ContractedBy(2f), TexButton.Plus, 1f);
            }
            GUI.color = Color.white;
        }

        private void DrawCollapseStrip(Rect rect) {
            if (Mouse.IsOver(rect)) {
                Widgets.DrawHighlight(rect);
            } else {
                Widgets.DrawLightHighlight(rect);
            }

            var iconRect = rect.MiddlePartPixels(COLLAPSE_WIDTH);
            Widgets.DrawTextureFitted(iconRect, showStats ? Textures.PreviousTex : Textures.NextTex, 1f);

            TooltipHandler.TipRegion(rect, showStats ? Strings.OverviewHideStats : Strings.OverviewShowStats);
            if (Widgets.ButtonInvisible(rect)) {
                showStats = !showStats;
            }
        }

        private void DrawToolbar(Rect rect) {
            var searchRect = rect.PopLeftPartPixels(200f).MiddlePartPixels(QuickSearchWidget.WidgetHeight);
            search.OnGUI(searchRect, FindPawns);
            search.noResultsMatched = search.filter.Active && pawns.Count == 0;
            TooltipHandler.TipRegion(searchRect, Strings.OverviewSearchDesc);
            rect.PopLeftPartPixels(GenUI.GapTiny);

            if (Widgets.ButtonText(rect.PopLeftPartPixels(140f), Strings.OverviewScope(scope))) {
                var options = new[] { Scope.CurrentMap, Scope.AllMaps, Scope.Everywhere }
                    .Select(s => new FloatMenuOption(Strings.OverviewScope(s), () => {
                        scope = s;
                        FindPawns();
                    }))
                    .ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }

            // View as if [ State ] were active
            var stateLabel = overrideState ? shownState?.name ?? Strings.DefaultStateName : Strings.OverviewOwnState;
            var after = rect.PopRightPartPixels(Strings.WereActive.GetWidthCached() + GenUI.GapTiny);
            var stateRect = rect.PopRightPartPixels(stateLabel.GetWidthCached() + UIC.SMALL_GAP * 2);
            var before = rect.PopRightPartPixels(Strings.ViewAsIf.GetWidthCached() + GenUI.GapTiny);

            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(before, Strings.ViewAsIf);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(after.RightPartPixels(after.width - GenUI.GapTiny), Strings.WereActive);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            if (Widgets.ButtonText(stateRect, stateLabel)) {
                var options = new List<FloatMenuOption> {
                    new FloatMenuOption(Strings.OverviewOwnState, () => overrideState = false),
                    new FloatMenuOption(Strings.DefaultStateName, () => (overrideState, shownState) = (true, null))
                };
                options.AddRange(LoadoutManager.States.Select(state =>
                    new FloatMenuOption(state.name, () => (overrideState, shownState) = (true, state))));
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void DrawTable(Rect rect) {
            var headerRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            DrawHeader(headerRect);

            // ends with the rows, rather than over their scrollbar
            GUI.color = Widgets.SeparatorLineColor;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH);
            GUI.color = Color.white;
            rect.AdjVertBy(2f);

            if (pawns.Count == 0) {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect.TopPartPixels(UIC.SPACED_HEIGHT), Strings.OverviewNoPawns);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;
                return;
            }

            // scroll while dragging a tag near the top or bottom
            if (Dragging && Event.current.type == EventType.Repaint && Mouse.IsOver(rect.ExpandedBy(0, SCROLL_MARGIN))) {
                var y = Event.current.mousePosition.y;
                var direction = y < rect.y + SCROLL_MARGIN ? -1 : y > rect.yMax - SCROLL_MARGIN ? 1 : 0;
                tableScroll.y = Mathf.Clamp(tableScroll.y + direction * SCROLL_SPEED * Time.deltaTime, 0, Mathf.Max(0, tableHeight - rect.height));
            }

            if (selected.Count > 1) {
                DrawSelectionBar(rect.PopBottomPartPixels(UIC.SPACED_HEIGHT));
                rect.PopBottomPartPixels(GenUI.GapTiny);
            }

            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, tableHeight);
            Widgets.BeginScrollView(rect, ref tableScroll, viewRect);

            var visible = rect;
            visible.y += tableScroll.y;

            var height = 0f;
            var rowsRect = viewRect;
            for (int i = 0; i < pawns.Count; i++) {
                var pawn = pawns[i];
                var chipsWidth = ChipsWidth(viewRect.width);
                LayoutChips(pawn.GetComp<LoadoutComponent>().Loadout.elements, chipsWidth, out var rowHeight);

                var rowRect = rowsRect.PopTopPartPixels(rowHeight);
                height += rowHeight;
                if (!rowRect.Overlaps(visible)) continue;

                DrawRow(rowRect, pawn, i);
            }

            Widgets.EndScrollView();
            tableHeight = height;
        }

        // [ 3 selected ] [ Add tag ] [ Remove tag ] [ Paste ]
        private void DrawSelectionBar(Rect rect) {
            GUI.color = Widgets.SeparatorLineColor;
            Widgets.DrawLineHorizontal(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH);
            GUI.color = Color.white;
            rect = rect.ContractedBy(0, 2f);

            var chosen = Selected.ToList();
            var components = chosen.Select(p => p.GetComp<LoadoutComponent>()).ToList();

            var label = Strings.OverviewSelectedCount(chosen.Count);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect.PopLeftPartPixels(label.GetWidthCached() + UIC.SMALL_GAP), label);
            Text.Anchor = TextAnchor.UpperLeft;

            bool Button(string text) {
                var buttonRect = rect.PopLeftPartPixels(Mathf.Max(100f, text.GetWidthCached() + UIC.SMALL_GAP * 2));
                rect.PopLeftPartPixels(GenUI.GapTiny);
                return Widgets.ButtonText(buttonRect, text);
            }

            if (Button(Strings.AddTag)) {
                Find.WindowStack.Add(new Dialog_TagSelector(LoadoutManager.Tags, tag => {
                    foreach (var component in components.Where(c => !c.Loadout.AllTags.Contains(tag))) {
                        component.AddTag(tag, insertAtStart: false);
                    }
                }));
            }

            if (Button(Strings.OverviewRemoveTags)) {
                var tags = components.SelectMany(c => c.Loadout.AllTags).Distinct().ToList();
                Find.WindowStack.Add(new Dialog_TagSelector(tags, tag => {
                    foreach (var component in components) {
                        var element = component.Loadout.elements.FirstOrDefault(e => e.Tag == tag);
                        if (element != null) {
                            component.RemoveTag(element);
                        }
                    }
                }));
            }

            var pasteTo = chosen.Where(LoadoutCopyPaste.CanPasteTo).ToList();
            if (pasteTo.Any() && Button(Strings.OverviewPasteToSelected(pasteTo.Count))) {
                pasteTo.ForEach(LoadoutCopyPaste.PasteTo);
            }
        }

        private void DrawHeader(Rect rect) {
            rect.width -= UIC.SCROLL_WIDTH;

            void Column(Rect columnRect, string label, SortBy by) {
                Widgets.DrawHighlightIfMouseover(columnRect);
                GUI.color = sortBy == by ? Color.white : Color.gray;
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(columnRect, label);
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = Color.white;

                if (sortBy == by) {
                    var iconRect = new Rect(columnRect.x + label.GetWidthCached() + GenUI.GapTiny, columnRect.y, columnRect.height, columnRect.height);
                    Widgets.DrawTextureFitted(iconRect, descending ? Textures.SortByStatDscTex : Textures.SortByStatAscTex, 0.8f);
                }

                TooltipHandler.TipRegion(columnRect, Strings.OverviewSortDesc);
                if (!Widgets.ButtonInvisible(columnRect)) return;

                descending = sortBy == by && !descending;
                sortBy = by;
                FindPawns();
            }

            Column(rect.PopLeftPartPixels(NAME_WIDTH).RightPartPixels(NAME_WIDTH - UIC.SMALL_ICON - GenUI.GapTiny), Strings.OverviewName, SortBy.Name);
            Column(rect.PopLeftPartPixels(STATE_WIDTH), Strings.OverviewState, SortBy.State);
            rect.PopRightPartPixels(ButtonsWidth);
            Column(rect, Strings.OverviewTags, SortBy.Tags);
        }

        // the width left over for a row's tags, after its other columns
        private static float ChipsWidth(float rowWidth) => rowWidth - NAME_WIDTH - STATE_WIDTH - ButtonsWidth;

        // copy (and paste, once something is copied) & edit
        private static float ButtonsWidth => LoadoutCopyPaste.Width(UIC.SMALL_ICON) + GenUI.GapTiny + UIC.SPACED_HEIGHT;

        private void DrawRow(Rect rect, Pawn pawn, int idx) {
            if (selected.Contains(pawn)) {
                Widgets.DrawHighlightSelected(rect);
            } else if (idx % 2 == 1) {
                Widgets.DrawLightHighlight(rect);
            }
            Widgets.DrawHighlightIfMouseover(rect);

            var line = rect.TopPartPixels(UIC.SPACED_HEIGHT);
            DrawName(line.PopLeftPartPixels(NAME_WIDTH), pawn);

            var component = pawn.GetComp<LoadoutComponent>();
            var stateRect = line.PopLeftPartPixels(STATE_WIDTH).ContractedBy(GenUI.GapTiny, 2f);
            Widgets.Dropdown(stateRect, pawn, p => p.GetActiveState(), PawnColumnWorker_LoadoutState.GenerateMenuFor,
                pawn.GetActiveState()?.name ?? Strings.SelectState, paintable: true);
            line.PopLeftPartPixels(GenUI.GapTiny);

            var buttons = line.PopRightPartPixels(ButtonsWidth);
            var editRect = buttons.PopRightPartPixels(UIC.SPACED_HEIGHT).ContractedBy(2f);
            if (Widgets.ButtonImageFitted(editRect, Textures.EditTex)) {
                Find.WindowStack.Add(new Dialog_LoadoutEditor(pawn));
            }
            TooltipHandler.TipRegion(editRect, () => Strings.EditLoadoutDesc(pawn), 388492);
            var info = InfoFor(pawn);

            buttons.PopRightPartPixels(GenUI.GapTiny);
            var copyPasteRect = buttons.RightPartPixels(LoadoutCopyPaste.Width(UIC.SMALL_ICON)).MiddlePartPixels(UIC.SMALL_ICON);
            LoadoutCopyPaste.Draw(copyPasteRect, pawn);

            var state = ShownStateFor(pawn);
            var chipsRect = new Rect(line.x, rect.y, line.width, rect.height);
            var elements = component.Loadout.elements;
            var chips = LayoutChips(elements, chipsRect.width, out _);
            foreach (var (element, i) in elements.ToList().WithIndex()) {
                var chipRect = chips[i];
                chipRect.position += chipsRect.position;
                DrawChip(chipRect, pawn, element, element.Active(state), info.warnings.TryGetValue(element));
            }

            if (Dragging && Mouse.IsOver(rect)) {
                FindDropTarget(rect, chipsRect, pawn, chips);
            }

            var addRect = chips[chips.Count - 1];
            addRect.position += chipsRect.position;
            if (Widgets.ButtonImageFitted(addRect.ContractedBy(4f), TexButton.Plus)) {
                Find.WindowStack.Add(new Dialog_TagSelector(LoadoutManager.Tags.Except(component.Loadout.AllTags).ToList(),
                    tag => component.AddTag(tag, insertAtStart: false)));
            }
            TooltipHandler.TipRegion(addRect, Strings.AddTag);
        }

        private void DrawName(Rect rect, Pawn pawn) {
            var iconRect = rect.PopLeftPartPixels(UIC.SMALL_ICON).MiddlePartPixels(UIC.SMALL_ICON);
            Widgets.ThingIcon(iconRect, pawn);
            rect.PopLeftPartPixels(GenUI.GapTiny);

            // pawns away from a map, e.g. in a caravan
            if (!pawn.Spawned) {
                GUI.color = Color.gray;
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, pawn.LabelShortCap.Truncate(rect.width));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;

            var clickRect = rect;
            clickRect.xMin -= UIC.SMALL_ICON + GenUI.GapTiny;
            if (Widgets.ButtonInvisible(clickRect)) {
                Select(pawn);
            }
            TooltipHandler.TipRegion(clickRect, Strings.OverviewSelectDesc);
        }

        private static string ChipLabel(LoadoutElement element) {
            if (element.State == null) return element.Tag.name;
            return $"{element.Tag.name} {$"({element.StateName})".Colorize(Color.gray)}";
        }

        private static float ChipWidth(LoadoutElement element, float maxWidth) {
            var labelWidth = element.Tag.name.GetWidthCached();
            if (element.State != null) {
                labelWidth += $" ({element.StateName})".GetWidthCached();
            }

            return Mathf.Min(maxWidth, STRIP_WIDTH + CHIP_PADDING * 2 + labelWidth + REMOVE_WIDTH);
        }

        // positions of a row's tags, wrapping onto more lines once they run out of room, and lastly of the button to
        // add another tag, relative to the top left of the row's tags
        private static List<Rect> LayoutChips(List<LoadoutElement> elements, float width, out float height) {
            var rects = new List<Rect>(elements.Count + 1);
            var x = 0f;
            var line = 0;

            void Place(float chipWidth) {
                if (x > 0 && x + chipWidth > width) {
                    x = 0;
                    line++;
                }

                var y = line * UIC.SPACED_HEIGHT + (UIC.SPACED_HEIGHT - CHIP_HEIGHT) / 2f;
                rects.Add(new Rect(x, y, chipWidth, CHIP_HEIGHT));
                x += chipWidth + CHIP_GAP;
            }

            foreach (var element in elements) {
                Place(ChipWidth(element, width));
            }
            Place(CHIP_HEIGHT);

            height = (line + 1) * UIC.SPACED_HEIGHT;
            return rects;
        }

        private static void DrawChipBody(Rect rect, Tag tag, string label, float alpha) {
            var background = CharacterCardUtility.StackElementBackground;
            background.a *= alpha;
            Widgets.DrawBoxSolid(rect, background);
            var stripColour = Panel_ShowCoverage.GetColorForTag(tag);
            stripColour.a *= alpha;
            Widgets.DrawBoxSolid(rect.LeftPartPixels(STRIP_WIDTH), stripColour);

            var labelRect = rect;
            labelRect.xMin += STRIP_WIDTH + CHIP_PADDING;
            labelRect.xMax -= REMOVE_WIDTH;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(labelRect, label.Truncate(labelRect.width));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }

        private void DrawChip(Rect rect, Pawn pawn, LoadoutElement element, bool active, string warning) {
            var tag = element.Tag;
            var hovered = Mouse.IsOver(rect);

            // inactive tags, given the state shown, are faded, as is a tag being moved
            var beingMoved = Dragging && drag.element == element && !Event.current.control;
            DrawChipBody(rect, tag, ChipLabel(element), beingMoved ? 0.25f : active ? 1f : 0.5f);

            if (warning != null) {
                Widgets.DrawBoxSolid(rect, WarningColour);
            }

            if (Dragging) return;

            if (hovered) {
                Widgets.DrawHighlight(rect);
                DrawGrabHandle(rect.LeftPartPixels(STRIP_WIDTH));

                var removeRect = rect.RightPartPixels(REMOVE_WIDTH).MiddlePartPixels(REMOVE_WIDTH);
                if (Widgets.ButtonImage(removeRect.ContractedBy(2f), TexButton.CloseXSmall)) {
                    RemoveTag(pawn, element);
                    return;
                }
                TooltipHandler.TipRegion(removeRect, Strings.OverviewRemoveTag(pawn));
                if (Mouse.IsOver(removeRect)) return;

                if (Event.current.type == EventType.MouseDown && Event.current.button == 1) {
                    Event.current.Use();
                    Find.WindowStack.Add(new FloatMenu(ChipOptions(pawn, element).ToList()));
                } else if (Event.current.type == EventType.MouseDown && Event.current.button == 0) {
                    StartDrag(pawn, element);
                }
            }

            TooltipHandler.TipRegion(rect, () => ChipTooltip(element, active, warning), tag.GetHashCode());
        }

        // the pulsing handle the loadout editor shows on its tags, hinting they can be dragged
        private static void DrawGrabHandle(Rect rect) {
            var center = rect.center;
            var handleRect = new Rect(center.x - UIC.SMALL_GAP / 2f, center.y - CHIP_HEIGHT / 2f, UIC.SMALL_GAP, CHIP_HEIGHT);
            var colour = Color.black;
            colour.a = 0.7f + Mathf.Sin((Time.time % 2) / 2.0f * Mathf.PI) * 0.3f;
            GUI.color = colour;
            Widgets.DrawTexturePart(handleRect, new Rect(1 / 4f, 0, 1 / 2f, 1), Textures.DraggableTex);
            GUI.color = Color.white;
        }

        private IEnumerable<FloatMenuOption> ChipOptions(Pawn pawn, LoadoutElement element) {
            yield return new FloatMenuOption(Strings.OverviewChangeState, () => Find.WindowStack.Add(new Dialog_SetTagLoadoutState(element)));
            yield return new FloatMenuOption(Strings.OverviewEditTag(element.Tag.name), () => Find.WindowStack.Add(new Dialog_TagEditor(element.Tag)));
            yield return new FloatMenuOption(Strings.OverviewRemoveTag(pawn), () => RemoveTag(pawn, element));

            var others = ActedOn(pawn).Count(p => p.GetComp<LoadoutComponent>().Loadout.AllTags.Contains(element.Tag));
            if (others > 1) {
                yield return new FloatMenuOption(Strings.OverviewRemoveFromSelected(others), () => RemoveTag(pawn, element, true));
            }
        }

        // removes the tag from `pawn`, and also from the other selected pawns if `fromSelected`
        private void RemoveTag(Pawn pawn, LoadoutElement element, bool fromSelected = false) {
            foreach (var other in fromSelected ? ActedOn(pawn) : new[] { pawn }) {
                var component = other.GetComp<LoadoutComponent>();
                var elem = other == pawn ? element : component.Loadout.elements.FirstOrDefault(e => e.Tag == element.Tag);
                if (elem != null) {
                    component.RemoveTag(elem);
                }
            }
        }

        private static string ChipTooltip(LoadoutElement element, bool active, string warning) {
            var tip = element.Tag.name.Colorize(ColoredText.TipSectionTitleColor);
            if (element.State != null) {
                tip += $"\n{Strings.OverviewActiveWhen(element.StateName)}";
            }
            if (!active) {
                tip += $"\n{Strings.OverviewInactive.Colorize(ColorLibrary.RedReadable)}";
            }
            if (warning != null) {
                tip += $"\n\n{warning.Colorize(ColorLibrary.RedReadable)}";
            }

            var items = element.Tag.requiredItems.Select(item => $"  - {item.Label} x{item.Quantity}");
            return tip + "\n\n" + string.Join("\n", items) + "\n\n" + Strings.OverviewChipHint.Colorize(Color.gray);
        }

    }

}
