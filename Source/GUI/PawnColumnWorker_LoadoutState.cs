using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.EventSystems;
using Verse;

namespace Inventory {

    class CopiedTags {

        public Pawn fromPawn = null;
        public List<LoadoutElement> elemsToCopy;
        public bool replace = true;

        public CopiedTags(Pawn pawn, List<LoadoutElement> elems, bool replace = true) {
            this.fromPawn = pawn;
            this.elemsToCopy = elems;
            this.replace = replace;
        }
        
        public void CopyTo(Pawn pawn) {
            var pComp = pawn.GetComp<LoadoutComponent>();
            if (replace) {
                var elems = pComp.Loadout.AllElements.ToList();
                foreach (var elem in elems) {
                    pComp.RemoveTag(elem);
                }

                foreach (var elem in elemsToCopy) {
                    pComp.AddTag(elem.Tag, elem.State, elem.ActiveCondition, false); 
                }
            } else {
                var elems = pComp.Loadout.AllElements.ToList();
                foreach (var elem in elemsToCopy) {
                    var existing = elems.FirstOrFallback(existing => existing.Tag == elem.Tag);
                    if (existing != null) { existing.SetLoadoutState(elem.State, elem.ActiveCondition); }
                    else { pComp.AddTag(elem.Tag, elem.State, elem.ActiveCondition, false); }
                }
            }
        }

        public bool ApplicableTo(Pawn pawn) {
            if (pawn == fromPawn) return false;

            var elements = pawn.GetComp<LoadoutComponent>().Loadout.AllElements.ToList();
            
            if (replace) {
                return !(elemsToCopy.All(t => elements.Any(e => e.Equivalent(t))) && elemsToCopy.Count == elements.Count);
            } 
            
            return !elemsToCopy.All(t => elements.Any(e => e.Equivalent(t)));
        }
    }
    
    public class PawnColumnWorker_LoadoutState : PawnColumnWorker {

        private const float HEADER_BUTTON_HEIGHT = 32f;
        private CopiedTags copiedTags = null;
        private bool hasCopyPrimed = false;

        public override void DoHeader(Rect rect, PawnTable table) {
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(rect, () => Strings.StatesExplanation, 39482492);
            base.DoHeader(rect, table);

            if (Widgets.ButtonText(rect.PopTopPartPixels(HEADER_BUTTON_HEIGHT), Strings.ModifyStates)) {
                Find.WindowStack.Add(new Dialog_LoadoutStateEditor(LoadoutManager.States));
            }
        }

        // [ cp ] [ pst ] [ Select State ] [ (EditIcon) ]
        //                  ^^ set loadout state  ^^ edit the pawns loadout
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table) {
            if ( !pawn.IsValidLoadoutHolder() ) {
                return;
            }

            DoCopyPaste(rect.PopLeftPartPixels(rect.height * 0.8f * (hasCopyPrimed ? 2 : 1)), pawn);
        
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
                        pawns.Do(p => p.SetActiveState(pawn.GetActiveState(), true));
                    })
                }));
            }

            TooltipHandler.TipRegion(editIconRect, () => Strings.EditLoadoutDesc(pawn), 388492);

            if (Widgets.ButtonText(editIconRect, "AssignTabEdit".TranslateSimple())) {
                Find.WindowStack.Add(new Dialog_LoadoutEditor(pawn));
            }
        }
        
        private void DoCopyPaste(Rect rect, Pawn pawn) {
            var copyRect = rect;
            if (hasCopyPrimed) {
                copyRect = rect.LeftHalf();
            }
            var pasteRect = rect.RightHalf();

            Widgets.DrawTextureFitted(copyRect, TexButton.Copy, 1f);
            if (Mouse.IsOver(copyRect))
            {
                var loadout = pawn.GetComp<LoadoutComponent>().Loadout;
                var elems = loadout.AllElements.ToList();
                
                if (Input.GetMouseButtonDown(0)) {
                    copiedTags = new CopiedTags(pawn, elems);
                    hasCopyPrimed = true;
                } else if (Input.GetMouseButtonDown(1)) {
                    copiedTags = null;
                    var tags = loadout.AllTags.ToList();
                    var opts = new List<FloatMenuOption>() {
                        new FloatMenuOption(Strings.CopyAllTagsFrom + pawn.LabelShort + Strings.ReplaceOnPaste, () => {
                            copiedTags = new CopiedTags(pawn, elems, true);
                            hasCopyPrimed = true;
                        }),
                        new FloatMenuOption(Strings.CopyAllTagsFrom + pawn.LabelShort + Strings.AddOnPaste, () => {
                            copiedTags = new CopiedTags(pawn, elems, false);
                            hasCopyPrimed = true;
                        }),
                        new FloatMenuOption(Strings.SelectTagsFrom + pawn.LabelShort + Strings.ReplaceOnPaste, () => {
                            Find.WindowStack.Add(new Dialog_TagSelector(tags, tag => {
                                var elem = elems.FirstOrDefault(e => e.Tag == tag);
                                
                                copiedTags ??= new CopiedTags(pawn, new List<LoadoutElement>(), true);
                                copiedTags.elemsToCopy.Add(elem);
                                hasCopyPrimed = true;
                            }, customTitleSuffix: Strings.ToCopy));
                        }),     
                        new FloatMenuOption(Strings.SelectTagsFrom + pawn.LabelShort + Strings.AddOnPaste, () => {
                            Find.WindowStack.Add(new Dialog_TagSelector(tags, tag => {
                                var elem = elems.FirstOrDefault(e => e.Tag == tag);

                                copiedTags ??= new CopiedTags(pawn, new List<LoadoutElement>(), false);
                                copiedTags.elemsToCopy.Add(elem);
                                hasCopyPrimed = true;
                            }, customTitleSuffix: Strings.ToCopy));
                        })
                    };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }
            TooltipHandler.TipRegion(copyRect, () => Strings.HasRightClick, 986789547);

            if (!hasCopyPrimed) return;
            
            var color = Color.white;
            if (copiedTags == null || !copiedTags.ApplicableTo(pawn)) {
                color = Color.gray;
            }
            else if (Mouse.IsOver(pasteRect)) {
                color = GenUI.MouseoverColor;
            }

            GUI.color = color;
            Widgets.DrawTextureFitted(pasteRect, TexButton.Paste, 1f);
            GUI.color = Color.white;

            if (copiedTags != null && Mouse.IsOver(pasteRect) && Input.GetMouseButton(0)) {
                copiedTags.CopyTo(pawn);
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

            return String.Compare(aState.name, bState.name, StringComparison.Ordinal);
        }

        public override int GetMinWidth(PawnTable table) {
            return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(131f + Text.LineHeight * (hasCopyPrimed ? 1.6f : 0)));
        }

        public override int GetOptimalWidth(PawnTable table) {
            return Mathf.Clamp(239, GetMinWidth(table), GetMaxWidth(table));
        }

        public override int GetMinHeaderHeight(PawnTable table) {
            return Mathf.Max(base.GetMinHeaderHeight(table), 65);
        }

    }

}