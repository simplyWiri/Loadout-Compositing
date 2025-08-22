using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Inventory
{
    // basically a lightweight floatmenu
    public class Dialog_SetTagLoadoutState : Window
    {
        private LoadoutElement element;
        private Color baseColor = Color.white;
        private Vector2 center;

        public override float Margin => 6f;
        public override Vector2 InitialSize => new(GetWidth, UIC.SPACED_HEIGHT + 12f);
        public float GetWidth => (element.state == null ? NullWidth : NonNullWidth) + 20 + 12;

        private float NullWidth => Strings.ActiveWhen.GetWidthCached() + 5
                                                                       + StateName(element.State).GetWidthCached() + 5
                                                                       + Strings.StateActive.GetWidthCached() + 5;

        private float NonNullWidth => Strings.ActiveWhen.GetWidthCached() + 5
                                                                          + StateName(element.State).GetWidthCached() +
                                                                          5
                                                                          + Strings.Is.GetWidthCached() + 5
                                                                          + ActiveConditionString(
                                                                                  element.ActiveCondition)
                                                                              .GetWidthCached() + 5;

        // We piece together the following sentences:
        // state != null: $"Active when { LoadoutState } is ( Active / Inactive )"
        //                  Strings.ActiveWhen  " StateName  "  Strings.Is  " ActiveConditionString "
        // state == null: $"Active when { Any } state is active";
        //                  FirstString  " StateName  "  AltThird

        private static string StateName(LoadoutState state) => $" {state?.name ?? Strings.DefaultStateNameInUse} ";

        private static string ActiveConditionString(ActiveCondition condition) =>
            condition == ActiveCondition.StateActive ? $" {Strings.Active} " : $" {Strings.Inactive} ";

        public Dialog_SetTagLoadoutState(LoadoutElement element)
        {
            layer = WindowLayer.Super;
            closeOnClickedOutside = true;
            drawShadow = false;
            preventCameraMotion = false;
            this.element = element;
            SoundDefOf.FloatMenu_Open.PlayOneShotOnCamera();
        }

        public override void SetInitialSizeAndPosition()
        {
            var vector = UI.MousePositionOnUIInverted;
            vector.x -= GetWidth / 2.0f;
            if (vector.x + InitialSize.x > UI.screenWidth)
            {
                vector.x = UI.screenWidth - InitialSize.x;
            }

            if (vector.y + InitialSize.y > UI.screenHeight)
            {
                vector.y = UI.screenHeight - InitialSize.y;
            }

            windowRect = new Rect(vector.x, vector.y, InitialSize.x, InitialSize.y);
            center = windowRect.center;
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
            if (dist > 95f)
            {
                Close(false);
                return true;
            }

            return false;
        }

        public static void Draw(Rect rect, LoadoutElement element)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;

            // "Active when "
            Widgets.Label(rect.PopLeftPartPixels(Strings.ActiveWhen.GetWidthCached() + 5), Strings.ActiveWhen);

            // If we don't have an active state, then we are always active - but we can change this. 
            if (element.State == null)
            {
                var defaultStateName = Strings.DefaultStateNameInUse;

                var dropDownRect = rect.PopLeftPartPixels(defaultStateName.GetWidthCached() + 5);
                GUIUtility.WithModifiers(
                    () => { Widgets.Dropdown(dropDownRect, element, (e) => e.State, GetElems, defaultStateName); },
                    color: Color.white);

                Widgets.Label(rect.PopLeftPartPixels(Strings.StateActive.GetWidthCached() + 5), Strings.StateActive);
            }
            else
            {
                var stateName = element.State.name;
                GUIUtility.WithModifiers(
                    () =>
                    {
                        Widgets.Dropdown(rect.PopLeftPartPixels(stateName.GetWidthCached() + 5), element,
                            (e) => e.state, GetElems, stateName);
                    }, color: Color.white);

                // 'is'
                Widgets.Label(rect.PopLeftPartPixels(Strings.Is.GetWidthCached() + 5), Strings.Is);

                GUIUtility.WithModifiers(() =>
                {
                    var conditionStr = ActiveConditionString(element.ActiveCondition);
                    if (Widgets.ButtonText(rect.PopLeftPartPixels(conditionStr.GetWidthCached()), conditionStr))
                    {
                        var oppositeCondition = element.ActiveCondition == ActiveCondition.StateActive
                            ? ActiveCondition.StateInactive
                            : ActiveCondition.StateActive;
                        element.SetLoadoutState(element.State, oppositeCondition);
                    }
                }, color: Color.white);
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GUI.color = baseColor;

            var width = GetWidth;
            if (Math.Abs(width - windowRect.width) > 0.025)
            {
                windowRect.width = width;
                windowRect.x = center.x - (width / 2.0f);
            }

            if (UpdateBaseColor())
            {
                return;
            }
            
            var rect = inRect.PopTopPartPixels(UIC.SPACED_HEIGHT);
            rect.AdjHorzBy(10f);

            Draw(rect, element);

            GUI.color = Color.white;
        }

        private static IEnumerable<Widgets.DropdownMenuElement<LoadoutState>> GetElems(LoadoutElement element)
        {
            yield return new Widgets.DropdownMenuElement<LoadoutState>()
            {
                option = new FloatMenuOption(Strings.Create,
                    () => Find.WindowStack.Add(new Dialog_LoadoutStateEditor(LoadoutManager.States))),
                payload = null,
            };

            if (element.state != null)
            {
                yield return new Widgets.DropdownMenuElement<LoadoutState>()
                {
                    option = new FloatMenuOption(Strings.DefaultStateNameInUse,
                        () => element.SetLoadoutState(null, element.ActiveCondition)),
                    payload = null
                };
            }

            foreach (var state in LoadoutManager.States.Except(element.State))
            {
                yield return new Widgets.DropdownMenuElement<LoadoutState>()
                {
                    option = new FloatMenuOption(state.name,
                        () => element.SetLoadoutState(state, element.ActiveCondition)),
                    payload = state
                };
            }
        }
    }
}