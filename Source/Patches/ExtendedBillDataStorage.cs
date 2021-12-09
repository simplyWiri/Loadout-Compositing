using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory
{
    [HarmonyPatch]
    public class ExtendedBillDataStorage
    {
        public static bool Prepare()
        {
            return LoadedModManager.RunningModsListForReading.Any(m => m.PackageId == "falconne.BWM".ToLowerInvariant());
        }

        public static MethodInfo TargetMethod()
        {
            return AccessTools.Method("ImprovedWorkbenches.ExtendedBillDataStorage:MirrorBills");
        }

        public static void Postfix(Bill_Production sourceBill, Bill_Production destinationBill)
        {
            if (sourceBill.repeatMode == InvBillRepeatModeDefOf.W_PerTag)
            {
                var tag = LoadoutManager.TagFor(sourceBill);    
                LoadoutManager.SetTagForBill(destinationBill, tag);
            }
        }
    }
}