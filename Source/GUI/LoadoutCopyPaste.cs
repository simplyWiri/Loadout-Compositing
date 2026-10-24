using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
    
    // The copy & paste buttons for a pawn's tags, shared by the assign tab and the loadout overview, so tags copied in
    // one can be pasted in the other
    public static class LoadoutCopyPaste {

        private static CopiedTags copiedTags = null;

        // once something has been copied, the paste button is shown alongside the copy button
        public static bool Primed { get; private set; } = false;

        // the width the buttons need, given the width of one of them
        public static float Width(float buttonWidth) => buttonWidth * (Primed ? 2 : 1);

        // whether the copied tags would change `pawn`'s tags if pasted
        public static bool CanPasteTo(Pawn pawn) => copiedTags != null && copiedTags.ApplicableTo(pawn);

        public static void PasteTo(Pawn pawn) => copiedTags?.CopyTo(pawn);

        public static void Draw(Rect rect, Pawn pawn) {
            var copyRect = rect;
            if (Primed) {
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
                    Primed = true;
                } else if (Input.GetMouseButtonDown(1)) {
                    copiedTags = null;
                    var tags = loadout.AllTags.ToList();
                    var opts = new List<FloatMenuOption>() {
                        new FloatMenuOption(Strings.CopyAllTagsFrom + pawn.LabelShort + Strings.ReplaceOnPaste, () => {
                            copiedTags = new CopiedTags(pawn, elems, true);
                            Primed = true;
                        }),
                        new FloatMenuOption(Strings.CopyAllTagsFrom + pawn.LabelShort + Strings.AddOnPaste, () => {
                            copiedTags = new CopiedTags(pawn, elems, false);
                            Primed = true;
                        }),
                        new FloatMenuOption(Strings.SelectTagsFrom + pawn.LabelShort + Strings.ReplaceOnPaste, () => {
                            Find.WindowStack.Add(new Dialog_TagSelector(tags, tag => {
                                var elem = elems.FirstOrDefault(e => e.Tag == tag);
                                
                                copiedTags ??= new CopiedTags(pawn, new List<LoadoutElement>(), true);
                                copiedTags.elemsToCopy.Add(elem);
                                Primed = true;
                            }, customTitleSuffix: Strings.ToCopy));
                        }),     
                        new FloatMenuOption(Strings.SelectTagsFrom + pawn.LabelShort + Strings.AddOnPaste, () => {
                            Find.WindowStack.Add(new Dialog_TagSelector(tags, tag => {
                                var elem = elems.FirstOrDefault(e => e.Tag == tag);

                                copiedTags ??= new CopiedTags(pawn, new List<LoadoutElement>(), false);
                                copiedTags.elemsToCopy.Add(elem);
                                Primed = true;
                            }, customTitleSuffix: Strings.ToCopy));
                        })
                    };
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }
            TooltipHandler.TipRegion(copyRect, () => Strings.HasRightClick, 986789547);

            if (!Primed) return;
            
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

    }

}
