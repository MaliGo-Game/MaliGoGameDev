using System;
using System.Collections;
using System.Collections.Generic;
using MaliGo.Copy;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.Settings;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// The end-of-day reveal (DESIGN_SPEC §5.4.8; sort 60): "You started Day {d} with R___. You ended it with R___.
    /// Here's what moved it." Built only from the persisted DayRecord, obligations and stats (the NightResult from
    /// EndDay or from <see cref="DayCycle.Rebuild"/>; its settlement is never read), so a fresh reveal and one
    /// rebuilt after a restart are identical. Debt is shown honestly: the day's change is a neutral figure, and what
    /// is still owed (Attention colour) and every new promise sit right under it at label size; a loan's row says
    /// what goes back and when, in the transfer colour, never as plain money in.
    /// A modal (§7.2): pushes in <see cref="Show"/> (never in Awake/OnEnable), pops in Hide, OnDisable and
    /// OnDestroy. Any tap before the end jumps to the final state; the button then continues. Back = tap.
    /// Never shows XP, level, stress, grades or a score.
    /// </summary>
    public class EndOfDayRevealView : MonoBehaviour
    {
        const float MaxCardWidth = 1840f;
        const float CardHeight = 1000f;
        const float PlainCardHeight = 280f;
        const float CardPad = 40f;
        const float ColumnGap = 40f;
        const float LeftShare = 0.44f;
        const float BandHeight = 200f;
        const float Portrait = 160f;
        const float ButtonWidth = 400f;
        const float RowHeight = 70f;
        const float ArrowSize = 40f;
        const float InputLockout = 0.30f;
        /// <summary>Longest the animated sequence may run before the watchdog jumps to the final state.</summary>
        const float MaxSequenceSeconds = 30f;
        const int MaliMaxLines = 3;

        Canvas canvas;
        CanvasGroup canvasGroup;
        RectTransform safeRoot;
        Image backdrop;
        RectTransform card;
        RectTransform leftColumn, rightColumn, rowsRoot;
        Image divider;
        CanvasGroup startGroup, endGroup, summaryGroup, comingGroup, buttonGroup, maliGroup;
        Text startedLabel, startAmount, startCaption, endedLabel, endAmount, endCaption;
        RectTransform deltaPill;
        Text deltaText, splitText, owedText, promiseText;
        Text ledgerTitle, emptyText, comingTitle, comingText;
        Text maliText;
        RectTransform maliPortrait, maliTag;
        Button continueButton;
        Text continueLabel;
        readonly List<CanvasGroup> rowGroups = new List<CanvasGroup>();

        Action onDismiss;
        bool open;
        bool plain;
        bool skip;
        bool finished;
        float openedAt;
        string[] pages = Array.Empty<string>();
        int page;
        float startTotal, endTotal;
        float layoutWidth = -1f;
        Coroutine sequence;

        public bool IsOpen => open;

        void Awake()
        {
            Build();
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

        void Update()
        {
            if (!open || safeRoot == null)
            {
                return;
            }

            float width = safeRoot.rect.width;
            if (width > 0f && !Mathf.Approximately(width, layoutWidth))
            {
                Layout(width);
            }

            // Watchdog: the sequence must always end in Finish(), or the button never unlocks and the
            // game looks frozen (tester report: stuck after "You started Day 1").
            if (!finished && (sequence == null || Time.unscaledTime - openedAt > MaxSequenceSeconds))
            {
                if (sequence != null)
                {
                    StopCoroutine(sequence);
                }

                Finish();
            }
        }

        // ================================================================ public API

        /// <summary>Shows the reveal for <paramref name="night"/>; <paramref name="dismissed"/> runs after the button.</summary>
        public void Show(PlayerData data, NightResult night, Action dismissed)
        {
            if (data == null || night?.record == null)
            {
                dismissed?.Invoke();
                return;
            }

            plain = false;
            onDismiss = dismissed;
            Layout(safeRoot.rect.width > 0f ? safeRoot.rect.width : UiTheme.ReferenceWidth);
            Fill(data, night);
            Open();
            sequence = StartCoroutine(Guarded(Sequence()));
        }

        /// <summary>The plain default (reveal flag off, §7.3 step 2): Mali's one line and the button, nothing else.</summary>
        public void ShowPlain(string line, string buttonLabel, Action dismissed)
        {
            plain = true;
            onDismiss = dismissed;
            ClearRows();
            startTotal = endTotal = 0f;
            continueLabel.text = string.IsNullOrEmpty(buttonLabel) ? "Next" : buttonLabel;
            Open();
            SetMaliLine(line, line);
            SetAll(1f);
            finished = true;
            buttonGroup.interactable = true;
            maliText.text = pages.Length > 0 ? pages[0] : "";
        }

        // ================================================================ data

        void Fill(PlayerData data, NightResult night)
        {
            DayRecord record = night.record;
            startTotal = record.StartTotal;
            endTotal = record.EndTotal;

            startedLabel.text = RevealLineBuilder.StartedLine(record);
            startAmount.text = MoneyFormat.Rand(startTotal);
            endedLabel.text = RevealLineBuilder.EndedLine;
            endAmount.text = MoneyFormat.Rand(endTotal);
            deltaText.text = RevealLineBuilder.DeltaPill(record);
            splitText.text = RevealLineBuilder.SplitLine(record);

            string owed = RevealLineBuilder.StillOwedLine(data, record);
            owedText.text = owed ?? "";
            owedText.gameObject.SetActive(owed != null);
            promiseText.text = string.Join("\n", RevealLineBuilder.NewPromiseLines(data, record.day));

            ClearRows();
            List<RevealRow> rows = RevealLineBuilder.LedgerRows(data, record);
            foreach (RevealRow row in rows)
            {
                AddRow(row);
            }

            emptyText.gameObject.SetActive(rows.Count == 0);
            comingText.text = string.Join("\n", RevealLineBuilder.ComingUp(data, record.day));
            continueLabel.text = RevealLineBuilder.ButtonLabel(record);

            SetMaliLine(RevealLineBuilder.Build(data, night, true), RevealLineBuilder.Build(data, night, false));
        }

        void SetMaliLine(string withPrefix, string withoutPrefix)
        {
            float width = MaliWidth();
            string line = withPrefix ?? "";
            if (UiTextLayout.CountLines(maliText, line, width) > MaliMaxLines)
            {
                line = withoutPrefix ?? "";
            }

            pages = UiTextLayout.Paginate(maliText, line, width, MaliMaxLines);
            page = 0;
            maliText.text = "";
        }

        float MaliWidth()
        {
            float cardWidth = card != null ? card.sizeDelta.x : MaxCardWidth;
            return cardWidth - 2f * CardPad - Portrait - 24f - (ButtonWidth + 40f);
        }

        // ================================================================ open, tap, close

        void Open()
        {
            open = true;
            skip = false;
            finished = false;
            openedAt = Time.unscaledTime;
            canvas.gameObject.SetActive(true);
            card.gameObject.SetActive(true);
            card.anchoredPosition = Vector2.zero;
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            Layout(safeRoot.rect.width > 0f ? safeRoot.rect.width : UiTheme.ReferenceWidth);
            UiModal.Push(this, Tap);
            GameEvents.RaiseSoundRequested("day_end_chime");
            SetAll(0f);
            buttonGroup.interactable = false;
        }

        void Tap()
        {
            if (!open || Time.unscaledTime - openedAt < InputLockout)
            {
                return;
            }

            if (!finished)
            {
                skip = true;
                return;
            }

            if (page < pages.Length - 1)
            {
                page++;
                maliText.text = pages[page];
            }
        }

        void Continue()
        {
            if (!open || Time.unscaledTime - openedAt < InputLockout)
            {
                return;
            }

            if (!finished)
            {
                skip = true;
                return;
            }

            Hide();
            Action done = onDismiss;
            onDismiss = null;
            done?.Invoke();
        }

        /// <summary>Closes and pops at once (no callback).</summary>
        public void Hide()
        {
            open = false;
            if (sequence != null)
            {
                StopCoroutine(sequence);
                sequence = null;
            }

            UiModal.Pop(this);
            StopTweens();
            if (canvas != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                canvas.gameObject.SetActive(false);
            }
        }

        // ================================================================ sequence (§5.4.8)

        IEnumerator Sequence()
        {
            bool reduce = UiTween.ReduceMotion;
            if (!reduce)
            {
                backdrop.color = UiTheme.WithAlpha(UiTheme.Inverse, 0f);
                UiTween.Fade(backdrop, 1f, UiTheme.Motion.Backdrop);
                card.gameObject.SetActive(false);
                yield return Wait(0.4f);

                card.gameObject.SetActive(true);
                Vector2 rest = card.anchoredPosition;
                card.anchoredPosition = rest - new Vector2(0f, UiTheme.Motion.SheetRise);
                UiTween.Move(card, rest, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
                yield return Wait(0.3f);

                UiTween.Fade(startGroup, 1f);
                yield return Wait(0.2f);

                foreach (CanvasGroup row in rowGroups)
                {
                    UiTween.Fade(row, 1f);
                    yield return Wait(UiTheme.Motion.Stagger);
                }

                if (rowGroups.Count == 0)
                {
                    UiTween.Fade(emptyText, 1f);
                }

                UiTween.Fade(endGroup, 1f);
                endAmount.text = MoneyFormat.Rand(startTotal);
                UiTween.CountUp(endAmount, startTotal, endTotal, v => MoneyFormat.Rand(v));
                yield return Wait(UiTheme.Motion.Count);

                UiTween.Fade(summaryGroup, 1f);
                yield return Wait(0.2f);

                UiTween.Fade(maliGroup, 1f);
                yield return TypeLine();
            }

            Finish();
        }

        /// <summary>Steps <paramref name="routine"/> by hand so an exception inside it is logged and the reveal
        /// jumps to its final state, instead of the coroutine dying silently with the button locked.</summary>
        IEnumerator Guarded(IEnumerator routine)
        {
            // Nested routines (Wait, TypeLine) are stepped here too, so their exceptions are caught as well.
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                object current = null;
                bool yielded = false;
                try
                {
                    if (stack.Peek().MoveNext())
                    {
                        current = stack.Peek().Current;
                        if (current is IEnumerator nested)
                        {
                            stack.Push(nested);
                        }
                        else
                        {
                            yielded = true;
                        }
                    }
                    else
                    {
                        stack.Pop();
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError("[EndOfDayReveal] Sequence failed, showing the final state: " + e);
                    sequence = null;
                    try
                    {
                        Finish();
                    }
                    catch (Exception inner)
                    {
                        Debug.LogError("[EndOfDayReveal] Finish failed: " + inner);
                        finished = true;
                        buttonGroup.alpha = 1f;
                        buttonGroup.interactable = true;
                    }

                    yield break;
                }

                if (yielded)
                {
                    yield return current;
                }
            }
        }

        IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (!skip && Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        IEnumerator TypeLine()
        {
            string text = pages.Length > 0 ? pages[0] : "";
            if (!MaliGoFeatures.Typewriter || GameSettings.InstantText)
            {
                maliText.text = text;
                yield break;
            }

            float cps = Mathf.Max(1, GameSettings.CharsPerSecond);
            int shown = 0;
            float next = Time.unscaledTime;
            while (!skip && shown < text.Length)
            {
                if (Time.unscaledTime >= next)
                {
                    char c = text[shown];
                    shown++;
                    maliText.text = text.Substring(0, shown);
                    float delay = 1f / cps;
                    if (c == '.' || c == '?')
                    {
                        delay += GameSettings.PauseAfterSentence;
                    }
                    else if (c == ',')
                    {
                        delay += GameSettings.PauseAfterComma;
                    }

                    next = Time.unscaledTime + delay;
                }

                yield return null;
            }

            maliText.text = text;
        }

        void Finish()
        {
            StopTweens();
            sequence = null;
            card.gameObject.SetActive(true);
            card.anchoredPosition = Vector2.zero;
            backdrop.color = UiTheme.Inverse;
            endAmount.text = MoneyFormat.Rand(endTotal);
            SetAll(1f);
            page = 0;
            maliText.text = pages.Length > 0 ? pages[0] : "";
            finished = true;
            buttonGroup.interactable = true;
        }

        void StopTweens()
        {
            UiTween.Stop(backdrop);
            UiTween.Stop(card);
            UiTween.Stop(endAmount);
            UiTween.Stop(emptyText);
            foreach (CanvasGroup g in new[] { startGroup, endGroup, summaryGroup, comingGroup, buttonGroup, maliGroup })
            {
                UiTween.Stop(g);
            }

            foreach (CanvasGroup g in rowGroups)
            {
                UiTween.Stop(g);
            }
        }

        void SetAll(float alpha)
        {
            foreach (CanvasGroup g in new[] { startGroup, endGroup, summaryGroup, comingGroup, buttonGroup, maliGroup })
            {
                g.alpha = alpha;
            }

            foreach (CanvasGroup g in rowGroups)
            {
                g.alpha = alpha;
            }

            Color c = emptyText.color;
            c.a = alpha;
            emptyText.color = c;
            backdrop.color = UiTheme.WithAlpha(UiTheme.Inverse, plain ? 1f : (alpha > 0f ? 1f : backdrop.color.a));
            leftColumn.gameObject.SetActive(!plain);
            rightColumn.gameObject.SetActive(!plain);
            divider.gameObject.SetActive(!plain);
            if (alpha > 0f)
            {
                // Coming up and the button come last; in the final state everything is visible.
                comingGroup.alpha = 1f;
                buttonGroup.alpha = 1f;
            }
        }

        // ================================================================ rows

        void ClearRows()
        {
            foreach (CanvasGroup g in rowGroups)
            {
                if (g != null)
                {
                    Destroy(g.gameObject);
                }
            }

            rowGroups.Clear();
        }

        void AddRow(RevealRow row)
        {
            int index = rowGroups.Count;
            RectTransform rect = UiKit.Rect(rowsRoot, "Row " + index);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, RowHeight);
            rect.anchoredPosition = new Vector2(0f, -index * RowHeight);
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            rowGroups.Add(group);

            string arrowName;
            Color color;
            if (row.isLoan)
            {
                arrowName = "arrowUp";
                color = UiTheme.MoneyTransfer;
            }
            else if (row.kind == MoneyEventKind.In)
            {
                arrowName = "arrowUp";
                color = UiTheme.MoneyIn;
            }
            else if (row.kind == MoneyEventKind.Out)
            {
                arrowName = "arrowDown";
                color = UiTheme.MoneyOut;
            }
            else
            {
                arrowName = "arrow_horizontal";
                color = UiTheme.MoneyTransfer;
            }

            Image arrow = UiKit.IconImage(rect, "Arrow", arrowName, ArrowSize, color);
            var arrowRect = (RectTransform)arrow.transform;
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(ArrowSize * 0.5f, 0f);

            Text amount = UiKit.Label(rect, "Amount", row.amountText, UiTheme.LedgerValue, color, TextAnchor.MiddleRight);
            amount.horizontalOverflow = HorizontalWrapMode.Overflow;
            float amountWidth = UiTextLayout.MeasureWidth(amount, row.amountText);

            Text label = UiKit.Label(rect, "Label", row.label, UiTheme.Body, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            label.rectTransform.offsetMin = new Vector2(ArrowSize + 16f, 0f);
            label.rectTransform.offsetMax = new Vector2(-(amountWidth + 20f), 0f);
        }

        // ================================================================ layout

        void Layout(float safeWidth)
        {
            layoutWidth = safeWidth;
            float width = Mathf.Min(MaxCardWidth, safeWidth - 80f);
            float height = plain ? PlainCardHeight : CardHeight;
            card.sizeDelta = new Vector2(width, height);

            float inner = width - 2f * CardPad;
            float left = (inner - ColumnGap) * LeftShare;
            float right = inner - ColumnGap - left;
            float columnsHeight = CardHeight - 2f * CardPad - BandHeight - 26f;
            SetTopLeft(leftColumn, CardPad, CardPad, left, columnsHeight);
            SetTopLeft(rightColumn, CardPad + left + ColumnGap, CardPad, right, columnsHeight);
            SetTopLeft(divider.rectTransform, CardPad, CardPad + columnsHeight + 24f, inner, 2f);

            PlaceCaption(startAmount, startCaption);
            PlaceCaption(endAmount, endCaption, Mathf.Max(startTotal, endTotal));
            float pillWidth = UiTextLayout.MeasureWidth(deltaText, deltaText.text) + 60f;
            deltaPill.sizeDelta = new Vector2(Mathf.Max(200f, pillWidth), 80f);

            float rowsBottom = 44f + Mathf.Max(1, rowGroups.Count) * RowHeight;
            SetTopLeft(comingTitle.rectTransform, 0f, rowsBottom + 20f, right, 44f);
            SetTopLeft(comingText.rectTransform, 0f, rowsBottom + 64f, right, 88f);

            float maliWidth = MaliWidth();
            SetBottomLeft(maliPortrait, CardPad, CardPad + (BandHeight - Portrait) * 0.5f, Portrait, Portrait);
            SetBottomLeft(maliTag, CardPad + Portrait + 24f, CardPad + BandHeight - 44f, 120f, 44f);
            SetBottomLeft(maliText.rectTransform, CardPad + Portrait + 24f, CardPad, maliWidth, BandHeight - 52f);
            var buttonRect = (RectTransform)continueButton.transform;
            buttonRect.anchorMin = buttonRect.anchorMax = buttonRect.pivot = new Vector2(1f, 0f);
            buttonRect.anchoredPosition = new Vector2(-CardPad, CardPad + (BandHeight - 144f) * 0.5f);

            if (pages.Length > 0 && maliText.text.Length > 0)
            {
                maliText.text = pages[Mathf.Clamp(page, 0, pages.Length - 1)];
            }
        }

        void PlaceCaption(Text amount, Text caption, float widest = float.NaN)
        {
            string sample = float.IsNaN(widest) ? amount.text : MoneyFormat.Rand(widest);
            float w = Mathf.Max(UiTextLayout.MeasureWidth(amount, amount.text), UiTextLayout.MeasureWidth(amount, sample));
            RectTransform a = amount.rectTransform;
            RectTransform c = caption.rectTransform;
            c.anchorMin = c.anchorMax = c.pivot = new Vector2(0f, 1f);
            c.sizeDelta = new Vector2(320f, 44f);
            c.anchoredPosition = new Vector2(a.anchoredPosition.x + w + 24f, a.anchoredPosition.y - 58f);
        }

        // ================================================================ build

        void Build()
        {
            canvas = UiCanvasFactory.Create("Reveal_Canvas", UiTheme.Sort.Reveal, transform, out safeRoot);
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            backdrop = UiCanvasFactory.FullBleed(canvas, "Backdrop", UiTheme.Inverse);
            backdrop.raycastTarget = true;
            AddTap(backdrop.gameObject, backdrop);

            Image cardImage = UiKit.Panel(safeRoot, "Card", UiTheme.Paper, UiTheme.RadiusSheet, true);
            cardImage.raycastTarget = true;
            card = cardImage.rectTransform;
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(MaxCardWidth, CardHeight);
            AddTap(card.gameObject, cardImage);

            leftColumn = UiKit.Rect(card, "Left");
            rightColumn = UiKit.Rect(card, "Right");
            divider = UiKit.SpriteImage(card, "Divider", UiKit.White, 2f, UiTheme.BorderSubtle);
            divider.preserveAspect = false;

            // Left column.
            startGroup = Group(leftColumn, "Start");
            startedLabel = MakeText(startGroup, "Started", UiTheme.Body, UiTheme.TextMuted, 0f, 0f, 757f, 52f);
            startAmount = MakeText(startGroup, "StartAmount", UiTheme.DisplayAmount, UiTheme.TextPrimary, 0f, 52f, 700f, 112f);
            startCaption = MakeText(startGroup, "StartCaption", UiTheme.Label, UiTheme.TextSecondary, 0f, 0f, 320f, 44f);
            startCaption.text = RevealLineBuilder.TotalCaption;

            endGroup = Group(leftColumn, "End");
            endedLabel = MakeText(endGroup, "Ended", UiTheme.Body, UiTheme.TextMuted, 0f, 164f, 757f, 52f);
            endAmount = MakeText(endGroup, "EndAmount", UiTheme.DisplayAmount, UiTheme.TextPrimary, 0f, 216f, 700f, 112f);
            endCaption = MakeText(endGroup, "EndCaption", UiTheme.Label, UiTheme.TextSecondary, 0f, 0f, 320f, 44f);
            endCaption.text = RevealLineBuilder.TotalCaption;

            summaryGroup = Group(leftColumn, "Summary");
            Image pill = UiKit.Panel((RectTransform)summaryGroup.transform, "DeltaPill", UiTheme.Sunken, 40f, false);
            pill.raycastTarget = false;
            deltaPill = pill.rectTransform;
            deltaPill.anchorMin = deltaPill.anchorMax = deltaPill.pivot = new Vector2(0f, 1f);
            deltaPill.anchoredPosition = new Vector2(0f, -344f);
            deltaText = UiKit.Label(deltaPill, "Text", "", UiTheme.HudValue, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            deltaText.horizontalOverflow = HorizontalWrapMode.Overflow;
            splitText = MakeText(summaryGroup, "Split", UiTheme.Caption, UiTheme.TextMuted, 0f, 436f, 757f, 44f);
            owedText = MakeText(summaryGroup, "StillOwed", UiTheme.Label, UiTheme.Attention, 0f, 480f, 757f, 44f);
            promiseText = MakeText(summaryGroup, "NewPromises", UiTheme.Label, UiTheme.TextSecondary, 0f, 524f, 757f, 88f);

            // Right column.
            ledgerTitle = UiKit.Label(rightColumn, "LedgerTitle", RevealLineBuilder.LedgerTitle, UiTheme.Label, UiTheme.TextPrimary);
            SetTopLeft(ledgerTitle.rectTransform, 0f, 0f, 963f, 44f);
            rowsRoot = UiKit.Rect(rightColumn, "Rows");
            rowsRoot.anchorMin = new Vector2(0f, 1f);
            rowsRoot.anchorMax = new Vector2(1f, 1f);
            rowsRoot.pivot = new Vector2(0.5f, 1f);
            rowsRoot.sizeDelta = new Vector2(0f, RowHeight * RevealLineBuilder.MaxLedgerRows);
            rowsRoot.anchoredPosition = new Vector2(0f, -44f);
            emptyText = UiKit.Label(rowsRoot, "Empty", RevealLineBuilder.EmptyLedger, UiTheme.Label, UiTheme.TextMuted, TextAnchor.MiddleLeft);
            emptyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            emptyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            emptyText.rectTransform.pivot = new Vector2(0.5f, 1f);
            emptyText.rectTransform.sizeDelta = new Vector2(0f, RowHeight);
            emptyText.rectTransform.anchoredPosition = Vector2.zero;

            comingGroup = Group(rightColumn, "ComingUp");
            comingTitle = UiKit.Label((RectTransform)comingGroup.transform, "Title", RevealLineBuilder.ComingUpTitle, UiTheme.Label, UiTheme.TextPrimary);
            comingText = UiKit.Label((RectTransform)comingGroup.transform, "Lines", "", UiTheme.Label, UiTheme.TextSecondary);
            comingText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Bottom band.
            maliGroup = Group(card, "Mali");
            Image portrait = UiKit.SpriteImage((RectTransform)maliGroup.transform, "Portrait", UiKit.MaliPortrait, Portrait, Color.white);
            maliPortrait = (RectTransform)portrait.transform;
            Image tag = UiKit.Panel((RectTransform)maliGroup.transform, "NameTag", UiTheme.Gold, 22f, false);
            tag.raycastTarget = false;
            maliTag = tag.rectTransform;
            Text tagText = UiKit.Label(maliTag, "Name", "Mali", UiTheme.Label, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            tagText.horizontalOverflow = HorizontalWrapMode.Overflow;
            maliText = UiKit.Label((RectTransform)maliGroup.transform, "Line", "", UiTheme.Body, UiTheme.TextPrimary);
            maliText.horizontalOverflow = HorizontalWrapMode.Overflow;

            buttonGroup = Group(card, "Continue");
            continueButton = UiKit.PrimaryButton((RectTransform)buttonGroup.transform, "On to Day 2", Continue, ButtonWidth);
            continueLabel = continueButton.GetComponentInChildren<Text>();

            Layout(UiTheme.ReferenceWidth);
            canvas.gameObject.SetActive(false);
        }

        void AddTap(GameObject go, Graphic graphic)
        {
            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            button.onClick.AddListener(Tap);
        }

        static CanvasGroup Group(RectTransform parent, string name)
        {
            RectTransform rect = UiKit.Rect(parent, name);
            CanvasGroup group = rect.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            return group;
        }

        static Text MakeText(CanvasGroup group, string name, UiTheme.TextRole role, Color color, float x, float top, float width, float height)
        {
            Text text = UiKit.Label((RectTransform)group.transform, name, "", role, color);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(text.rectTransform, x, top, width, height);
            return text;
        }

        static void SetTopLeft(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -top);
        }

        static void SetBottomLeft(RectTransform rect, float x, float bottom, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, bottom);
        }
    }
}
