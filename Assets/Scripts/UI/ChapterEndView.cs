using System;
using System.Collections;
using System.Collections.Generic;
using MaliGo.Copy;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.Settings;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// The chapter end (DESIGN_SPEC §1.3, §5.4.9; sort 60), four screens: A the week (start and end totals, what is
    /// still owed and already promised for payday at body size beside the total, what moved it most, the bills
    /// line), B what Mali noticed, C the payday plan, D close with "Live the week again". Numbers come only from
    /// <see cref="ChapterReflection"/>; no screen shows a score, a ratio or a previous run's total.
    /// A modal (§7.2): pushes in <see cref="Show"/>, pops in Hide, OnDisable and OnDestroy.
    /// </summary>
    public class ChapterEndView : MonoBehaviour
    {
        const float Margin = 40f;
        const float LeftShare = 0.34f;
        const float CircleSize = 720f;
        const float WaveHeight = 640f;
        const float CardWidth = 360f;
        const float CardHeight = 200f;
        const float CardGap = 20f;
        const float NextWidth = 320f;
        const float LiveWidth = 480f;

        Canvas canvas;
        RectTransform safeRoot;
        RectTransform left;
        RectTransform right;
        RectTransform screen;
        RectTransform circleRect;
        RectTransform waveRect;
        Action onLiveAgain;
        PlayerData data;
        bool open;
        int current;
        bool planSaved;
        string pickedPlan;
        Button saveButton;
        readonly List<Image> planCards = new List<Image>();
        readonly List<Image> planRings = new List<Image>();
        readonly List<Image> planChecks = new List<Image>();
        float layoutWidth = -1f;

        // Screen B typing.
        readonly List<Text> noticedTexts = new List<Text>();
        string[] noticedLines = Array.Empty<string>();
        bool typingSkip;
        bool typingDone;
        RectTransform nextB;
        Coroutine typing;

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
                // A width change (often the first frame after Show) rebuilds the current screen in place: no chime,
                // finished typing stays finished, a picked plan stays picked.
                Layout(width);
                ShowScreen(current, true);
            }
        }

        /// <summary>Shows screen A. <paramref name="liveAgain"/> runs on "Live the week again" (screen D).</summary>
        public void Show(PlayerData player, Action liveAgain)
        {
            data = player;
            onLiveAgain = liveAgain;
            planSaved = false;
            pickedPlan = null;
            open = true;
            canvas.gameObject.SetActive(true);
            Layout(safeRoot.rect.width > 0f ? safeRoot.rect.width : UiTheme.ReferenceWidth);
            UiModal.Push(this, Back);
            ShowScreen(0);
            // Once per Show (Â§5.4.9), not in BuildWeek, which a layout rebuild may run again.
            GameEvents.RaiseSoundRequested("day_end_chime");
        }

        /// <summary>Closes and pops (no callback).</summary>
        public void Hide()
        {
            open = false;
            UiModal.Pop(this);
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }
        }

        void Back()
        {
            switch (current)
            {
                case 0: ShowScreen(1); break;
                case 1: TapB(); break;
                case 2: NotNow(); break;
            }
        }

        void ShowScreen(int index, bool rebuild = false)
        {
            bool keepTyped = rebuild && index == 1 && current == 1 && (typingDone || typingSkip);
            current = index;
            if (typing != null)
            {
                StopCoroutine(typing);
                typing = null;
            }

            for (int i = screen.childCount - 1; i >= 0; i--)
            {
                Destroy(screen.GetChild(i).gameObject);
            }

            screen.DetachChildren();
            switch (index)
            {
                case 0: BuildWeek(); break;
                case 1: BuildNoticed(keepTyped); break;
                case 2: BuildPlan(); break;
                default: BuildClose(); break;
            }
        }

        // ================================================================ screen A: the week

        void BuildWeek()
        {
            ChapterSummary s = ChapterReflection.Summary(data);
            float width = right.rect.width > 0f ? right.rect.width : 1187f;
            float y = 0f;

            Add(ChapterReflection.Title, UiTheme.DisplayTitle, UiTheme.TextPrimary, 0f, y, width, 80f);
            y += 96f;

            TotalRow(ChapterReflection.StartLabel, s.startTotal, y);
            y += 112f;
            TotalRow(ChapterReflection.EndLabel, s.endTotal, y);
            y += 112f;

            Text weekText = Add(ChapterReflection.WeekPill(s), UiTheme.HudValue, UiTheme.TextPrimary, 0f, 0f, 400f, 72f);
            float pillWidth = UiTextLayout.MeasureWidth(weekText, weekText.text) + 60f;
            Image pill = UiKit.Panel(screen, "WeekPill", UiTheme.Sunken, 36f, false);
            pill.raycastTarget = false;
            SetTopLeft(pill.rectTransform, 0f, y, pillWidth, 72f);
            pill.transform.SetSiblingIndex(weekText.transform.GetSiblingIndex());
            weekText.alignment = TextAnchor.MiddleCenter;
            SetTopLeft(weekText.rectTransform, 0f, y, pillWidth, 72f);
            Add(ChapterReflection.SplitLine(s), UiTheme.Caption, UiTheme.TextMuted, pillWidth + 24f, y + 18f, width - pillWidth - 24f, 44f);
            y += 72f + 16f;

            string owed = ChapterReflection.StillOwedLine(s);
            if (owed != null)
            {
                string amount = MoneyFormat.Rand(s.stillOwed);
                string rich = owed.Replace(amount, "<color=#" + ColorUtility.ToHtmlStringRGB(UiTheme.Attention) + ">" + amount + "</color>");
                Add(rich, UiTheme.Body, UiTheme.TextPrimary, 0f, y, width, 52f);
                y += 52f;
            }

            string promised = ChapterReflection.PromisedLine(s);
            if (promised != null)
            {
                Add(promised, UiTheme.Body, UiTheme.TextPrimary, 0f, y, width, 52f);
                y += 52f;
                Add(ChapterReflection.PromisedList(s), UiTheme.Caption, UiTheme.TextSecondary, 0f, y, width, 40f);
                y += 40f;
            }

            y += 20f;
            Add(ChapterReflection.MomentsTitle, UiTheme.Label, UiTheme.TextPrimary, 0f, y, width, 44f);
            y += 44f + 12f;

            if (s.top.Length == 0)
            {
                Add(ChapterReflection.NoMoments, UiTheme.Body, UiTheme.TextSecondary, 0f, y, width, CardHeight);
            }

            for (int i = 0; i < s.top.Length; i++)
            {
                MomentCard(s.top[i], s.topDays[i], i * (CardWidth + CardGap), y);
            }

            y += CardHeight + 12f;

            string bills = ChapterReflection.BillsLine(s);
            if (bills != null)
            {
                Add(bills, UiTheme.Body, UiTheme.TextPrimary, 0f, y + 46f, 704f, 52f);
            }

            NextButton("Next", () => ShowScreen(1), y);
        }

        void TotalRow(string label, float total, float y)
        {
            Text l = Add(label, UiTheme.Body, UiTheme.TextSecondary, 0f, y + 46f, 340f, 52f);
            float x = UiTextLayout.MeasureWidth(l, label) + 24f;
            Text amount = Add(MoneyFormat.Rand(total), UiTheme.DisplayAmount, UiTheme.TextPrimary, x, y, 600f, 112f);
            float w = UiTextLayout.MeasureWidth(amount, amount.text);
            Add(ChapterReflection.TotalCaption, UiTheme.Label, UiTheme.TextSecondary, x + w + 24f, y + 52f, 320f, 44f);
        }

        void MomentCard(MoneyEvent e, int day, float x, float y)
        {
            Image card = UiKit.Panel(screen, "Moment", UiTheme.Card, UiTheme.RadiusCard, true);
            card.raycastTarget = false;
            SetTopLeft(card.rectTransform, x, y, CardWidth, CardHeight);
            RectTransform r = card.rectTransform;
            float inner = CardWidth - 48f;

            Text dayText = UiKit.Label(r, "Day", ChapterReflection.MomentDay(day), UiTheme.Caption, UiTheme.TextMuted);
            SetTopLeft(dayText.rectTransform, 24f, 18f, inner, 40f);
            Text label = UiKit.Label(r, "Label", "", UiTheme.Label, UiTheme.TextPrimary);
            SetTopLeft(label.rectTransform, 24f, 58f, inner, 88f);
            UiTextLayout.WrapKeepingAmounts(label, e.label, inner);

            bool loan = e.category == MoneyCategory.Loan && e.kind == MoneyEventKind.In;
            Color color = loan ? UiTheme.MoneyTransfer : e.kind == MoneyEventKind.In ? UiTheme.MoneyIn : UiTheme.MoneyOut;
            Text amount = UiKit.Label(r, "Amount", MoneyFormat.Signed(e.TotalDelta), UiTheme.HudValue, color);
            amount.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(amount.rectTransform, 24f, CardHeight - 64f, inner, 48f);
        }

        // ================================================================ screen B: what Mali noticed

        void BuildNoticed(bool alreadyTyped)
        {
            float width = right.rect.width > 0f ? right.rect.width : 1187f;
            Add(ChapterReflection.NoticedTitle, UiTheme.Title, UiTheme.TextPrimary, 0f, 0f, width, 72f);

            noticedLines = ChapterReflection.Noticed(data);
            noticedTexts.Clear();
            float y = 72f + 40f;
            var tapCatcher = UiKit.Rect(screen, "Tap");
            Image catcher = tapCatcher.gameObject.AddComponent<Image>();
            catcher.sprite = UiKit.White;
            catcher.color = Color.clear;
            AddButton(tapCatcher.gameObject, catcher, TapB);

            foreach (string line in noticedLines)
            {
                Text t = Add("", UiTheme.Dialogue, UiTheme.TextPrimary, 0f, y, width, 180f);
                int lines = Mathf.Max(1, UiTextLayout.CountLines(t, line, width));
                t.rectTransform.sizeDelta = new Vector2(width, lines * 60f);
                noticedTexts.Add(t);
                y += lines * 60f + 30f;
            }

            nextB = NextButton("Next", () => ShowScreen(2), 1000f - 144f - 0f, false);
            nextB.gameObject.SetActive(false);
            typingDone = false;
            typingSkip = alreadyTyped; // a rebuild after the lines were typed (or skipped) shows them at once
            typing = StartCoroutine(TypeNoticed(width));
        }

        IEnumerator TypeNoticed(float width)
        {
            bool instant = !MaliGoFeatures.Typewriter || GameSettings.InstantText;
            for (int i = 0; i < noticedLines.Length; i++)
            {
                string wrapped = UiTextLayout.Wrap(noticedTexts[i], noticedLines[i], width);
                Text t = noticedTexts[i];
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                if (instant || typingSkip)
                {
                    t.text = wrapped;
                    continue;
                }

                float cps = Mathf.Max(1, GameSettings.CharsPerSecond);
                int shown = 0;
                float next = Time.unscaledTime;
                while (!typingSkip && shown < wrapped.Length)
                {
                    if (Time.unscaledTime >= next)
                    {
                        char c = wrapped[shown++];
                        t.text = wrapped.Substring(0, shown);
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

                t.text = wrapped;
                if (!typingSkip)
                {
                    float until = Time.unscaledTime + 0.4f;
                    while (!typingSkip && Time.unscaledTime < until)
                    {
                        yield return null;
                    }
                }
            }

            typingDone = true;
            typing = null;
            if (nextB != null)
            {
                nextB.gameObject.SetActive(true);
            }
        }

        void TapB()
        {
            if (current != 1)
            {
                return;
            }

            if (!typingDone)
            {
                typingSkip = true;
                return;
            }

            ShowScreen(2);
        }

        // ================================================================ screen C: payday plan

        void BuildPlan()
        {
            float width = right.rect.width > 0f ? right.rect.width : 1187f;
            Add(ChapterReflection.PlanPrompt1, UiTheme.Title, UiTheme.TextPrimary, 0f, 0f, width, 72f);
            Text prompt = Add("", UiTheme.Body, UiTheme.TextSecondary, 0f, 88f, width, 110f);
            UiTextLayout.WrapKeepingAmounts(prompt, ChapterReflection.PlanPrompt2, width);

            planCards.Clear();
            planRings.Clear();
            planChecks.Clear();
            float y = 88f + 120f;
            foreach (PaydayPlan plan in PaydayPlans.All)
            {
                string id = plan.id;
                Image ring = UiKit.Panel(screen, "Ring " + id, UiTheme.AccentPrimary, UiTheme.RadiusCard, false);
                ring.raycastTarget = false;
                SetTopLeft(ring.rectTransform, 0f, y, width, 144f);
                ring.enabled = false;
                Image cardImage = UiKit.Panel(screen, "Plan " + id, UiTheme.Card, UiTheme.RadiusCard - 6f, false);
                cardImage.raycastTarget = true;
                SetTopLeft(cardImage.rectTransform, 6f, y + 6f, width - 12f, 132f);
                Text text = UiKit.Label(cardImage.rectTransform, "Text", "", UiTheme.Body, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
                // The right 96 u hold the selected checkmark, so the text wraps short of it.
                text.rectTransform.offsetMin = new Vector2(30f, 0f);
                text.rectTransform.offsetMax = new Vector2(-96f, 0f);
                UiTextLayout.WrapKeepingAmounts(text, plan.text, width - 138f);
                Image check = UiKit.IconImage(cardImage.rectTransform, "Check", "checkmark", 48f, UiTheme.AccentPrimary);
                RectTransform checkRect = check.rectTransform;
                checkRect.anchorMin = checkRect.anchorMax = checkRect.pivot = new Vector2(1f, 0.5f);
                checkRect.anchoredPosition = new Vector2(-30f, 0f);
                check.raycastTarget = false;
                check.enabled = false;
                AddButton(cardImage.gameObject, cardImage, () => Pick(id));
                planCards.Add(cardImage);
                planRings.Add(ring);
                planChecks.Add(check);
                y += 144f + 20f;
            }

            saveButton = UiKit.PrimaryButton(screen, "Save my plan", SavePlan, 400f);
            Place((RectTransform)saveButton.transform, new Vector2(1f, 1f), new Vector2(width - 400f * 0.5f, -(y + 10f + 72f)));
            saveButton.interactable = false;
            Button notNow = UiKit.TextButton(screen, "Not now", NotNow, 280f);
            Place((RectTransform)notNow.transform, new Vector2(1f, 1f), new Vector2(width - 400f - 20f - 140f, -(y + 10f + 72f)));

            if (!string.IsNullOrEmpty(pickedPlan))
            {
                Pick(pickedPlan); // a layout rebuild keeps the pick
            }
        }

        void Pick(string id)
        {
            pickedPlan = id;
            for (int i = 0; i < PaydayPlans.All.Length && i < planCards.Count; i++)
            {
                bool selected = PaydayPlans.All[i].id == id;
                planCards[i].color = selected ? UiTheme.Tint : UiTheme.Card;
                planRings[i].enabled = selected;
                if (i < planChecks.Count)
                {
                    planChecks[i].enabled = selected && planChecks[i].sprite != null;
                }
            }

            if (saveButton != null)
            {
                saveButton.interactable = true;
            }
        }

        void SavePlan()
        {
            if (string.IsNullOrEmpty(pickedPlan))
            {
                return;
            }

            PaydayPlan plan = PaydayPlans.Get(pickedPlan);
            PlayerDataManager.Instance?.UpdatePlayerData(d =>
            {
                d.paydayPlanId = plan.id;
                d.paydayPlanText = plan.text;
            }, saveImmediately: true);
            data = PlayerDataAccess.GetCurrentPlayer() ?? data;
            planSaved = true;
            ShowScreen(3);
        }

        void NotNow()
        {
            planSaved = false;
            ShowScreen(3);
        }

        // ================================================================ screen D: close

        void BuildClose()
        {
            float width = right.rect.width > 0f ? right.rect.width : 1187f;
            Image box = UiKit.Panel(screen, "MaliBox", UiTheme.Warm, UiTheme.RadiusCard, true);
            box.raycastTarget = false;
            SetTopLeft(box.rectTransform, 0f, 200f, width, 260f);
            Image tag = UiKit.Panel(screen, "NameTag", UiTheme.Gold, 30f, false);
            tag.raycastTarget = false;
            SetTopLeft(tag.rectTransform, 40f, 170f, 140f, 60f);
            Text tagText = UiKit.Label(tag.rectTransform, "Name", "Mali", UiTheme.Label, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            tagText.horizontalOverflow = HorizontalWrapMode.Overflow;

            Text line = Add("", UiTheme.Dialogue, UiTheme.TextPrimary, 48f, 250f, width - 96f, 190f);
            UiTextLayout.WrapKeepingAmounts(line, ChapterReflection.CloseLine(data, planSaved), width - 96f);

            Text caption = Add("", UiTheme.Label, UiTheme.TextSecondary, 0f, 500f, width, 100f);
            UiTextLayout.WrapKeepingAmounts(caption, ChapterReflection.CloseCaption, width);

            Button live = UiKit.PrimaryButton(screen, ChapterReflection.LiveAgain, LiveAgain, LiveWidth);
            Place((RectTransform)live.transform, new Vector2(1f, 1f), new Vector2(width - LiveWidth * 0.5f, -(640f + 72f)));
        }

        void LiveAgain()
        {
            Action live = onLiveAgain;
            onLiveAgain = null;
            Hide();
            live?.Invoke();
        }

        // ================================================================ helpers

        Text Add(string text, UiTheme.TextRole role, Color color, float x, float y, float width, float height)
        {
            Text t = UiKit.Label(screen, "Text", text, role, color);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(t.rectTransform, x, y, width, height);
            return t;
        }

        RectTransform NextButton(string label, Action onClick, float top, bool rowWithBills = true)
        {
            float width = right.rect.width > 0f ? right.rect.width : 1187f;
            Button b = UiKit.PrimaryButton(screen, label, onClick, NextWidth);
            var rect = (RectTransform)b.transform;
            Place(rect, new Vector2(0f, 1f), new Vector2(width - NextWidth * 0.5f, -(top + 72f)));
            return rect;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
        }

        static void AddButton(GameObject go, Graphic graphic, Action onClick)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                UiKit.NotifyButtonClicked();
                onClick?.Invoke();
            });
        }

        static void SetTopLeft(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -top);
        }

        // ================================================================ build and layout

        void Build()
        {
            canvas = UiCanvasFactory.Create("ChapterEnd_Canvas", UiTheme.Sort.ChapterEnd, transform, out safeRoot);
            Image backdrop = UiCanvasFactory.FullBleed(canvas, "Paper", UiTheme.Paper);
            backdrop.raycastTarget = true;

            left = UiKit.Rect(safeRoot, "Left");
            Image circle = UiKit.SpriteImage(left, "GoldCircle", UiKit.Circle, CircleSize, UiTheme.WithAlpha(UiTheme.Gold, 0.25f));
            circle.preserveAspect = true;
            circleRect = circle.rectTransform;
            Image wave = UiKit.SpriteImage(left, "MaliWave", UiKit.MaliWave, WaveHeight, Color.white);
            wave.preserveAspect = true;
            waveRect = wave.rectTransform;

            right = UiKit.Rect(safeRoot, "Right");
            screen = UiKit.Rect(right, "Screen");

            Layout(UiTheme.ReferenceWidth);
            canvas.gameObject.SetActive(false);
        }

        void Layout(float safeWidth)
        {
            layoutWidth = safeWidth;
            float inner = safeWidth - 2f * Margin;
            float leftWidth = inner * LeftShare;
            float gap = 27f;
            SetTopLeft(left, Margin, Margin, leftWidth, 1080f - 2f * Margin);
            SetTopLeft(right, Margin + leftWidth + gap, Margin, inner - leftWidth - gap, 1080f - 2f * Margin);
            float circle = Mathf.Min(CircleSize, leftWidth);
            if (circleRect != null)
            {
                circleRect.sizeDelta = new Vector2(circle, circle);
                waveRect.sizeDelta = new Vector2(circle, WaveHeight * circle / CircleSize);
            }
        }
    }
}
