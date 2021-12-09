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
            
            var loadoutComp = p.GetComp<LoadoutComponent>();

            List<Tag> toRemove = new List<Tag>();
            

            foreach (var tag in loadoutComp.Loadout.tags)
            {
                var r = new Rect(0, curY, width - GUIUtility.DEFAULT_HEIGHT, GUIUtility.DEFAULT_HEIGHT);
                GUIUtility.DrawTag(r, tag);
                if (Widgets.ButtonImageFitted(new Rect(0 + width - GUIUtility.DEFAULT_HEIGHT, curY, GUIUtility.DEFAULT_HEIGHT, GUIUtility.DEFAULT_HEIGHT), TexButton.DeleteX))
                {
                    toRemove.Add(tag);
                }
                curY += GUIUtility.DEFAULT_HEIGHT;
            }

            foreach (var tag in toRemove)
            {
                loadoutComp.Loadout.tags.Remove(tag);
            }
        }
        
        // [ Add Tag ] [ Edit Tags ] [ Create Tag from Pawn ] 
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