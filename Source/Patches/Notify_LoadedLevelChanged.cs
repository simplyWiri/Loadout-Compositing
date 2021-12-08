using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Inventory
{
    [HarmonyPatch(typeof(Verse.Messages), nameof(Verse.Messages.Notify_LoadedLevelChanged))]
    public class Notify_LoadedLevelChanged
    {
        private static bool patchedDefs = false;

        private static void Postfix()
        {
            if (patchedDefs) return;
            
            foreach (var thing in DefDatabase<PawnKindDef>.AllDefs.Where(t => t.race.race.Humanlike))
            {
                var race = thing.race;
                if (race.comps.Any(c => c.compClass == typeof(LoadoutComponent))) continue;

                race.comps.Add(new CompProperties(typeof(LoadoutComponent)));
            }

            patchedDefs = true;
        }
    }
}