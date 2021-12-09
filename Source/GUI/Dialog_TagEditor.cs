﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    [HotSwappable]
    public class Dialog_TagEditor : Window
    {
        enum State
        {
            Apparel, Melee, Ranged, Medicinal, Items
        }
        

        private Vector2 curScroll = Vector2.zero;
        private Vector2 curItemScroll = Vector2.zero;

        private State curState = State.Apparel;
        private string defFilter = string.Empty;

        public Tag curTag = null;

        public override Vector2 InitialSize => new Vector2(840, 640);

        public Dialog_TagEditor()
        {
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
        }   
        public Dialog_TagEditor(Tag tag)
        {
            curTag = tag;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
        }  

        public void Draw(Rect rect)
        {
            DrawTagEditor(rect.LeftPart(.65f).TopPart(0.95f));
            if(curTag != null)
                DrawItemColumns(rect.RightPart(.35f));
        }

        public void DrawTagEditor(Rect r)
        {
            var topRect = r.TopPartPixels(22f);
            
            if (Widgets.ButtonText(topRect.LeftPart(0.33f), "Select Tag"))
            {
                var floatOpts = LoadoutManager.OptionPerTag(tag => $"{tag.name}", tag => curTag = tag);
                Find.WindowStack.Add(new FloatMenu(floatOpts));
            }
            
            topRect.AdjHorzBy(topRect.width * 0.33f);
            if (Widgets.ButtonText(topRect.LeftHalf(), "Create New Tag"))
            {
                curTag = new Tag("Placeholder");
                LoadoutManager.AddTag(curTag);
            }
            
            r.AdjVertBy(GUIUtility.DEFAULT_HEIGHT);
            
            if( curTag == null) return;

            Widgets.ListSeparator(ref r.m_YMin, r.width, $"Modify {curTag.name}");
            
            // [ Tag Name ] [ Edit Name ] 
            var rect = r.PopTopPartPixels(GUIUtility.DEFAULT_HEIGHT);

            GUIUtility.InputField(rect, "Change Tag Name", ref curTag.name);
            curTag.name ??= " ";
            
            var viewRect = new Rect(r.x, r.y, rect.width - 16f, (curTag.requiredItems.Count * GUIUtility.SPACED_HEIGHT * 2));
            Widgets.BeginScrollView(r, ref curItemScroll, viewRect);

            var visibilityRect = r;
            visibilityRect.y += curItemScroll.y;
            var baseRect = viewRect;
            
            List<Item> toRemove = new List<Item>();
            // List each item in the currently required items
            // [ Info ] [ Icon ] [ Name ] [ Edit Filter ] [ Edit Quantity ] [ Remove ]  
            foreach (var item in curTag.requiredItems)
            {
                var def = item.Def;
                var itemRect = baseRect.PopTopPartPixels(GUIUtility.SPACED_HEIGHT * 2);

                if (!itemRect.Overlaps(visibilityRect)) {
                    continue;
                }

                // Info
                Rect infoRect = itemRect.PopLeftPartPixels(GenUI.SmallIconSize);
                if (Widgets.ButtonImageFitted(infoRect.ContractedBy(1f), TexButton.Info))
                {
                    Thing thing = Utility.MakeThingWithoutID(def, item.RandomStuff, item.RandomQuality);
                    Find.WindowStack.Add(new Dialog_InfoCard(thing));
                }

                // Icon
                var iconRect = itemRect.PopLeftPartPixels(GUIUtility.SPACED_HEIGHT * 2);
                Widgets.DefIcon(iconRect, def, item.RandomStuff);

                // Remove
                var removeButton = itemRect.PopRightPartPixels(GUIUtility.SPACED_HEIGHT * 1.5f);
                if (Widgets.ButtonImageFitted(removeButton.ContractedBy(1f), TexButton.DeleteX))
                    toRemove.Add(item);
                TooltipHandler.TipRegion(removeButton, "Remove item from Tag");

                // Copy, Paste
                var copyPasteButton = itemRect.PopRightPartPixels(GUIUtility.SPACED_HEIGHT * 3);
                GUIUtility.DraggableCopyPaste(copyPasteButton, ref item.filter, Filter.CopyFrom);
                TooltipHandler.TipRegion(copyPasteButton, "Copy and paste shared attributes in filters between items in Tags");

                // Edit 
                var constrainButton = itemRect.PopRightPartPixels(GUIUtility.SPACED_HEIGHT * 1.5f);
                if (Widgets.ButtonImageFitted(constrainButton.ContractedBy(1f), TexButton.OpenStatsReport))
                    Find.WindowStack.Add(new Dialog_ItemSpecifier(item.Filter));
                TooltipHandler.TipRegion(constrainButton, "Edit the range of values which this item accepts");

                var quantityFieldRect = itemRect.PopRightPartPixels(GUIUtility.SPACED_HEIGHT * 2f);
                item.quantityStr ??= item.Quantity.ToString();
                GUIUtility.InputField(quantityFieldRect.ContractedBy(0, quantityFieldRect.height / 4.0f), "QuantityField" + item.Def.defName, ref item.quantityStr);
                if (item.quantityStr == "") 
                    item.SetQuantity(0);
                else
                    try
                    {
                        item.SetQuantity(int.Parse(item.quantityStr));
                    }
                    catch (Exception e)
                    {
                        Log.ErrorOnce($"Invalid numeric string {item.quantityStr}: " + e.Message, item.quantityStr.GetHashCode());
                    }
                TooltipHandler.TipRegion(quantityFieldRect, "Edit the quantity of items which should be held");

                // Name
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(itemRect, item.Label);
                Text.Anchor = TextAnchor.UpperLeft;
            }
            
            Widgets.EndScrollView();


            foreach (var item in toRemove)
            {
                curTag.requiredItems.Remove(item);
            }
        }

        // [ Apparel ] [ Melee ] [ Ranged ] [ Medical / Drugs ] 
        public void DrawItemColumns(Rect r)
        {
            var topRect = r.TopPartPixels(GUIUtility.DEFAULT_HEIGHT);

            void DrawOptionButton(Texture2D tex, string tooltip, State state)
            {
                var optionRect = topRect.LeftPartPixels(GUIUtility.DEFAULT_HEIGHT);
                GUI.DrawTexture(topRect.LeftPartPixels(GUIUtility.DEFAULT_HEIGHT), tex, ScaleMode.ScaleToFit, true, 1f, Color.white, 0f, 0f);
                TooltipHandler.TipRegion(optionRect, tooltip);
                if (Widgets.ButtonInvisible(optionRect)) {
                    curState = state;
                    defFilter = string.Empty;
                }
                topRect.x += GUIUtility.SPACED_HEIGHT;
            }

            r.AdjVertBy(GUIUtility.DEFAULT_HEIGHT);

            DrawOptionButton(Widgets.PlaceholderIconTex, "Apparel", State.Apparel);
            DrawOptionButton(Widgets.PlaceholderIconTex, "Melee", State.Melee);
            DrawOptionButton(Widgets.PlaceholderIconTex, "Ranged", State.Ranged);
            DrawOptionButton(Widgets.PlaceholderIconTex, "Medicine", State.Medicinal);
            DrawOptionButton(Widgets.PlaceholderIconTex, "Items", State.Items);

            switch (curState)
            {
                case State.Apparel: DrawDefList(r, ApparelUtility.ApparelCanFitOnBody(BodyDefOf.Human, curTag.requiredItems.Select(s => s.Def).Where(def => def.IsApparel).ToList()).ToList());
                    break;
                case State.Melee: DrawDefList(r, Utility.meleeWeapons);
                    break;
                case State.Ranged: DrawDefList(r, Utility.rangedWeapons);
                    break;
                case State.Medicinal: DrawDefList(r, Utility.medicinalDefs);
                    break;
                case State.Items: DrawDefList(r, Utility.items);
                    break;
            }
        }

        private void DrawDefList(Rect r, IReadOnlyList<ThingDef> defList)
        {
            var itms = curTag.requiredItems.Select(it => it.Def).ToHashSet();
            List<ThingDef> defs = defList.Where(t => !itms.Contains(t)).ToList();

            if (defFilter != string.Empty) {
                var filter = defFilter.ToLower();
                defs.RemoveAll(td => !td.LabelCap.ToString().ToLowerInvariant().Contains(filter));
            }
            
            GUIUtility.InputField(r.PopTopPartPixels(GUIUtility.SPACED_HEIGHT).ContractedBy(2f), "Def List Filter", ref defFilter);
            
            var viewRect = new Rect(r.x, r.y, r.width - 16f, (defs.Count * GUIUtility.DEFAULT_HEIGHT));
            Widgets.BeginScrollView(r, ref curScroll, viewRect);
            GUI.BeginGroup(viewRect);
            
            var rect = new Rect(0, 0, viewRect.width, GUIUtility.DEFAULT_HEIGHT);
            
            var viewFrustum = r.AtZero();
            viewFrustum.y += curScroll.y;

            for (int i = 0; i < defs.Count; i++)
            {
                if (!rect.Overlaps(viewFrustum))
                {
                    rect.y += GUIUtility.DEFAULT_HEIGHT;
                    continue;
                }

                var descRect = rect.LeftPart(0.85f);
                var def = defs[i];

                Widgets.DefIcon(descRect.LeftPart(.15f), def);
                Widgets.Label(descRect.RightPart(.85f), def.LabelCap);
                
                if (Widgets.ButtonInvisible(descRect)) {
                    AddDefToTag(def);
                }

                if (Widgets.ButtonImageFitted(rect.RightPart(0.15f).ContractedBy(2f), TexButton.Info))
                {
                    var stuff = def.MadeFromStuff ? GenStuff.AllowedStuffsFor(def).First() : null;
                    Thing thing = Utility.MakeThingWithoutID(def, stuff, QualityCategory.Normal);
                    Find.WindowStack.Add(new Dialog_InfoCard(thing));
                }

                if (i % 2 == 0)
                    Widgets.DrawLightHighlight(rect);

                Widgets.DrawHighlightIfMouseover(rect);

                rect.y += GUIUtility.DEFAULT_HEIGHT;
            }

            GUI.EndGroup();
            Widgets.EndScrollView();
        }

        private void AddDefToTag(ThingDef def)
        {
            curTag.Add(def);
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (Event.current.type == EventType.Layout)
                return;

            Text.Font = GameFont.Small;
            
            Draw(inRect);
        }
    }
}