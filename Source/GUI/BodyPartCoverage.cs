using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    // The body part half of the coverage panel: each part of a pawn which the loadout's prosthetics would take (or are
    // blocked from), showing which take it, and what they block. Drawn in the same style as the apparel grid
    public class BodyPartCoverage {

        private static readonly Color FadedIconColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        private Dialog_LoadoutEditor parent;
        private Vector2 scroll = Vector2.zero;
        private float height = 9999f;
        private LoadoutComponent component;

        public BodyPartCoverage(Dialog_LoadoutEditor parent) {
            this.parent = parent;
            this.component = parent.component;
        }

        public void Draw(Rect rect, BodyDef body) {
            var slots = component.Loadout.HypotheticalProsthetics(parent.shownState, body);
            // parts which are only replaced along with an added part above them (e.g. the toes under a bionic leg) are
            // folded into that part's tooltip, unless something is blocked on them
            var parts = body.AllParts.Where(part => slots.PlacementsOn(part).Any(p => p.use != ProstheticSlots.PartUse.Replaced) || slots.BlockedOn(part).Any()).ToList();
            if (parts.Count == 0) {
                GUIUtility.WithModifiers(rect.TopPartPixels(UIC.SPACED_HEIGHT), Strings.NoProstheticParts, color: Color.gray);
                return;
            }

            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, height);
            Widgets.BeginScrollView(rect, ref scroll, viewRect);

            var shown = parts.ToHashSet();

            // nested parts are indented under the closest shown part above them, as in the health tab
            int Depth(BodyPartRecord part) {
                var depth = 0;
                for (var p = part.parent; p != null; p = p.parent) {
                    if (shown.Contains(p)) depth++;
                }
                return depth;
            }

            var labelWidth = Mathf.Floor(parts.Max(part => part.LabelCap.GetWidthCached() + Depth(part) * UIC.SMALL_GAP) + UIC.SMALL_GAP);
            var x = Mathf.Floor(viewRect.x);
            var labelCol = new Rect(x, viewRect.y, labelWidth, UIC.SPACED_HEIGHT);
            var cellCol = new Rect(labelCol.xMax, viewRect.y, Mathf.Floor(viewRect.xMax - labelCol.xMax), UIC.SPACED_HEIGHT);

            GUIUtility.WithModifiers(labelCol, Strings.BodyPart, color: Color.gray, anchor: TextAnchor.MiddleLeft);
            GUIUtility.WithModifiers(cellCol, Strings.ProstheticsHeader, color: Color.gray, anchor: TextAnchor.MiddleCenter);

            var curY = viewRect.y + UIC.SPACED_HEIGHT;
            foreach (var part in parts) {
                var labelRect = new Rect(labelCol.x, curY, labelCol.width, UIC.SPACED_HEIGHT);
                labelRect.xMin += Depth(part) * UIC.SMALL_GAP;
                GUIUtility.WithModifiers(labelRect, part.LabelCap, color: Color.gray, anchor: TextAnchor.MiddleLeft);

                DrawCell(new Rect(cellCol.x, curY, cellCol.width, UIC.SPACED_HEIGHT), part, slots);
                curY += UIC.SPACED_HEIGHT;
            }

            height = curY - viewRect.y;

            Widgets.EndScrollView();
        }

        private void DrawCell(Rect rect, BodyPartRecord part, ProstheticSlots slots) {
            var placements = slots.PlacementsOn(part);
            var blocked = slots.BlockedOn(part);
            var lines = new List<string>();

            Widgets.DrawBoxSolidWithOutline(rect, Widgets.WindowBGFillColor, placements.Any() || !blocked.Any() ? Color.gray : Color.red);

            if (placements.Any()) {
                GUI.color = Panel_ShowCoverage.GetColorForTagAtIndex(component.Loadout.AllElements.FirstIndexOf(e => e.Tag == placements[0].tag));
                GUI.DrawTexture(rect.LeftPartPixels(rect.height).ContractedBy(1f), Textures.CornerTex);
                GUI.color = Color.white;

                // several implants can share a part (e.g. the brain), so their icons sit side by side, then their names
                var contents = rect.ContractedBy(3f);
                contents.AdjHorzBy(GenUI.GapTiny);
                foreach (var placement in placements) {
                    var iconRect = contents.PopLeftPartPixels(contents.height);
                    if (placement.use == ProstheticSlots.PartUse.Installed) {
                        GUIUtility.DrawItemIcon(iconRect, placement.item, false, string.Empty, placement.item.MemberFor(placement.def));
                    } else {
                        Widgets.DefIcon(iconRect, placement.def, color: FadedIconColor);
                    }

                    lines.Add(Strings.ProstheticPlacement(placement.use, placement.def.LabelCap, placement.tag.name));
                    if (placement.use != ProstheticSlots.PartUse.Replaced) {
                        var covered = Below(part).Where(p => slots.PlacementsOn(p).Any(o => o.use == ProstheticSlots.PartUse.Replaced && o.def == placement.def)).ToList();
                        if (covered.Any()) lines.Add(Strings.ProstheticCovers(string.Join(", ", covered.Select(p => p.Label))));
                    }
                }

                var installed = placements.Any(p => p.use == ProstheticSlots.PartUse.Installed);
                DrawName(contents, string.Join(", ", placements.Select(p => p.def.LabelCap.ToString()).Distinct()), installed ? Color.white : Color.gray);
            } else if (blocked.Any()) {
                var contents = rect.ContractedBy(3f);
                contents.AdjHorzBy(GenUI.GapTiny);
                Widgets.DefIcon(contents.PopLeftPartPixels(contents.height), blocked[0].Item1.RepresentativeDef, color: FadedIconColor);
                DrawName(contents, blocked[0].Item1.RepresentativeDef.LabelCap, Color.gray);
            }

            if (blocked.Any()) {
                lines.Add(Strings.ProstheticsBlocked);
                lines.AddRange(blocked.Select(tuple => $" - {tuple.Item1.Label} ({tuple.Item2.name})"));
            }

            var current = CurrentlyOn(part);
            var planned = placements.Where(p => p.use == ProstheticSlots.PartUse.Installed)
                .Select(p => ProstheticUtility.InstallRecipesFor(p.def).FirstOrDefault()?.addsHediff);
            if (!current.Select(h => h.def).ToHashSet().SetEquals(planned)) {
                var labels = current.Any() ? string.Join(", ", current.Select(h => h.LabelCap)) : Strings.NaturalPart;
                lines.Add(Strings.CurrentlyOnPart(labels));
            }

            if (lines.Any()) {
                TooltipHandler.TipRegion(rect, string.Join("\n", lines));
            }
        }

        private static void DrawName(Rect rect, string name, Color color) {
            rect.AdjHorzBy(GenUI.GapTiny);
            GUIUtility.WithModifiers(rect, name.Truncate(rect.width), color: color, anchor: TextAnchor.MiddleLeft);
        }

        private static IEnumerable<BodyPartRecord> Below(BodyPartRecord part) {
            foreach (var child in part.parts) {
                yield return child;
                foreach (var descendant in Below(child)) yield return descendant;
            }
        }

        // what the pawn has on `part` now, which a prosthetic would replace
        private List<Hediff> CurrentlyOn(BodyPartRecord part) {
            return parent.pawn.health.hediffSet.hediffs
                .Where(h => h.Part == part && (h is Hediff_AddedPart || h is Hediff_Implant || h is Hediff_MissingPart))
                .ToList();
        }

    }

}
