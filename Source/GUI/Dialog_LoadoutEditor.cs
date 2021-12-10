using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using UnityEngine.UI;
using Verse;
using Text = Verse.Text;

namespace Inventory
{
    public class Dialog_LoadoutEditor : Window
    {
        internal Pawn pawn;
        internal LoadoutComponent component;
        private Vector2 tagScroll;
        private float tagsHeight = 9999f;
        
        private Vector2 wearableApparelScroll;
        
        private Vector2 statsScroll;
        private float statsHeight = 9999f;

        private Panel_ShowCoverage coveragePanel = null;
        
        public override Vector2 InitialSize
        {
            get
            {
                var width = 420;
                if (drawPawnStats) width += 210;
                if (drawShowCoverage) width += 200;
                return new Vector2(width, 640);
            }
        }
        
        private bool drawShowCoverage = false;
        private bool drawPawnStats = false;
        


        public Dialog_LoadoutEditor(Pawn pawn)
        {
            this.pawn = pawn;
            this.component = pawn.GetComp<LoadoutComponent>();
            absorbInputAroundWindow = true;
            doCloseX = true;
            draggable = true;
            coveragePanel = new Panel_ShowCoverage(this);
        }
        
        public Dialog_LoadoutEditor(Pawn pawn, bool drawShowCoverage, bool drawPawnStats)
        {
            this.pawn = pawn;
            this.component = pawn.GetComp<LoadoutComponent>();
            this.drawShowCoverage = drawShowCoverage;
            this.drawPawnStats = drawPawnStats;
            absorbInputAroundWindow = true;
            doCloseX = true;
            draggable = true;
            coveragePanel = new Panel_ShowCoverage(this);
        }
        
        /* | Pawn Stats |  Apparel Slots | Add Tag     |
         * |            |                | Tag1 ^v     |
         * |            |                | Tag2 ^v     |
         * |            |                |------------ |
         * |            |                | Statistics  |
         * |            |                |             |
         */  
        public override void DoWindowContents(Rect inRect)
        {
            if (drawPawnStats)
            {
                var leftPanel = inRect.PopLeftPartPixels(210f);
                if (DrawPawnStats(leftPanel)) // returns true if selected another pawn
                {
                    return;
                }
            }
            
            var middlePanel = drawShowCoverage ? inRect.PopLeftPartPixels(420f) : inRect;

            DrawTags(middlePanel.TopPartPixels(middlePanel.height / 2.0f));
            DrawStatistics(middlePanel.BottomPartPixels((middlePanel.height - Mathf.Min(middlePanel.height/2.0f, tagsHeight)) - GUIUtility.SPACED_HEIGHT * 2f));

            if (drawShowCoverage)
            {
                coveragePanel.Draw(inRect);
            }
        }

        public bool DrawPawnStats(Rect rect)
        {
            // header [ Prev ] [ Current Pawn Name ] [ Next ]
            //          Render of Pawn with Clothes
            var topPartRect = rect.PopTopPartPixels(GUIUtility.SPACED_HEIGHT);

            var lhs = topPartRect.PopLeftPartPixels(GUIUtility.SPACED_HEIGHT);
            var rhs = topPartRect.PopRightPartPixels(GUIUtility.SPACED_HEIGHT);
            if (Widgets.ButtonImageFitted(lhs, Textures.PlaceholderTex) || Input.GetKeyDown(KeyCode.LeftArrow)) {
                ThingSelectionUtility.SelectPreviousColonist();
                this.Close();
                Find.WindowStack.Add(new Dialog_LoadoutEditor(Find.Selector.SelectedPawns.First(), drawShowCoverage, drawPawnStats ));
                return true;
            }
            TooltipHandler.TipRegion(lhs, Strings.SelectPrevious);
            if (Widgets.ButtonImageFitted(rhs, Textures.PlaceholderTex) || Input.GetKeyDown(KeyCode.RightArrow)) {
                ThingSelectionUtility.SelectNextColonist();
                this.Close();
                Find.WindowStack.Add(new Dialog_LoadoutEditor(Find.Selector.SelectedPawns.First(), drawShowCoverage, drawPawnStats));
                return true;
            }
            TooltipHandler.TipRegion(rhs, Strings.SelectNext);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(topPartRect, $"{pawn.LabelShort}");
            Text.Anchor = TextAnchor.UpperLeft;
            
            rect.PopRightPartPixels(GenUI.GapTiny);
            rect.PopTopPartPixels(GenUI.GapTiny);

            GUIUtility.ListSeperator(ref rect, Strings.TopFourSkills);
            
            var skillList = pawn.skills.skills.OrderByDescending(skill => skill.Level).ToList();
            for (int i = 0; i < 4; i++)
            {
                var skillRect = rect.PopTopPartPixels(GUIUtility.SPACED_HEIGHT);
                var skill = skillList[i];
                
                if (skill.passion > Passion.None)
                {
                    Texture2D image = (skill.passion == Passion.Major) ? SkillUI.PassionMajorIcon : SkillUI.PassionMinorIcon;
                    Widgets.DrawTextureFitted(skillRect.LeftPartPixels(24), image, 1);
                }

                float fillPercent = Mathf.Max(0.01f, (float)skill.Level / 20f);
                Widgets.FillableBar(skillRect, fillPercent, SkillUI.SkillBarFillTex, null, false);

                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = Color.gray;
                Widgets.Label(skillRect.LeftPart(0.95f), $"{skill.def.skillLabel.CapitalizeFirst()} ({skill.Level})");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                
                TooltipHandler.TipRegion(skillRect, new TipSignal(SkillUI.GetSkillDescription(skill), skill.def.GetHashCode() * 397945));
            }
            
            GUIUtility.ListSeperator(ref rect, Strings.ApparelWhichCanBeWorn);
            
            var wornApparel = component.Loadout.HypotheticalWornApparel(pawn.RaceProps.body).ToList();
            var apparels = ApparelUtility.ApparelCanFitOnBody(pawn.RaceProps.body, wornApparel.Select(td => td.Def).ToList()).ToList();
            var allocatedHeight = apparels.Count * GUIUtility.DEFAULT_HEIGHT;

            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, allocatedHeight);
            
            Widgets.BeginScrollView(rect, ref wearableApparelScroll, viewRect);
            int aIdx = 0;
            foreach (var apparel in apparels)
            {
                var apparelRect = viewRect.PopTopPartPixels(GUIUtility.DEFAULT_HEIGHT);
                if ( aIdx++ % 2 == 0 )
                    Widgets.DrawLightHighlight(apparelRect);
                Widgets.DefIcon(apparelRect.LeftPart(.15f), apparel);
                Widgets.Label(apparelRect.RightPart(.85f), apparel.LabelCap);
            }

            Widgets.EndScrollView();
            
            return false;
        }

        public void DrawTags(Rect rect)
        {
            var tags = component.Loadout.tags.ToList();
            var height = 0f;
            
            DrawHeaderButtons(ref rect, tags);
                
            rect.AdjVertBy(GenUI.GapTiny);
            height += GenUI.ListSpacing + GenUI.GapTiny;
            
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, tagsHeight);
            
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);
            
            GUIUtility.ListSeperator(ref viewRect, Strings.AppliedTags);
            height += 35;

            
            foreach (var tag in tags)
            {
                var tagIdx = tags.FindIndex(t => t == tag);
                var tagHeight = GenUI.ListSpacing * (Mathf.CeilToInt(tag.requiredItems.Count / 4.0f));
                var tagRect = viewRect.PopTopPartPixels(tagHeight );
                height += tagHeight;

                if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), Textures.PlaceholderTex)) {
                    Find.WindowStack.Add(new Dialog_TagEditor(tag));
                }
                
                if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), TexButton.DeleteX)) {
                    component.Loadout.tags.Remove(tag);
                    if ( LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList))
                    {
                        pList.pawns.Remove(pawn);
                    }
                }

                if (tagIdx != 0) {
                    if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), TexButton.ReorderUp))
                    {
                        var tmp = component.Loadout.tags[tagIdx - 1];
                        component.Loadout.tags[tagIdx - 1] = tag;
                        component.Loadout.tags[tagIdx] = tmp;
                    }
                }
                else
                {
                    tagRect.PopRightPartPixels(GenUI.ListSpacing);
                }

                if (tagIdx != tags.Count - 1) {
                    if (Widgets.ButtonImageFitted(tagRect.PopRightPartPixels(GenUI.ListSpacing).TopPartPixels(GenUI.ListSpacing), TexButton.ReorderDown)) {
                        var tmp = component.Loadout.tags[tagIdx + 1];
                        component.Loadout.tags[tagIdx + 1] = tag;
                        component.Loadout.tags[tagIdx] = tmp;
                    }
                }
                else
                {
                    tagRect.PopRightPartPixels(GenUI.ListSpacing);
                }


                Widgets.DrawBoxSolid(tagRect.PopLeftPartPixels(10.0f), Panel_ShowCoverage.coloursByIdx[tagIdx]);
                Widgets.Label(tagRect.PopLeftPartPixels(tag.name.GetWidthCached() + 10f), tag.name);

                
                var y = tagRect.y;
                
                // draw required items in blocks of 3
                for (int i = 0; i < tag.requiredItems.Count; i+= 4) {
                    for (int j = 0; j < 4; j++) {
                        var drawRect = new Rect(tagRect.x + GenUI.ListSpacing * j, y + (i/4.0f) * GenUI.ListSpacing, GenUI.ListSpacing, GenUI.ListSpacing);
                        var idx = i + j;
                        if (idx >= tag.requiredItems.Count) break;
                        var item = tag.requiredItems[idx];
                        if (item.Quantity > 1)
                        {
                            GUIUtility.FittedDefIconCount(drawRect, item.Def, item.RandomStuff, item.Quantity);
                        }
                        else
                        {
                            Widgets.DefIcon(drawRect, item.Def, item.RandomStuff);
                        }
                        TooltipHandler.TipRegion(drawRect, item.Def.LabelCap);
                    }
                }
            }

            tagsHeight = height;
            
            Widgets.EndScrollView();
        }

        public void DrawHeaderButtons(ref Rect rect, List<Tag> tags)
        {
            var buttonRect = rect.PopTopPartPixels(GenUI.ListSpacing);
            
            if (Widgets.ButtonText(buttonRect.PopLeftPartPixels(rect.width / 3f), Strings.PawnStats))
            {
                this.windowRect.width = windowRect.width + (drawPawnStats ? -210f : 210f);
                drawPawnStats = !drawPawnStats;
            }
            
            if (Widgets.ButtonText(buttonRect.LeftHalf(), Strings.AddTag))
            {
                var opts = LoadoutManager.Tags.Except(tags).Select(tag =>
                    new FloatMenuOption(tag.name, () =>
                    {
                        if ( !LoadoutManager.PawnsWithTags.TryGetValue(tag, out var pList))
                        {
                            pList = new SerializablePawnList(new List<Pawn>());
                            LoadoutManager.PawnsWithTags.Add(tag, pList);
                        }
                        pList.pawns.Add(pawn);
                        component.Loadout.tags.Add(tag);
                    })).ToList();
                
                if(opts.Count == 0)
                {
                    Messages.Message(new Message(Strings.NoTagsYetWarning, MessageTypeDefOf.RejectInput));
                }
                else
                {
                    Find.WindowStack.Add(new FloatMenu(opts));
                }
            }

            if (Widgets.ButtonText(buttonRect.RightHalf(), drawShowCoverage ? Strings.HideCoverage : Strings.ShowCoverage))
            {
                var width = 210f + coveragePanel.extraWidth;
                this.windowRect.width = windowRect.width + (drawShowCoverage ? -width : width);
                drawShowCoverage = !drawShowCoverage;
            }
        }

        public void DrawStatistics(Rect rect)
        {
            var viewRect = new Rect(rect.x, rect.y, rect.width - 16f, statsHeight);
            
            //Widgets.DrawBoxSolid(rect, Color.green);

            var height = 0f;
            Widgets.BeginScrollView(rect, ref tagScroll, viewRect);
            
            GUIUtility.ListSeperator(ref viewRect, Strings.LoadoutStatistics);

            viewRect.AdjVertBy(GenUI.GapTiny);

            var loadoutItems = component.Loadout.AllItems.ToList();
            
            GUIUtility.BarWithOverlay(
                viewRect.PopTopPartPixels(GUIUtility.SPACED_HEIGHT),
                Utility.HypotheticalEncumberancePercent(pawn, loadoutItems),
                Utility.HypotheticalUnboundedEncumberancePercent(pawn, loadoutItems) > 1f ? GUIUtility.ValvetTex as Texture2D : GUIUtility.RWPrimaryTex as Texture2D,
                Strings.Weight,
                Utility.HypotheticalGearAndInventoryMass(pawn, loadoutItems).ToString("0.#") + "/" + MassUtility.Capacity(pawn).ToStringMass(),
                Strings.WeightOverCapacity);

            height += GenUI.GapTiny + GUIUtility.SPACED_HEIGHT;

            // var statBonuses = new Dictionary<StatDef, List<Item>>();
            // var wornApparel = component.Loadout.HypotheticalWornApparel(pawn.RaceProps.body).ToList();
            // var heldItems = loadoutItems.Where(item => !item.Def.IsApparel).ToList();
            //
            // foreach (var potentialStatBoost in wornApparel.Union(heldItems))
            // {
            //     if (!potentialStatBoost.Def?.equippedStatOffsets?.Any() ?? true) continue;
            //
            //     foreach (var mod in potentialStatBoost.Def.equippedStatOffsets.Select(m => m.stat)) {
            //         if (!statBonuses.TryGetValue(mod, out var list)) {
            //             statBonuses.Add(mod, new List<Item> { potentialStatBoost });
            //             continue;
            //         }
            //         list.Add(potentialStatBoost);
            //     }
            // }
            //
            // var apparelBonuses = new List<StatDef> {
            //     StatDefOf.SharpDamageMultiplier, StatDefOf.ArmorRating_Blunt, StatDefOf.ArmorRating_Heat, 
            //     StatDefOf.Insulation_Cold, StatDefOf.Insulation_Heat
            // };
            //
            // foreach (var stat in apparelBonuses) {
            //     statBonuses.Add(stat, wornApparel);
            // }
            //
            // foreach (var statBonus in statBonuses) {
            //     TryDrawSpoofStatCalculations(ref viewRect, statBonus.Key, statBonus.Value);
            //     height += GUIUtility.SPACED_HEIGHT;
            // }
            
            Widgets.EndScrollView();
            
            statsHeight = height;
        }
        
        private void TryDrawSpoofStatCalculations(ref Rect rect, StatDef stat, List<Item> items)
        {
            var statRect = rect.PopTopPartPixels(GUIUtility.SPACED_HEIGHT);

            float statValue = stat.defaultBaseValue;
            
            
            foreach (var thing in items) {
                statValue += thing.MakeDummyThingNoId().GetStatValue(stat);
                // statValue += StatWorker.StatOffsetFromGear(thing.MakeDummyThingNoId(), stat);
            }

            Widgets.Label(statRect, stat.LabelForFullStatList);
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(statRect, statValue.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}