﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Settings : ModSettings {

        public bool immediatelyResolveLoadout = false;
        public bool onlyItemsFromLoadout = false;
        public bool biasLoadBearingItems = false;
        public bool hideGizmo = false;
        public bool noPanicAlert = false;
        public bool disableCustomScroll = false;
        public FloatRange defaultHitpoints = FloatRange.ZeroToOne;
        public QualityRange defaultQualityRange = QualityRange.All;
        
        public List<Tag> genericTags = new List<Tag>();
        Panel_InterGameSettingsPanel interGamePanel = new Panel_InterGameSettingsPanel();

        private bool seenOpenKeybindingDialog = false;

        public void DoSettingsWindow(Rect rect) {

            var leftColumn = rect.LeftHalf();
            var rightColumn = rect.RightHalf();

            GUI.color = Widgets.SeparatorLineColor;
            Widgets.DrawLineVertical(rightColumn.x, rightColumn.y, rightColumn.height);
            GUI.color = Color.white;

            leftColumn.width -= UIC.SMALL_GAP / 2.0f;
            rightColumn.AdjHorzBy(UIC.SMALL_GAP / 2.0f);
            
            GUIUtility.ListSeperator(ref leftColumn, Strings.Options, true);
            DrawOptions(ref leftColumn);

            leftColumn.AdjVertBy(UIC.SMALL_GAP);

            GUIUtility.ListSeperator(ref rightColumn, Strings.Keybinds, true);
            DrawKeybinds(ref rightColumn);

            rightColumn.AdjVertBy(UIC.SMALL_GAP);
            
            GUIUtility.ListSeperator(ref rightColumn, Strings.ItemFilterDefaults, true);
            DrawDefaults(ref rightColumn);

            GUIUtility.ListSeperator(ref leftColumn, Strings.InterGameTagSaving, true, Strings.InterGameTagSavingSubheading);
            CrossGameTags(ref leftColumn);

            if ( seenOpenKeybindingDialog ) {
                seenOpenKeybindingDialog = Find.WindowStack.IsOpen<Dialog_DefineBinding>();
                if ( !seenOpenKeybindingDialog ) {
                    KeyPrefs.Save();
                }
            }

        }

        private void DrawOptions(ref Rect rect) {
            void DrawOption(ref Rect rect, string label, ref bool option, string tooltip = null) {
                var optionRect = rect.PopTopPartPixels(Text.CalcHeight(label, rect.width - UIC.SCROLL_WIDTH));
                Widgets.CheckboxLabeled(optionRect, label, ref option);
                if (tooltip != null) {
                    TooltipHandler.TipRegion(optionRect, tooltip);
                }
            }

            DrawOption(ref rect, Strings.ImmediatelyResolveLoadout, ref immediatelyResolveLoadout, Strings.ImmediatelyResolveLoadoutDesc);
            DrawOption(ref rect, Strings.BiasLoadBearingItems, ref biasLoadBearingItems, Strings.BiasLoadBearingItemsDesc);
            DrawOption(ref rect, Strings.OnlyLoadoutItems, ref onlyItemsFromLoadout, Strings.OnlyLoadoutItemsDesc);
            DrawOption(ref rect, Strings.HideGizmo, ref hideGizmo, Strings.HideGizmoDesc);
            DrawOption(ref rect, Strings.DisableCustomScroll, ref disableCustomScroll, Strings.DisableCustomScrollDesc);
            DrawOption(ref rect, Strings.NoPanicAlert, ref noPanicAlert, Strings.NoPanicAlertDesc);
        }

        private void DrawDefaults(ref Rect rect) {
            var defaultHpRect = rect.PopTopPartPixels(30f);
            defaultHpRect.CenterWithWidth(2 * rect.width / 3.0f);
            Widgets.FloatRange(defaultHpRect, Rand.Int, ref defaultHitpoints, 0f, 1f, Strings.HitPointsAmount, ToStringStyle.PercentZero);
            
            rect.AdjVertBy(UIC.SMALL_GAP);
            
            var qualRangeRect = rect.PopTopPartPixels(30f);
            qualRangeRect.CenterWithWidth( 2 * rect.width / 3.0f);
            Widgets.QualityRange(qualRangeRect, Rand.Int, ref defaultQualityRange);
        }

        private void DrawKeybinds(ref Rect rect) {
            
            foreach (var keyBind in new List<KeyBindingDef> { InvKeyBindingDefOf.CL_OpenLoadoutEditor, InvKeyBindingDefOf.CL_OpenTagEditor, InvKeyBindingDefOf.CL_PanicButton }) {
                var keyCode = KeyPrefs.KeyPrefsData.GetBoundKeyCode(keyBind, KeyPrefs.BindingSlot.A);
                void SetBinding(KeyCode code) {
                    KeyPrefs.KeyPrefsData.SetBinding(keyBind, KeyPrefs.BindingSlot.A, code);
                }
                
                var keyBindRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);

                var labelRect = keyBindRect.PopLeftPartPixels(keyBind.LabelCap.GetWidthCached() + 5);
                Widgets.Label(labelRect, keyBind.LabelCap);

                var keyRect = keyBindRect.RightPartPixels(Mathf.Max(UIC.SPACED_HEIGHT * 3, keyCode.ToStringReadable().GetWidthCached() + 5f));
                TooltipHandler.TipRegionByKey(keyRect, "BindingButtonToolTip");
                
                if (!Widgets.ButtonText(keyRect, keyCode.ToStringReadable())) continue;
                
                if (Event.current.button == 0) {
                    Find.WindowStack.Add(new Dialog_DefineBinding(KeyPrefs.KeyPrefsData, keyBind, KeyPrefs.BindingSlot.A));
                    seenOpenKeybindingDialog = true;    
                } else if (Event.current.button == 1) {
                    var list = new List<FloatMenuOption> {
                        new ("ResetBinding".Translate(), () => SetBinding(keyBind.defaultKeyCodeA)),
                        new ("ClearBinding".Translate(), () => SetBinding(KeyCode.None))
                    };
                        
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            }
        }

        private void CrossGameTags(ref Rect rect) {
            var loadoutManagerComponent = Current.Game?.GetComponent<LoadoutManager>();
            if (loadoutManagerComponent == null) {
                return;
            }

            interGamePanel.Draw(ref rect, LoadoutManager.Tags, genericTags);
        }

        public override void ExposeData() {
            Scribe_Values.Look(ref hideGizmo, nameof(hideGizmo), false);
            Scribe_Values.Look(ref immediatelyResolveLoadout, nameof(immediatelyResolveLoadout), false);
            Scribe_Values.Look(ref biasLoadBearingItems, nameof(biasLoadBearingItems), false);
            Scribe_Values.Look(ref onlyItemsFromLoadout, nameof(onlyItemsFromLoadout), false);
            Scribe_Values.Look(ref disableCustomScroll, nameof(disableCustomScroll), Application.platform == RuntimePlatform.LinuxPlayer);
            Scribe_Values.Look(ref noPanicAlert, nameof(noPanicAlert), false);
            Scribe_Values.Look(ref defaultHitpoints, nameof(defaultHitpoints), FloatRange.ZeroToOne);
            Scribe_Values.Look(ref defaultQualityRange, nameof(defaultQualityRange), QualityRange.All);

            Tag.GenericLoad = true;
            Scribe_Collections.Look(ref genericTags, nameof(genericTags), LookMode.Deep);
            Tag.GenericLoad = false;

            genericTags ??= new List<Tag>();
        }

    }

}