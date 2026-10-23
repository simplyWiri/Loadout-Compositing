using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory
{
    // surgeries for tagged prosthetics are queued suspended by default, for the player to approve
    public class Alert_SurgeryAwaitingApproval : Alert
    {
        public Alert_SurgeryAwaitingApproval()
        {
            this.defaultLabel = Strings.SurgeryAwaitingApproval;
            this.defaultPriority = AlertPriority.Medium;
        }

        public override AlertReport GetReport()
        {
            var bills = AwaitingBills().ToList();
            if (bills.Count == 0) {
                return false;
            }

            var lines = bills.Select(pair => $"  - {pair.Item1.LabelShort}: {pair.Item2.LabelCap}");
            this.defaultExplanation = Strings.SurgeryAwaitingApprovalDesc + "\n\n" + string.Join("\n", lines);
            return AlertReport.CulpritsAre(bills.Select(pair => pair.Item1).Distinct().ToList());
        }

        // suspended surgeries which install a prosthetic one of the pawn's active tags asks for
        private static IEnumerable<System.Tuple<Pawn, Bill>> AwaitingBills() {
            foreach (var map in Find.Maps) {
                foreach (var pawn in map.mapPawns.AllPawnsSpawned.Where(p => p.IsValidLoadoutHolder())) {
                    var bills = pawn.BillStack?.Bills;
                    if (bills.NullOrEmpty()) continue;

                    var loadout = pawn.TryGetComp<LoadoutComponent>().Loadout;
                    foreach (var bill in bills.Where(b => b.suspended)) {
                        var def = ProstheticUtility.ProstheticInstalledBy(bill.recipe);
                        if (def != null && loadout.Items.Any(item => item.IsProsthetic && item.RepresentativeDef == def)) {
                            yield return new System.Tuple<Pawn, Bill>(pawn, bill);
                        }
                    }
                }
            }
        }
    }
}
