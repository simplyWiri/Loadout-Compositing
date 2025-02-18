using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Inventory
{
    public struct Warning
    {
        public enum Severity : int { Note = 0, Warn = 1 };

        public string message;
        public string tooltip;
        public Severity severity;
    }

    public class Dialog_ConfirmSettings : Window
    {
        Action onAccept;
        string operationStr;
        List<Warning> warningStrs;
        Vector2 scrollPos;
        float height = 0;

        public Dialog_ConfirmSettings(Action ifAccepted, string operationString, List<Warning> warnings) {
            onAccept = ifAccepted;
            operationStr = operationString;
            warningStrs = warnings;
            warningStrs.SortByDescending(w => (int)w.severity);
            height = warningStrs.Sum(str => Text.CalcHeight(str.message, (360 - 2 * Margin) - UIC.SCROLL_WIDTH));
        }

        public override Vector2 InitialSize => new Vector2(360, Mathf.Max(Mathf.Min(height, 500), 240));

        public override void DoWindowContents(Rect rect) {
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect.PopTopPartPixels(Text.CalcHeight(operationStr, rect.width)), operationStr);
            Text.Anchor = TextAnchor.UpperLeft;

            var buttonsRect = rect.PopBottomPartPixels(UIC.SPACED_HEIGHT);
            var viewRect = new Rect(rect.x, rect.y, rect.width - UIC.SCROLL_WIDTH, height);

            Widgets.BeginScrollView(rect, ref scrollPos, viewRect);
            foreach (var warning in warningStrs) {
                var strLen = Text.CalcHeight(warning.message, rect.width);
                var warnRect = rect.PopTopPartPixels(strLen);
                GUI.color = warning.severity == Warning.Severity.Note ? Color.gray : new Color(1, 0.65f, 0);
                Widgets.Label(warnRect, warning.message);
                TooltipHandler.TipRegion(warnRect, warning.tooltip);
                GUI.color = Color.white;
            }
            Widgets.EndScrollView();

            var confirmRect = buttonsRect.LeftHalf();
            var denyRect = buttonsRect.RightHalf();
            confirmRect.AdjHorzBy(-2);
            denyRect.AdjHorzBy(2);

            var buttonSize = "Confirm".GetWidthCached() + UIC.DEFAULT_HEIGHT;
            
            Text.Anchor = TextAnchor.MiddleCenter;
            if (Widgets.ButtonText(confirmRect.RightPartPixels(buttonSize), "Confirm")) {
                onAccept();
                Close();
            }
            if (Widgets.ButtonText(denyRect.LeftPartPixels(buttonSize), "Deny")) {
                Close();
            }
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
