using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.EventSystems;
using Verse;

namespace Inventory {

    public class PawnColumnWorker_LoadoutState : PawnColumnWorker {

        private const float HEADER_BUTTON_HEIGHT = 32f;

        public override void DoHeader(Rect rect, PawnTable table) {
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(rect, () => Strings.StatesExplanation, 39482492);
            base.DoHeader(rect, table);

            if (Widgets.ButtonText(rect.PopTopPartPixels(HEADER_BUTTON_HEIGHT), Strings.ModifyStates)) {
                Find.WindowStack.Add(new Dialog_LoadoutStateEditor());
            }
        }

        // [ Select State ] [ (EditIcon) ]
        // ^^ set loadout state   ^^ edit the pawns loadout
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table) {
            var buttonRect = new Rect(rect.x, rect.y + 2f, rect.width * 0.7f, rect.height - 4f);
            var editIconRect = new Rect(rect.x + buttonRect.width, rect.y + 2f, rect.width * 0.3f, rect.height - 4);
            editIconRect.AdjHorzBy(4);
            
            Widgets.Dropdown(buttonRect, pawn,
                (p) => p.TryGetComp<LoadoutComponent>().Loadout.CurrentState,
                GenerateMenuFor, pawn.GetActiveState()?.name ?? Strings.SelectState,
                paintable: true);

            if (Mouse.IsOver(buttonRect) && Input.GetMouseButtonDown(1)) {
                var pawns = (pawn.IsCaravanMember()
                        ? pawn.GetCaravan().PawnsListForReading
                        : pawn.Map.mapPawns.AllPawns)
                    ?.Where(p => p.IsValidLoadoutHolder());

                if (pawns.EnumerableNullOrEmpty()) {
                    return;
                }

                Find.WindowStack.Add(new FloatMenu(new List<FloatMenuOption>() {
                    new FloatMenuOption(Strings.MassChangeStates(pawn, pawn.IsCaravanMember() ? Strings.Caravan : Strings.Map, pawn.GetActiveState()), () => {
                        pawns.Do(p => p.SetActiveState(pawn.GetActiveState()));
                    })
                }));
            }

            TooltipHandler.TipRegion(editIconRect, () => Strings.EditLoadoutDesc(pawn), 388492);

            if (Widgets.ButtonText(editIconRect, "AssignTabEdit".TranslateSimple())) {
                Find.WindowStack.Add(new Dialog_LoadoutEditor(pawn));
            }
        }

        public IEnumerable<Widgets.DropdownMenuElement<LoadoutState>> GenerateMenuFor(Pawn p) {
            var pState = p.TryGetComp<LoadoutComponent>().Loadout.CurrentState;

            yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                option = new FloatMenuOption(Strings.DefaultStateName, () => p.SetActiveState(null)),
                payload = null
            };

            foreach (var state in LoadoutManager.States.Except(pState))
                yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                    option = new FloatMenuOption(state.name, () => p.SetActiveState(state)),
                    payload = state
                };
        }

        public override int Compare(Pawn a, Pawn b) {
            var aState = a.GetActiveState();
            var bState = b.GetActiveState();
            if (aState == null && bState == null) return 0;
            if (aState == null) return 1;
            if (bState == null) return -1;

            return aState.name.CompareTo(bState.name);
        }

        public override int GetMinWidth(PawnTable table) {
            return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(131f));
        }

        public override int GetOptimalWidth(PawnTable table) {
            return Mathf.Clamp(Mathf.CeilToInt(201f), GetMinWidth(table), GetMaxWidth(table));
        }

        public override int GetMinHeaderHeight(PawnTable table) {
            return Mathf.Max(base.GetMinHeaderHeight(table), 65);
        }

    }

}