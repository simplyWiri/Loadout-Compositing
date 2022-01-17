using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Panel_BillConfig {

        // called from a patch method, so must be static
        public static void Draw(Dialog_BillConfig window, Listing_Standard standard, Bill_Production bill) {
            standard.Gap(6f);
            
            // Drop down
            var dropdownStr = LoadoutManager.TagFor(bill) == null
                ? Strings.PickTargetTag
                : Strings.SwitchTargetTag(LoadoutManager.TagFor(bill).name);
            
            var dropdownStrSize = Text.CalcHeight(dropdownStr, standard.ColumnWidth);
            var dropDownRect = standard.GetRect(Mathf.Max(30, dropdownStrSize + 5f));
            if (Widgets.ButtonText(dropDownRect, dropdownStr)) {
                Find.WindowStack.Add(new Dialog_TagSelector(LoadoutManager.Tags, (tag) => LoadoutManager.SetTagForBill(bill, tag), false));
            }
            
            // How many do we currently have?
            
            var text = $"{"CurrentlyHave".Translate()}: {bill.recipe.WorkerCounter.CountProducts(bill)} / {bill.DesiredTargetCount()}";
            var str = bill.recipe.WorkerCounter.ProductsDescription(bill);
            if (!str.NullOrEmpty()) {
                text += "\n" + "CountingProducts".Translate() + ": " + str.CapitalizeFirst();
            }

            standard.Label(text);

            var rect = standard.GetRect(30f);

            Text.Anchor = TextAnchor.MiddleCenter;
            // ( 3 + [ x ] ) * [ z ]

            var firstString = $"( {LoadoutManager.ColonistCountFor(bill)}  + ";
            var firstRect = rect.PopLeftPartPixels(firstString.GetWidthCached() + 5f).MiddlePartPixels(Text.LineHeight);
            window.targetCountEditBuffer ??= bill.targetCount.ToString();
            var secondRect = rect.PopLeftPartPixels(window.targetCountEditBuffer.GetWidthCached() + 17).MiddlePartPixels(Text.LineHeight);
            var secondString = " )   *   ";
            var thirdRect = rect.PopLeftPartPixels(secondString.GetWidthCached() + 5);
            window.repeatCountEditBuffer ??= bill.repeatCount.ToString();
            var fourthRect = rect.PopLeftPartPixels(window.repeatCountEditBuffer.GetWidthCached() + 17).MiddlePartPixels(Text.LineHeight);

            // need to run this before the text inputs, so we can snipe the tab keypress.
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode != KeyCode.None) {
                if (Event.current.keyCode == KeyCode.Tab) {
                    string NameFor(Rect rect) {
                        // this is how `TextFieldNumeric` is labelled according to vanilla, so we copy it.
                        return "TextField" + rect.y.ToString("F0") + rect.x.ToString("F0");
                    }
                    
                    if (!GUI.GetNameOfFocusedControl().Contains("TextField")) {
                        GUI.FocusControl(NameFor(secondRect));
                    } else if (GUI.GetNameOfFocusedControl() == NameFor(secondRect)) {
                        GUI.FocusControl(NameFor(fourthRect));
                    } else {
                        GUI.FocusControl(NameFor(secondRect));
                    }
                    Event.current.Use();
                }
            }

            Widgets.Label(firstRect, firstString);
            Widgets.Label(thirdRect, secondString);
            Widgets.DrawHighlightIfMouseover(firstRect);
            TooltipHandler.TipRegion(firstRect, Strings.NumTagsDesc);
            Widgets.TextFieldNumeric(secondRect, ref bill.repeatCount, ref window.repeatCountEditBuffer);
            TooltipHandler.TipRegion(secondRect, Strings.ExtraCopiesDesc);
            Widgets.TextFieldNumeric(fourthRect, ref bill.targetCount, ref window.targetCountEditBuffer);
            TooltipHandler.TipRegion(fourthRect, Strings.ItemRepetitionDesc);

            Text.Anchor = TextAnchor.UpperLeft;

            var producedThingDef = bill.recipe.ProducedThingDef;
            if (producedThingDef == null) return;

            var tag = LoadoutManager.TagFor(bill);
            if (tag != null && tag.HasThingDef(producedThingDef, out var item)) {
                var copyTagStr = Strings.CopyFromTag(item.Def.LabelCap);
                if (standard.ButtonText(copyTagStr)) {
                    item.Filter.CopyTo(bill.ingredientFilter);

                    bill.limitToAllowedStuff = !item.Filter.Generic;

                    if (item.Filter.SpecificQualityRange)
                        bill.qualityRange = item.Filter.QualityRange;

                    if (item.Filter.SpecificHitpointRange)
                        bill.hpRange = item.Filter.HpRange;
                }
            }

            if (producedThingDef.IsWeapon || producedThingDef.IsApparel) {
                standard.CheckboxLabeled("IncludeEquipped".Translate(), ref bill.includeEquipped);
            }

            if (producedThingDef.IsApparel && producedThingDef.apparel.careIfWornByCorpse) {
                standard.CheckboxLabeled("IncludeTainted".Translate(), ref bill.includeTainted);
            }

            if (bill.recipe.products.Any((prod) => prod.thingDef.useHitPoints)) {
                Widgets.FloatRange(standard.GetRect(28f), 975643279, ref bill.hpRange, 0f, 1f, "HitPoints", ToStringStyle.PercentZero);
                bill.hpRange.min = Mathf.Round(bill.hpRange.min * 100f) / 100f;
                bill.hpRange.max = Mathf.Round(bill.hpRange.max * 100f) / 100f;
            }

            if (producedThingDef.HasComp(typeof(CompQuality))) {
                Widgets.QualityRange(standard.GetRect(28f), 1098906561, ref bill.qualityRange);
            }

            if (producedThingDef.MadeFromStuff) {
                standard.CheckboxLabeled("LimitToAllowedStuff".Translate(), ref bill.limitToAllowedStuff);
            }
        }


    }

}