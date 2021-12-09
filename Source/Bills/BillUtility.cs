using RimWorld;

namespace Inventory
{
    public static class BillUtility
    {
        // precondition: the productionBill.repeatMode MUST be W_PerTag
        public static int DesiredTargetCount(this Bill_Production productionBill)
        {
            // This value may be 0.
            // This is valid in two situations, consider when the bill is first set,
            // the particular tag to target will not have been set yet, and ditto when
            // there are actually no pawns on the map using the tag.
            var numPawnsWithTag = LoadoutManager.ColonistCountFor(productionBill);
            var numRepetitionsPerTag = productionBill.targetCount;
            
            return numPawnsWithTag * numRepetitionsPerTag;
        }
        
        // precondition: the productionBill.repeatMode MUST be W_PerTag
        public static bool Satisfied(this Bill_Production productionBill)
        {
            var desiredTargetCount = productionBill.DesiredTargetCount();
            if (desiredTargetCount == 0 ) {
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