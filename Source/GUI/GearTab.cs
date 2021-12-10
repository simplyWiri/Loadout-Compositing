using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace Inventory
{
    [HotSwappable]
    public static class GearTab
    {
        public static void DrawTags(Pawn p, ref float curY, float width)
        {
            if (!p.IsColonist || p.IsQuestLodger() || p.apparel.AnyApparelLocked ) return;

            Widgets.ListSeparator(ref curY, width, Strings.TagLoadoutComposition);
            var rect = new Rect(0, curY, width, GUIUtility.DEFAULT_HEIGHT);
            curY += GUIUtility.DEFAULT_HEIGHT;
            
            DrawButtons(rect, p);
        }
        
        public static void DrawButtons(Rect rect, Pawn p)
        {
            if (Widgets.ButtonText(rect.LeftHalf(), Strings.EditXLoadout(p.LabelShort)))
            {
                Find.WindowStack.Add(new Dialog_LoadoutEditor(p));
            }

            if (Widgets.ButtonText(rect.RightHalf(), Strings.EditOrCreateTags))
            {
                Find.WindowStack.Add(new Dialog_TagEditor());
            }
        }
    }
}