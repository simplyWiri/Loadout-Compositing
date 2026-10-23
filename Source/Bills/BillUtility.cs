using System.Linq;
using RimWorld;
using UnityEngine;

namespace Inventory {

    public static class BillUtility {

        // precondition: the productionBill.repeatMode MUST be W_PerTag
        public static int DesiredTargetCount(this Bill_Production productionBill) {
            // This value may be 0.
            // This is valid in two situations, consider when the bill is first set,
            // the particular tag to target will not have been set yet, and ditto when
            // there are actually no pawns on the map using the tag.
            var numRepetitionsPerTag = productionBill.targetCount;
            var numOffset = productionBill.repeatCount;

            // prosthetics are only needed by the pawns who are yet to have them installed
            var tag = LoadoutManager.TagFor(productionBill);
            var item = tag?.ItemFor(productionBill.recipe.ProducedThingDef);
            if (item != null && item.IsProsthetic) {
                var numMissing = LoadoutManager.PawnsFor(productionBill)
                    .Sum(pawn => Mathf.Max(0, numRepetitionsPerTag - ProstheticUtility.InstalledCount(pawn, item.RepresentativeDef)));
                return numMissing + numOffset * numRepetitionsPerTag;
            }

            var numPawnsWithTag = LoadoutManager.ColonistCountFor(productionBill);
            return (numPawnsWithTag + numOffset) * numRepetitionsPerTag;
        }

        // precondition: the productionBill.repeatMode MUST be W_PerTag
        public static bool Satisfied(this Bill_Production productionBill)  {
            if (productionBill.suspended) {
                return false;
            }
                
            var tag = LoadoutManager.TagFor(productionBill);
            if ( productionBill.suspended || tag == null ) {
                productionBill.suspended = true;
                return false;
            }
        
            var desiredTargetCount = productionBill.DesiredTargetCount();
            if (desiredTargetCount == 0) {
                productionBill.paused = true;
                return false;
            }

            var numProducts = productionBill.recipe.WorkerCounter.CountProducts(productionBill);

            var satisfied = numProducts >= desiredTargetCount;
            if (satisfied && productionBill.pauseWhenSatisfied) {
                productionBill.paused = true;
            }

            if (!satisfied && productionBill.paused) {
                productionBill.paused = false;
            }

            return satisfied;
        }

    }

}