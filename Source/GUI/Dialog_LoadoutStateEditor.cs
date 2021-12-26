using System.Linq;
using UnityEngine;
using Verse;

namespace Inventory {

    public class Dialog_LoadoutStateEditor : Window {

        private string loadoutStateString = string.Empty;
        private Vector2 scrollPos = Vector2.zero;
        public override Vector2 InitialSize => new Vector2(320, 320);

        
        public override void DoWindowContents(Rect inRect) {
            var topPrt = inRect.PopTopPartPixels(UIC.SPACED_HEIGHT);

            if (Widgets.ButtonText(topPrt.PopRightPartPixels(100), Strings.Create)) {
                LoadoutManager.States.Add(new LoadoutState(loadoutStateString));
                loadoutStateString = string.Empty;
            }
            
            GUIUtility.InputField(topPrt.LeftPart(0.85f), "NameLoadoutState", ref loadoutStateString);

            var viewRect = inRect;
            viewRect.width -= 16f;
            viewRect.height = UIC.SPACED_HEIGHT * LoadoutManager.States.Count;
            Widgets.BeginScrollView(inRect, ref scrollPos, inRect);

            foreach (var state in LoadoutManager.States.ToList()) {
                var rowRect = viewRect.PopTopPartPixels(UIC.SPACED_HEIGHT);
                
                if(Widgets.ButtonImage(rowRect.PopRightPartPixels(UIC.SMALL_ICON), TexButton.DeleteX)) {
                    LoadoutManager.RemoveState(state);
                }
                
                Widgets.Label(rowRect, state.name);
            }
            
            Widgets.EndScrollView();
        }

    }

}