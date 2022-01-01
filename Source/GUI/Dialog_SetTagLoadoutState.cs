using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Inventory {

    // basically a lightweight floatmenu
    public class Dialog_SetTagLoadoutState : Window {

        private Loadout loadout;
        private LoadoutElement element;
        private Color baseColor = Color.white;

        public override float Margin => 6f;
        public override Vector2 InitialSize => new Vector2(GetWidth, UIC.SPACED_HEIGHT + 12f);
        public float GetWidth => (element.state == null ? NullWidth : NonNullWidth) + 20 + 12;

        private float NullWidth => FirstString.GetWidthCached() + 5
                                + StateName.GetWidthCached() + 5
                                + AltThird.GetWidthCached() + 5;

        private float NonNullWidth => FirstString.GetWidthCached() + 5
                                   + StateName.GetWidthCached() + 5
                                   + ThirdString.GetWidthCached() + 5
                                   + SwitchName.GetWidthCached() + 5;
        
        private string FirstString => Strings.ActiveWhen;
        private string StateName => $" {element.state?.name ?? Strings.DefaultStateNameInUse} ";
        private string ThirdString => Strings.Is;
        private string AltThird => Strings.StateActive;
        private string SwitchName => element.Switch ? $" {Strings.Active} " : $" {Strings.Inactive} ";
        
        public Dialog_SetTagLoadoutState(Loadout loadout, LoadoutElement element) {
            layer = WindowLayer.Super;
            closeOnClickedOutside = true;
            drawShadow = false;
            preventCameraMotion = false;
            this.element = element;
            this.loadout = loadout;
            SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
        }
        
        public override void SetInitialSizeAndPosition()
        {
            var vector = UI.MousePositionOnUIInverted;
            vector.x -= GetWidth / 2.0f;
            if (vector.x + InitialSize.x > UI.screenWidth) {
                vector.x = UI.screenWidth - InitialSize.x;
            }
            if (vector.y + InitialSize.y > UI.screenHeight) {
                vector.y = UI.screenHeight - InitialSize.y;
            }
            
            windowRect = new Rect(vector.x, vector.y, InitialSize.x, InitialSize.y);
        }
        
        private bool UpdateBaseColor()
        {
            baseColor = Color.white;
            var rect = windowRect;
            rect.x = 0;
            rect.y = 0;
            if (rect.Contains(Event.current.mousePosition)) return false;
            
            var dist = GenUI.DistFromRect(rect, Event.current.mousePosition);
            baseColor = new Color(1f, 1f, 1f, 1f - dist / 95f);
            if (dist > 95f) {
                Close(false);
                return true;
            }

            return false;
        }
        
        // state != null: $"Active when { LoadoutState } is ( Active / Inactive )"
        // state == null: $"Active when { Any } state is active";
        public override void DoWindowContents(Rect inRect) {
            var width = GetWidth;
            if (Math.Abs(width - windowRect.width) > 0.025) {
                var delta = width - windowRect.width;
                windowRect.width += delta;
                windowRect.x -= delta / 2.0f;
            }

            if (UpdateBaseColor()) {
                return;
            }

            GUI.color = baseColor;

            var rect = inRect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            rect.AdjHorzBy(10f);
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;


            if (element.state == null) {
                Widgets.Label(rect.PopLeftPartPixels(FirstString.GetWidthCached() + 5), FirstString);
                Widgets.Dropdown(rect.PopLeftPartPixels(StateName.GetWidthCached() + 5), element, (elem) => elem.state, GetElems, StateName);
                Widgets.Label(rect.PopLeftPartPixels(AltThird.GetWidthCached() + 5), AltThird);
            }
            else {
                Widgets.Label(rect.PopLeftPartPixels(FirstString.GetWidthCached() + 5), FirstString);
                Widgets.Dropdown(rect.PopLeftPartPixels(StateName.GetWidthCached() + 5), element, (elem) => elem.state, GetElems, StateName);
                Widgets.Label(rect.PopLeftPartPixels(ThirdString.GetWidthCached() + 5), ThirdString);
                Widgets.Dropdown(rect.PopLeftPartPixels(SwitchName.GetWidthCached() + 5), element, (elem) => elem.Switch, GetSwitchValueOptions, SwitchName );
            }

            Text.Anchor = TextAnchor.UpperLeft;
            
            GUI.color = Color.white;
        }

        private IEnumerable<Widgets.DropdownMenuElement<bool>> GetSwitchValueOptions(LoadoutElement element) {
            yield return new Widgets.DropdownMenuElement<bool>() {
                option = new FloatMenuOption("Active", () => element.SetTo(loadout, element.state, true)),
                payload = true
            };
            yield return new Widgets.DropdownMenuElement<bool>() {
                option = new FloatMenuOption("Inactive", () => element.SetTo(loadout, element.state, false)),
                payload = false
            };
        }
        private IEnumerable<Widgets.DropdownMenuElement<LoadoutState>> GetElems(LoadoutElement element) {
            if (element.state != null) {
                yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                    option = new FloatMenuOption(Strings.DefaultStateNameInUse, () => element.SetTo(loadout, null, element.Switch)),
                    payload = null
                };
            }

            foreach (var state in LoadoutManager.States.Except(element.state)) {
                yield return new Widgets.DropdownMenuElement<LoadoutState>() {
                    option = new FloatMenuOption(state.name, () => element.SetTo(loadout, state, element.Switch)),
                    payload = state
                };
            }
        }
    }

}