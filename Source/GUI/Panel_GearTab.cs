using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public static class Panel_GearTab {

        public static void DrawTags(Pawn p, ref float curY, float width) {
            if (!p.IsValidLoadoutHolder()) return;
            
            Widgets.ListSeparator(ref curY, width, Strings.TagLoadoutComposition);
            var rect = new Rect(0, curY, width, UIC.DEFAULT_HEIGHT);
            curY += UIC.DEFAULT_HEIGHT;

            DrawButtons(rect, p);
            DrawProsthetics(p, ref curY, width);
        }

        // [ Icon ] [ Field hand 1/2: Surgery awaiting approval ], one row per prosthetic of the active tags
        private static void DrawProsthetics(Pawn p, ref float curY, float width) {
            var loadout = p.TryGetComp<LoadoutComponent>().Loadout;
            var items = loadout.Items.Where(item => item.IsProsthetic).Distinct().ToList();
            if (items.Count == 0) return;

            var slots = loadout.HypotheticalProsthetics(loadout.CurrentState, p.RaceProps.body);
            foreach (var item in items) {
                var rect = new Rect(0, curY, width, UIC.DEFAULT_HEIGHT);
                curY += UIC.DEFAULT_HEIGHT;

                GUIUtility.DrawItemIcon(rect.PopLeftPartPixels(UIC.DEFAULT_HEIGHT), item, false);
                rect.PopLeftPartPixels(GenUI.GapTiny);

                var def = item.RepresentativeDef;
                var label = $"{def.LabelCap} {Mathf.Min(ProstheticUtility.SatisfiedCount(p, def), item.Quantity)}/{item.Quantity}: {ProstheticStatus(p, loadout, slots, item)}";
                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Widgets.Label(rect, label.Truncate(rect.width));
                Text.WordWrap = true;
                Text.Anchor = TextAnchor.UpperLeft;
                TooltipHandler.TipRegion(rect, label);
            }
        }

        // where the prosthetic is at, from installed, through queued, to what's stopping it being queued
        private static string ProstheticStatus(Pawn p, Loadout loadout, ProstheticSlots slots, Item item) {
            var def = item.RepresentativeDef;
            if (ProstheticUtility.SatisfiedCount(p, def) >= item.Quantity) {
                return ProstheticUtility.InstalledCount(p, def) >= item.Quantity ? Strings.StatusInstalled : Strings.StatusHasBetter;
            }

            var recipes = ProstheticUtility.InstallRecipesFor(def);
            var bills = p.BillStack.Bills.Where(bill => recipes.Contains(bill.recipe)).ToList();
            if (bills.Any(bill => !bill.suspended)) return Strings.StatusSurgeryQueued;
            if (bills.Any()) return Strings.SurgeryAwaitingApproval;

            if (slots.Conflicting(item)) {
                var blocker = slots.Conflicts[item];
                return blocker == null ? Strings.StatusNoRoom : Strings.StatusConflicts(blocker.name);
            }

            if (!ProstheticUtility.CanEverInstall(p, def)) return Strings.StatusCantEverInstall;
            if (loadout.Declined(def)) return Strings.StatusDeclined;
            if (ModBase.settings.autoSurgery == AutoSurgery.Off) return Strings.StatusAutoSurgeryOff;

            return Strings.StatusWaitingForPart;
        }

        public static void DrawButtons(Rect rect, Pawn p) {
            if (Widgets.ButtonText(rect.LeftHalf(), Strings.EditXLoadout(p.LabelShort))) {
                Find.WindowStack.Add(new Dialog_LoadoutEditor(p));
            }

            if (Widgets.ButtonText(rect.RightHalf(), Strings.EditOrCreateTags)) {
                Find.WindowStack.Add(new Dialog_TagEditor());
            }
        }

    }

}