using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Panel_BillConfig {

        // called from a patch method, so must be static
        public static void Draw(Dialog_BillConfig window, Listing_Standard standard, Bill_Production bill) {
            // Drop down
            var dropdownStr = LoadoutManager.TagFor(bill) == null
                ? Strings.PickTargetTag
                : Strings.SwitchTargetTag(LoadoutManager.TagFor(bill).name);
            var dropdownStrSize = Text.CalcHeight(dropdownStr, standard.ColumnWidth);
            var dropDownRect = standard.GetRect(dropdownStrSize);
            Widgets.Dropdown(dropDownRect, bill, LoadoutManager.TagFor, GenerateTagOptions, dropdownStr);

            // How many do we currently have?
            var text = $"{"CurrentlyHave".Translate()}: {bill.recipe.WorkerCounter.CountProducts(bill)} / {bill.targetCount * LoadoutManager.ColonistCountFor(bill)}";
            var str = bill.recipe.WorkerCounter.ProductsDescription(bill);
            if (!str.NullOrEmpty()) {
                text += "\n" + "CountingProducts".Translate() + ": " + str.CapitalizeFirst();
            }

            standard.Label(text);

            // integer input for the number of items to produce
            var targetCount = bill.targetCount;
            standard.IntEntry(ref bill.targetCount, ref window.targetCountEditBuffer, bill.recipe.targetCountAdjustment);
            bill.unpauseWhenYouHave = Mathf.Max(0, bill.unpauseWhenYouHave + (bill.targetCount - targetCount));
            var producedThingDef = bill.recipe.ProducedThingDef;
            if (producedThingDef == null) return;

            var tag = LoadoutManager.TagFor(bill);
            if (tag != null && tag.HasThingDef(producedThingDef, out var item)) {
                var copyTagStr = Strings.CopyFromTag(tag.name, item.Def.LabelCap);
                var size = Text.CalcHeight(copyTagStr, standard.ColumnWidth - 80f);
                var rect = standard.GetRect(size);
                if (Widgets.ButtonText(rect, copyTagStr)) {
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
                standard.CheckboxLabeled("LimitToAllowedStuff".Translate(), ref bill.limitToAllowedStuff, null);
            }
        }

        private static IEnumerable<Widgets.DropdownMenuElement<Tag>> GenerateTagOptions(Bill_Production bill) {
            foreach (var tag in LoadoutManager.Tags.OrderBy(tag => tag.name)) {
                yield return new Widgets.DropdownMenuElement<Tag> {
                    option = new FloatMenuOption(tag.name, delegate() { LoadoutManager.SetTagForBill(bill, tag); }),
                    payload = null
                };
            }
        }


    }

}