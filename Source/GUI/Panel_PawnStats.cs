using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Panel_PawnStats {

        public const float WIDTH = 210f;
        private Dialog_LoadoutEditor parent;

        private Vector2 wearableApparelScroll;

        public bool ShouldDraw { get; set; } = false;

        public Panel_PawnStats(Dialog_LoadoutEditor parent, bool shouldDraw = false) {
            this.parent = parent;
            this.ShouldDraw = shouldDraw;
        }

        public void Draw(Rect rect) {
            // header [ Prev ] [ Current Pawn Name ] [ Next ]

            var topPartRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            DrawHeader(topPartRect);

            rect.PopRightPartPixels(GenUI.GapTiny);
            rect.PopTopPartPixels(GenUI.GapTiny);

            GUIUtility.ListSeperator(ref rect, Strings.TopFourSkills);

            var skillList = parent.pawn.skills.skills.OrderByDescending(skill => skill.Level).ToList();
            for (var i = 0; i < 4; i++) {
                var skillRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
                var skill = skillList[i];

                if (skill.passion > Passion.None) {
                    Texture2D image = (skill.passion == Passion.Major)
                        ? SkillUI.PassionMajorIcon
                        : SkillUI.PassionMinorIcon;
                    Widgets.DrawTextureFitted(skillRect.LeftPartPixels(24), image, 1);
                }

                var fillPercent = Mathf.Max(0.01f, skill.Level / 20f);
                Widgets.FillableBar(skillRect, fillPercent, SkillUI.SkillBarFillTex, null, false);

                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = Color.gray;
                Widgets.Label(skillRect.LeftPart(0.95f), $"{skill.def.skillLabel.CapitalizeFirst()} ({skill.Level})");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;

                TooltipHandler.TipRegion(skillRect,
                    new TipSignal(() => SkillUI.GetSkillDescription(skill), skill.def.GetHashCode() * 397945));
            }

            GUIUtility.ListSeperator(ref rect, Strings.PawnTraits);

            foreach( var trait in parent.pawn.story.traits.allTraits ) {
                var traitRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);

                if (Mouse.IsOver(traitRect)) {
                    Widgets.DrawHighlight(traitRect);
                    TooltipHandler.TipRegion(traitRect, new TipSignal(() => trait.TipString(parent.pawn), parent.pawn.thingIDNumber % 89));
                }

                GUI.color = CharacterCardUtility.StackElementBackground;
                GUI.DrawTexture(traitRect.ContractedBy(1f), BaseContent.WhiteTex);
                GUI.color = Color.white;

                Widgets.Label(traitRect.ContractedBy(3f), trait.LabelCap);
            }

            GUIUtility.ListSeperator(ref rect, Strings.ApparelWhichCanBeWorn);

            var wornApparel = parent.component.Loadout.HypotheticalWornApparel(parent.shownState, parent.pawn.RaceProps.body).ToList();
            var apparels = ApparelUtility
                .ApparelCanFitOnBody(parent.pawn.RaceProps.body, wornApparel.Select(td => td.Def).ToList()).ToList();
            var allocatedHeight = apparels.Count * UIC.DEFAULT_HEIGHT;

            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, allocatedHeight);

            Widgets.BeginScrollView(rect, ref wearableApparelScroll, viewRect);
            int aIdx = 0;
            foreach (var apparel in apparels) {
                var apparelRect = viewRect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
                if (aIdx++ % 2 == 0)
                    Widgets.DrawLightHighlight(apparelRect);
                Widgets.DefIcon(apparelRect.LeftPart(.15f), apparel);
                Widgets.Label(apparelRect.RightPart(.85f), apparel.LabelCap);
            }

            Widgets.EndScrollView();
        }

        public void DrawHeader(Rect rect) {
            var lhs = rect.PopLeftPartPixels(UIC.SMALL_ICON);
            var rhs = rect.PopRightPartPixels(UIC.SMALL_ICON);

            TooltipHandler.TipRegion(lhs, Strings.SelectPrevious);
            if (Widgets.ButtonImageFitted(lhs, Textures.PreviousTex)) {
                ThingSelectionUtility.SelectPreviousColonist();
            }

            TooltipHandler.TipRegion(rhs, Strings.SelectNext);
            if (Widgets.ButtonImageFitted(rhs, Textures.NextTex)) {
                ThingSelectionUtility.SelectNextColonist();
            }

            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, $"{parent.pawn.LabelShort}");
            Text.Anchor = TextAnchor.UpperLeft;
        }

    }

}