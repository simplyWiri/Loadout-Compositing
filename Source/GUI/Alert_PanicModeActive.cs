using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Inventory
{
    public class Alert_PanicModeActive : Alert
    {
        public Alert_PanicModeActive()
        {
            this.defaultLabel = Strings.PanicStateAlert;
            this.defaultPriority = AlertPriority.Medium;
        }

        public override AlertReport GetReport()
        {
            if ( ModBase.settings.noPanicAlert ) {
                return false;
            }

            if ( !LoadoutManager.ActivePanicState() ) {
                return false;
            }

            this.defaultExplanation = Strings.PanicStateAlertDesc(LoadoutManager.PanicState);
            return AlertReport.CulpritsAre(PanicModePawns().ToList());
        }

        public IEnumerable<Pawn> PanicModePawns() {
            var maps = Find.Maps;
            foreach (var map in maps) {
                var pawns = map.mapPawns.FreeColonistsSpawned.Where(p => p.IsValidLoadoutHolder());
                foreach (var pawn in pawns.Where(p => p.TryGetComp<LoadoutComponent>().Loadout.InPanicMode)) {
                    yield return pawn;
                }
            }
        }
    }
}
