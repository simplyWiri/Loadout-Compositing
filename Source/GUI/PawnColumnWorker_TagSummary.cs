using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    public class PawnColumnWorker_TagSummary : PawnColumnWorker
    {
        private Dictionary<Tag, int> colours = new Dictionary<Tag, int>();
        
        public Color tagColour(Tag t)
        {
            if (!colours.TryGetValue(t, out var idx)) {
                idx = colours.Count;
                colours.Add(t, idx);
            } 

            return Panel_ShowCoverage.GetColorForTagAtIndex(idx);
        }
        
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if ( !pawn.IsValidLoadoutHolder() ) {
                return;
            }

            var loadout = pawn.TryGetComp<LoadoutComponent>().Loadout;
            var elemNames = loadout.elements.Select(e => e.Tag.name.Colorize(tagColour(e.Tag)));

            Widgets.Label(rect, string.Join(", ", elemNames));
        }
        
        public override int Compare(Pawn a, Pawn b) {
            var aState = a.GetActiveState();
            var bState = b.GetActiveState();
            if (aState == null && bState == null) return 0;
            if (aState == null) return 1;
            if (bState == null) return -1;

            return String.Compare(aState.name, bState.name, StringComparison.Ordinal);
        }
        
        public override int GetMinWidth(PawnTable table) {
            var length = table.cachedPawns.Where(p => p.IsValidLoadoutHolder()).Max(p => string.Join(", ", p.GetComp<LoadoutComponent>().Loadout.elements.Select(e => e.Tag.name)).GetWidthCached());
            return Mathf.Max(base.GetMinWidth(table), Mathf.CeilToInt(length));
        }

        public override int GetOptimalWidth(PawnTable table) {
            return Mathf.Clamp(239, GetMinWidth(table), GetMaxWidth(table));
        }
    }
}