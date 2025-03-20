using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    [HarmonyPatch(typeof(BillStack), "DoListing")]
    [StaticConstructorOnStartup]
    public class BillStack_DoListing_Patch
    {
        private static bool bwmLoaded = false;
        
        static BillStack_DoListing_Patch()
        {
            bwmLoaded = LoadedModManager.RunningModsListForReading.Any(m => m.PackageId.ToLowerInvariant() == "falconne.bwm".ToLowerInvariant());
        }
        
        public static void Postfix()
        {
            if (Find.Selector.SingleSelectedThing is not Building_WorkTable workTable) return;
            
            var ws = ITab_Bills.WinSize;
            var buttonRect = new Rect(ws.x - (ITab_Bills.PasteX + 4 + (ITab_Bills.PasteSize + 4) * (bwmLoaded ? 3 : 0)), 0, ITab_Bills.PasteSize, ITab_Bills.PasteSize);
                
            TooltipHandler.TipRegion(buttonRect, "Automatically create bills for all tags on pawns on the current map");
            if (!Widgets.ButtonImageFitted(buttonRect, Textures.PanicButtonTex, Color.white)) return;

            var map = workTable.Map;
            var pawns = map.mapPawns.FreeColonists;
            var existingBills = workTable.BillStack.Bills;
            var availableRecipes = workTable.def.AllRecipes.Where(r => r.AvailableNow).ToList();
            var producedDefs = availableRecipes.Select(r => r.ProducedThingDef).ToList();

            var tags = pawns.SelectMany(p => p.GetComp<LoadoutComponent>().Loadout.AllTags);
            var items = new Dictionary<Item, Tag>();
            foreach (var tag in tags)
            {
                foreach (var item in tag.requiredItems)
                {
                    if (producedDefs.Contains(item.Def))
                    {
                        items[item] = tag;
                    }
                }
            }
                    
            // Remove any bills which are already counted in the work table
            foreach (var existingBill in existingBills)
            {
                if (existingBill is not Bill_Production bp) continue;
                if (bp.repeatMode != InvBillRepeatModeDefOf.W_PerTag) continue;
                        
                items.RemoveAll(kp => bp.recipe.ProducedThingDef == kp.Key.Def && kp.Key.Filter.SupersetOf(bp));
            }

            foreach (var (item, tag) in items)
            {
                var recipe = availableRecipes.First(r => r.ProducedThingDef == item.Def);
                var bill = recipe.MakeNewBill() as Bill_Production;
                if (bill == null)
                {
                    Log.Error($"Failed to make bill for {item.Def.defName}");
                    continue;
                }
                        
                // Set up the bill in a reasonable manner.
                LoadoutManager.SetTagForBill(bill, tag);
                bill.repeatMode = InvBillRepeatModeDefOf.W_PerTag;
                bill.targetCount = 1;
                bill.repeatCount = 0;
                        
                item.Filter.CopyTo(bill.ingredientFilter);
                bill.limitToAllowedStuff = !item.Filter.Generic;

                if (item.Filter.SpecificQualityRange)
                    bill.qualityRange = item.Filter.QualityRange;

                if (item.Filter.SpecificHitpointRange)
                    bill.hpRange = item.Filter.HpRange;
                        
                        
                workTable.BillStack.AddBill(bill);
                
                Messages.Message($"Bill added for {item.Print()} of {tag.name}", MessageTypeDefOf.SilentInput);
            }
        }
    }
}