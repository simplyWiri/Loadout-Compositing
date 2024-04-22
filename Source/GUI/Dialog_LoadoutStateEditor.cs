using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Dialog_LoadoutStateEditor : Window {

        private string loadoutStateString = string.Empty;
        private Vector2 scrollPos = Vector2.zero;
        private List<Pair<LoadoutState, bool>> states;
        
        public override Vector2 InitialSize => new Vector2(320, 320);
        
        public Dialog_LoadoutStateEditor(List<LoadoutState> states) {
            this.states = states.Select(s => new Pair<LoadoutState, bool>(s, false))
                .OrderByDescending(s => s.First.name)
                .ToList();
        }
        
        
        public override void DoWindowContents(Rect inRect) {
            DrawSetPanicState(ref inRect);

            Text.Font = GameFont.Small;
            var topPrt = inRect.PopTopPartPixels(UIC.SPACED_HEIGHT);

            if (Widgets.ButtonText(topPrt.PopRightPartPixels(100), Strings.Create)) {
                if (loadoutStateString != string.Empty) {
                    var state = new LoadoutState(loadoutStateString);
                    LoadoutManager.States.Add(state);
                    this.states.Add(new Pair<LoadoutState, bool>(state, false));
                    this.states = this.states.OrderByDescending(st => st.First.name).ToList();
                } else {
                    Messages.Message(Strings.InvalidStateName, MessageTypeDefOf.RejectInput);
                }
                loadoutStateString = string.Empty;
            }
            
            GUIUtility.InputField(topPrt.LeftPart(0.85f), "NameLoadoutState", ref loadoutStateString);

            var viewRect = inRect;
            viewRect.width -= 16f;
            viewRect.height = (UIC.SPACED_HEIGHT + UIC.SMALL_GAP) * LoadoutManager.States.Count;
            Widgets.BeginScrollView(inRect, ref scrollPos, viewRect);

            viewRect.AdjVertBy(UIC.SMALL_GAP);

            foreach (var ((state, active), index) in states.WithIndex()) {
                var rowRect = viewRect.PopTopPartPixels(UIC.SPACED_HEIGHT);
                
                if (index % 2 == 1) {
                    Widgets.DrawLightHighlight(rowRect);
                }

#if VERSION_1_4
                if(Widgets.ButtonImage(rowRect.PopRightPartPixels(UIC.SMALL_ICON), TexButton.DeleteX)) {
#elif VERSION_1_5
                if(Widgets.ButtonImage(rowRect.PopRightPartPixels(UIC.SMALL_ICON), TexButton.Delete)) {
#endif
                    LoadoutManager.RemoveState(state);
                }

                rowRect.width -= UIC.SMALL_GAP;

                if (Widgets.ButtonImage(rowRect.PopRightPartPixels(UIC.SMALL_ICON), TexButton.Rename)) {
                    states[index].SetSecond(true);
                }

                if (active) {
                    var width = Mathf.Min(rowRect.width - 15f, state.name.GetWidthCached() + 15f);
                    var entryRect = rowRect.PopLeftPartPixels(width);
                    GUIUtility.InputField(entryRect, "RenameField-" + state.id, ref state.name);

                    if (Input.GetMouseButton(0) && !Mouse.IsOver(entryRect)) {
                        states[index].SetSecond(false);
                    }
                }
                else {
                    Widgets.Label(rowRect, state.name);
                }
                
                viewRect.AdjVertBy(UIC.SMALL_GAP);
            }
            
            Widgets.EndScrollView();
        }

        public void DrawSetPanicState(ref Rect rect) {
            var topRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT).TopPartPixels(Text.LineHeight);
            TooltipHandler.TipRegion(topRect, Strings.PanicStateDesc);
            Widgets.Label(topRect.PopLeftPartPixels(Strings.PanicState.GetWidthCached() + UIC.SMALL_GAP), Strings.PanicState);

            if ( LoadoutManager.States.Except(LoadoutManager.PanicState).Count() >= 1 ) {
               Widgets.Dropdown(topRect, LoadoutManager.PanicState, (p) => p, GenerateMenuFor, LoadoutManager.PanicState?.name ?? Strings.DefaultStateName, paintable: true);
            } else {
                GUI.color = Color.gray;
                if (Widgets.ButtonText(topRect, LoadoutManager.PanicState?.name ?? Strings.DefaultStateName)) {
                    Messages.Message(Strings.NoValidPanicStates, MessageTypeDefOf.CautionInput, false);
                }
                GUI.color = Color.white;
            }

            GUI.color = Widgets.SeparatorLineColor;
            Widgets.DrawLineHorizontal(rect.x - 2f, rect.y - 4f, rect.width + 2f);

            rect.AdjVertBy(4f);
            GUI.color = Color.white;
        }

        public IEnumerable<Widgets.DropdownMenuElement<LoadoutState>> GenerateMenuFor(LoadoutState currentPanicMode)
        {
            foreach (var state in LoadoutManager.States.Except(currentPanicMode)) {
                yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                    option = new FloatMenuOption(state.name, () => LoadoutManager.SetPanicState(state)),
                    payload = state
                };
            }

        }
    }

}