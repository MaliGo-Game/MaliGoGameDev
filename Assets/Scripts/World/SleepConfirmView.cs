using System;
using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.Scenarios;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.World
{
    /// <summary>
    /// "End Day {day}?" (DESIGN_SPEC §5.4.7, §4.8; sort 55): what tonight will charge, what is still owed, what is
    /// still waiting and the shift, built only from the shared bill helpers (<c>DueOnNight</c>,
    /// <c>LargestArrears</c>). Buttons Not yet / Sleep; back = Not yet. A modal (§7.2): pushes on open, pops on
    /// close, disable and destroy. On Sleep it closes (and pops) itself first, then runs the caller's action.
    /// </summary>
    public class SleepConfirmView : MonoBehaviour
    {
        const float SheetWidth = 1100f;
        const float SheetHeight = 640f;
        const float ButtonWidth = 360f;
        const int MaxLines = 5;

        Canvas canvas;
        CanvasGroup canvasGroup;
        RectTransform sheet;
        Text titleText;
        Text bodyText;
        Action onSleep;
        Action onCancel;
        bool open;

        public bool IsOpen => open;

        void Awake()
        {
            BuildUiIfNeeded();
        }

        void OnDisable()
        {
            UiModal.Pop(this);
            if (open)
            {
                open = false;
                if (canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                }
            }
        }

        void OnDestroy()
        {
            UiModal.Pop(this);
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
        }

        /// <summary>Opens the confirm for <paramref name="data"/>'s current day. <paramref name="sleep"/> runs after
        /// the confirm has closed and popped; <paramref name="cancel"/> after Not yet or back.</summary>
        public void Open(PlayerData data, Action sleep, Action cancel = null)
        {
            BuildUiIfNeeded();
            onSleep = sleep;
            onCancel = cancel;

            int day = data != null ? data.currentDay : 0;
            titleText.text = "End Day " + day + "?";
            string body = string.Join("\n", BuildLines(data));
            bodyText.text = UiTextLayout.WrapKeepingAmounts(bodyText, body, SheetWidth - 2f * UiTheme.SheetPadding);

            open = true;
            canvas.gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            UiModal.Push(this, NotYet);

            UiTween.Stop(canvasGroup);
            canvasGroup.alpha = 0f;
            UiTween.Fade(canvasGroup, 1f, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
            sheet.anchoredPosition = new Vector2(0f, -UiTheme.Motion.SheetRise);
            UiTween.Move(sheet, Vector2.zero, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
        }

        /// <summary>Closes and pops at once (no callback).</summary>
        public void Close()
        {
            open = false;
            UiModal.Pop(this);
            if (canvas != null)
            {
                UiTween.Stop(canvasGroup);
                UiTween.Stop(sheet);
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvas.gameObject.SetActive(false);
            }
        }

        void NotYet()
        {
            if (!open)
            {
                return;
            }

            Action cancel = onCancel;
            onSleep = null;
            onCancel = null;
            Close();
            cancel?.Invoke();
        }

        void Sleep()
        {
            if (!open)
            {
                return;
            }

            Action sleep = onSleep;
            onSleep = null;
            onCancel = null;
            Close();
            sleep?.Invoke();
        }

        // ================================================================ copy (§4.8)

        /// <summary>The confirm's lines for <paramref name="d"/>'s current day, at most five.</summary>
        public static List<string> BuildLines(PlayerData d)
        {
            var lines = new List<string>();
            if (d == null)
            {
                lines.Add("Nothing is due tonight.");
                return lines;
            }

            List<DueItem> due = ObligationLedger.DueOnNight(d, d.currentDay);
            var tonight = new List<string>();
            foreach (DueItem item in due)
            {
                tonight.Add("Tonight: " + Label(item.label, item.shortLabel) + " " + MoneyFormat.Rand(item.Total));
            }

            var others = new List<string>();
            Obligation owed = ObligationLedger.LargestArrears(d);
            if (owed != null)
            {
                others.Add("Still owed: " + MoneyFormat.Rand(owed.arrears) + " (" + Label(owed.label, owed.shortLabel)
                           + "). It comes off first.");
            }

            int waiting = WaitingCount(d);
            if (waiting == 1)
            {
                others.Add("One thing is still waiting today. It'll carry over to tomorrow.");
            }
            else if (waiting > 1)
            {
                others.Add(waiting + " things are still waiting today. They'll carry over to tomorrow.");
            }

            if (!WorkRules.HasWorkedToday(d))
            {
                others.Add(WorkRules.HasEnergy(d)
                    ? "You haven't taken today's shift."
                    : "You were too tired for a shift today.");
            }

            if (tonight.Count == 0)
            {
                lines.Add("Nothing is due tonight.");
            }
            else if (tonight.Count + others.Count <= MaxLines)
            {
                lines.AddRange(tonight);
            }
            else
            {
                // Too many to list: keep the biggest, sum the rest (DueOnNight is largest first).
                int keep = Math.Max(1, MaxLines - others.Count - 1);
                float rest = 0f;
                for (int i = 0; i < due.Count; i++)
                {
                    if (i < keep)
                    {
                        lines.Add(tonight[i]);
                    }
                    else
                    {
                        rest += due[i].Total;
                    }
                }

                if (rest > 0.005f)
                {
                    lines.Add("Tonight: " + (due.Count - keep) + " more, " + MoneyFormat.Rand(rest));
                }
            }

            lines.AddRange(others);
            if (lines.Count > MaxLines)
            {
                lines.RemoveRange(MaxLines, lines.Count - MaxLines);
            }

            return lines;
        }

        static string Label(string label, string shortLabel)
        {
            return string.IsNullOrEmpty(label) ? (shortLabel ?? "") : label;
        }

        /// <summary>Spots with something still waiting today (spot queue heads, as Mali counts them).</summary>
        static int WaitingCount(PlayerData data)
        {
            List<string> active = ChapterSchedule.ActiveScenarioIds(data, MaliGoFeatures.ChapterSchedule,
                data.spendingProfile?.focus, data.followUps);
            var spots = new HashSet<string>();
            foreach (string id in active)
            {
                string spot = ChapterSchedule.SpotFor(id);
                if (!string.IsNullOrEmpty(spot))
                {
                    spots.Add(spot);
                }
            }

            return spots.Count;
        }

        // ================================================================ build

        void BuildUiIfNeeded()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = UiCanvasFactory.Create("SleepConfirm_Canvas", UiTheme.Sort.SleepConfirm, transform,
                out RectTransform safeRoot);
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image scrim = UiCanvasFactory.FullBleed(canvas, "Scrim", UiTheme.Scrim);
            scrim.raycastTarget = true;

            Image sheetImage = UiKit.Panel(safeRoot, "Sheet", UiTheme.Paper, UiTheme.RadiusSheet, true);
            sheetImage.raycastTarget = true;
            sheet = sheetImage.rectTransform;
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight);
            sheet.anchoredPosition = Vector2.zero;

            float innerWidth = SheetWidth - 2f * UiTheme.SheetPadding;
            titleText = UiKit.Label(sheet, "Title", "", UiTheme.Title, UiTheme.TextPrimary, TextAnchor.UpperLeft);
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(titleText.rectTransform, UiTheme.SheetPadding, UiTheme.SheetPadding, innerWidth, 72f);

            bodyText = UiKit.Label(sheet, "Lines", "", UiTheme.Body, UiTheme.TextSecondary, TextAnchor.UpperLeft);
            float bodyTop = UiTheme.SheetPadding + 72f + UiTheme.Space20;
            SetTopLeft(bodyText.rectTransform, UiTheme.SheetPadding, bodyTop, innerWidth,
                SheetHeight - bodyTop - UiTheme.SheetPadding - UiTheme.TargetMin - UiTheme.Space20);

            Button notYet = UiKit.SecondaryButton(sheet, "Not yet", NotYet, ButtonWidth);
            var notYetRect = (RectTransform)notYet.transform;
            notYetRect.anchorMin = notYetRect.anchorMax = notYetRect.pivot = new Vector2(0f, 0f);
            notYetRect.anchoredPosition = new Vector2(UiTheme.SheetPadding, UiTheme.SheetPadding);

            Button sleep = UiKit.PrimaryButton(sheet, "Sleep", Sleep, ButtonWidth);
            var sleepRect = (RectTransform)sleep.transform;
            sleepRect.anchorMin = sleepRect.anchorMax = sleepRect.pivot = new Vector2(1f, 0f);
            sleepRect.anchoredPosition = new Vector2(-UiTheme.SheetPadding, UiTheme.SheetPadding);

            canvas.gameObject.SetActive(false);
        }

        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }
    }
}
