using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Settings : ModSettings {

        public bool immediatelyResolveLoadout = false;
        public bool onlyItemsFromLoadout = false;
        public FloatRange defaultHitpoints = FloatRange.ZeroToOne;
        public QualityRange defaultQualityRange = QualityRange.All;

        public void DoSettingsWindow(Rect rect) {
            var tRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.CheckboxLabeled(tRect, Strings.ImmediatelyResolveLoadout, ref immediatelyResolveLoadout);
            TooltipHandler.TipRegion(tRect, Strings.ImmediatelyResolveLoadoutDesc);


            var strRect = rect.PopTopPartPixels(UIC.DEFAULT_HEIGHT);
            Widgets.Label(strRect, Strings.ChangeDefaults);
            
            var defaultHpRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.FloatRange(defaultHpRect, Rand.Int, ref defaultHitpoints, 0f, 1f, Strings.HitPointsAmount, ToStringStyle.PercentZero);
            
            var qualRangeRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.QualityRange(qualRangeRect, Rand.Int, ref defaultQualityRange);

            var nRect = rect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            Widgets.CheckboxLabeled(nRect, Strings.OnlyLoadoutItems, ref onlyItemsFromLoadout);

            foreach (var keyBind in new List<KeyBindingDef> { InvKeyBindingDefOf.CL_OpenLoadoutEditor, InvKeyBindingDefOf.CL_OpenTagEditor }) {
                var keyBindRect = rect.PopTopPartPixels(34f).ContractedBy(3f);
                GenUI.SetLabelAlign(TextAnchor.MiddleLeft);
                Widgets.Label(keyBindRect, keyBind.LabelCap);
                GenUI.ResetLabelAlign();

                var vector = new Vector2(140f, 28f);
                var rect2 = new Rect(keyBindRect.x + keyBindRect.width - vector.x * 2f - 4f, keyBindRect.y, vector.x, vector.y);
                TooltipHandler.TipRegionByKey(rect2, "BindingButtonToolTip");

                if (!Widgets.ButtonText(rect2, KeyPrefs.KeyPrefsData.GetBoundKeyCode(keyBind, KeyPrefs.BindingSlot.A).ToStringReadable())) {
                    continue;
                }
                
                if (Event.current.button == 0) {
                    Find.WindowStack.Add(new Dialog_DefineBinding(KeyPrefs.KeyPrefsData, keyBind, KeyPrefs.BindingSlot.A));
                    Event.current.Use();
                } else if (Event.current.button == 1) {
                    var list = new List<FloatMenuOption> {
                        new FloatMenuOption("ResetBinding".Translate(), delegate() {
                            KeyPrefs.KeyPrefsData.SetBinding(keyBind, KeyPrefs.BindingSlot.A, keyBind.defaultKeyCodeA);
                        }),
                        new FloatMenuOption("ClearBinding".Translate(), delegate()
                        {
                            KeyPrefs.KeyPrefsData.SetBinding(keyBind, KeyPrefs.BindingSlot.A, KeyCode.None);
                        })
                    };
                    Find.WindowStack.Add(new FloatMenu(list));
                }
            }
        }

        public override void ExposeData() {
            Scribe_Values.Look(ref immediatelyResolveLoadout, nameof(immediatelyResolveLoadout), false);
            Scribe_Values.Look(ref onlyItemsFromLoadout, nameof(onlyItemsFromLoadout), false);

            Scribe_Values.Look(ref defaultHitpoints, nameof(defaultHitpoints), FloatRange.ZeroToOne);
            Scribe_Values.Look(ref defaultQualityRange, nameof(defaultQualityRange), QualityRange.All);
        }

    }

}