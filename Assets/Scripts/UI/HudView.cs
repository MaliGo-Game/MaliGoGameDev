using MaliGo.Copy;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// The HUD (DESIGN_SPEC §5.4.1, §7.4; sort 20): Day pill with the Today pill under it, Cash, Savings (with goal
    /// progress), Energy, the bill pill and Pause. Every string comes from <see cref="HudCopy"/>. Values count over
    /// <c>Count</c>; Cash and Savings show a delta tag per money event. If the top row does not fit the safe width,
    /// the right group (Energy, Bill, Pause) moves to the second row; nothing is ever scaled down. Never shows a
    /// name, level, XP, stress or score. Not a modal.
    /// Anchors: hud.cash, hud.savings, hud.today, hud.energy, hud.bill, hud.pause.
    /// </summary>
    public class HudView : MonoBehaviour
    {
        const float Margin = 24f;
        const float PillHeight = 112f;
        const float Gap = 16f;
        const float RowTwoTop = 152f;
        const float DayWidth = 328f;
        const float CashWidth = 280f;
        const float SavingsWidth = 392f;
        const float EnergyWidth = 300f;
        const float BillWidth = 340f;
        const float PauseSize = 144f;
        const float PauseTop = 16f;
        const float Pad = 24f;
        const float IconSize = 56f;
        const float IconTextX = 96f;
        const float TagGap = 8f;
        const float NaturalWidth = DayWidth + CashWidth + SavingsWidth + EnergyWidth + BillWidth + PauseSize + 5f * Gap + 2f * Margin;

        static readonly UiTheme.TextRole DayRole = new UiTheme.TextRole(40, UiFontWeight.Bold, 1.0f);

        static HudView instance;

        Canvas canvas;
        RectTransform safeRoot;
        RectTransform dayPill, todayPill, cashPill, savingsPill, energyPill, billPill, pauseButton;
        Text dayLine1, dayLine2, todayLine1, todayValue, cashValue, savingsGoal, savingsValue, energyValue, billLine1, billLine2;
        Text cashTag, savingsTag;
        Image savingsBar, energyBar, billDot;

        PlayerDataManager subscribed;
        bool shown;
        float shownCash, shownSavings, shownNow, shownEnergy;
        float lastSafeWidth = -1f;
        bool wrapped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>Creates the HUD once per scene.</summary>
        public static HudView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            HudView existing = FindFirstObjectByType<HudView>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return new GameObject("HudView").AddComponent<HudView>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            Build();
            GameEvents.MoneyChanged += HandleMoneyChanged;
        }

        void Start()
        {
            TrySubscribe();
            Refresh(false);
        }

        void OnDestroy()
        {
            GameEvents.MoneyChanged -= HandleMoneyChanged;
            if (subscribed != null)
            {
                subscribed.OnPlayerDataChanged -= HandlePlayerDataChanged;
                subscribed = null;
            }

            UiAnchors.Unregister("hud.cash", cashPill);
            UiAnchors.Unregister("hud.savings", savingsPill);
            UiAnchors.Unregister("hud.today", todayPill);
            UiAnchors.Unregister("hud.energy", energyPill);
            UiAnchors.Unregister("hud.bill", billPill);
            UiAnchors.Unregister("hud.pause", pauseButton);
            if (instance == this)
            {
                instance = null;
            }

            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
        }

        void Update()
        {
            if (subscribed == null || subscribed != PlayerDataManager.Instance)
            {
                TrySubscribe();
            }

            float width = safeRoot != null ? safeRoot.rect.width : 0f;
            if (width > 0f && !Mathf.Approximately(width, lastSafeWidth))
            {
                lastSafeWidth = width;
                Layout(width);
            }
        }

        void TrySubscribe()
        {
            PlayerDataManager manager = PlayerDataManager.Instance;
            if (manager == subscribed)
            {
                return;
            }

            if (subscribed != null)
            {
                subscribed.OnPlayerDataChanged -= HandlePlayerDataChanged;
            }

            subscribed = manager;
            if (subscribed != null)
            {
                subscribed.OnPlayerDataChanged += HandlePlayerDataChanged;
                Refresh(shown);
            }
        }

        void HandlePlayerDataChanged(PlayerData data)
        {
            Refresh(true);
        }

        // ================================================================ values (§7.4)

        void Refresh(bool animate)
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data == null || canvas == null)
            {
                return;
            }

            FinancialStats stats = data.financialStats ?? new FinancialStats();

            var day = HudCopy.DayPill(data);
            dayLine1.text = day.line1;
            dayLine2.text = day.line2;

            var today = HudCopy.TodayPill(data);
            todayLine1.text = today.line1;

            FinancialGoal goal = data.GetPrimaryGoal();
            savingsGoal.text = "of " + MoneyFormat.Rand(goal.targetAmount);
            savingsBar.fillAmount = goal.Progress(stats.savings);
            energyBar.fillAmount = Mathf.Clamp01(stats.energy / ChapterConfig.DailyEnergy);

            float now = HudCopy.TodayNow(data);
            bool count = animate && shown;
            SetMoney(cashValue, ref shownCash, stats.cash, count);
            SetMoney(savingsValue, ref shownSavings, stats.savings, count);
            SetMoney(todayValue, ref shownNow, now, count);
            SetNumber(energyValue, ref shownEnergy, stats.energy, count);
            shown = true;

            var bill = HudCopy.BillPill(data);
            billLine1.text = bill.line1;
            billLine2.text = bill.line2;
            billLine2.color = bill.attention ? UiTheme.TextOnInverse : UiTheme.TextOnInverseMuted;
            billDot.gameObject.SetActive(bill.attention);
            if (bill.attention)
            {
                float textWidth = UiTextLayout.MeasureWidth(billLine1, bill.line1);
                ((RectTransform)billDot.transform).anchoredPosition = new Vector2(Pad + textWidth + 8f, -38f);
            }
        }

        static void SetMoney(Text text, ref float shownValue, float value, bool count)
        {
            if (count && !Mathf.Approximately(shownValue, value))
            {
                UiTween.CountUp(text, shownValue, value, v => MoneyFormat.Rand(v));
            }
            else
            {
                UiTween.Stop(text);
                text.text = MoneyFormat.Rand(value);
            }

            shownValue = value;
        }

        static void SetNumber(Text text, ref float shownValue, float value, bool count)
        {
            if (count && !Mathf.Approximately(shownValue, value))
            {
                UiTween.CountUp(text, shownValue, value, v => Mathf.RoundToInt(v).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                UiTween.Stop(text);
                text.text = Mathf.RoundToInt(value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            shownValue = value;
        }

        // ================================================================ delta tags

        void HandleMoneyChanged(MoneyEvent e)
        {
            if (e == null || canvas == null)
            {
                return;
            }

            if (Mathf.Abs(e.cashDelta) > 0.005f)
            {
                ShowTag(cashTag, e.cashDelta, e);
            }

            if (Mathf.Abs(e.savingsDelta) > 0.005f)
            {
                ShowTag(savingsTag, e.savingsDelta, e);
            }
        }

        void ShowTag(Text tag, float delta, MoneyEvent e)
        {
            UiTween.Stop(tag);
            tag.text = MoneyFormat.Signed(delta);
            // Borrowed money is not income: a loan coming in is neutral like a transfer (as the choice sheet and
            // the reveal colour it), never the green of money earned.
            bool loan = e.category == MoneyCategory.Loan && e.kind == MoneyEventKind.In;
            Color color = e.kind == MoneyEventKind.Transfer || loan
                ? UiTheme.TextOnInverse
                : delta > 0f ? UiTheme.MoneyInOnInverse : UiTheme.MoneyOutOnInverse;
            color.a = 1f;
            tag.color = color;
            tag.gameObject.SetActive(true);
            UiTween.Delay(tag, UiTheme.Motion.DeltaTagHold, () => UiTween.Fade(tag, 0f, UiTheme.Motion.DeltaTagFade));
        }

        // ================================================================ layout

        void Layout(float safeWidth)
        {
            wrapped = safeWidth < NaturalWidth;
            Place(dayPill, Margin, Margin);
            Place(todayPill, Margin, RowTwoTop);
            Place(cashPill, Margin + DayWidth + Gap, Margin);
            Place(savingsPill, Margin + DayWidth + Gap + CashWidth + Gap, Margin);

            float rowTop = wrapped ? RowTwoTop : Margin;
            float right = safeWidth - Margin;
            PlaceRect(pauseButton, right - PauseSize, wrapped ? RowTwoTop - (PauseSize - PillHeight) * 0.5f : PauseTop);
            right -= PauseSize + Gap;
            Place(billPill, right - BillWidth, rowTop);
            right -= BillWidth + Gap;
            Place(energyPill, right - EnergyWidth, rowTop);

            float tagTop = wrapped ? RowTwoTop + PillHeight + TagGap : Margin + PillHeight + TagGap;
            PlaceRect((RectTransform)cashTag.transform, Margin + DayWidth + Gap, tagTop);
            PlaceRect((RectTransform)savingsTag.transform, Margin + DayWidth + Gap + CashWidth + Gap + IconTextX, tagTop);
        }

        static void Place(RectTransform rect, float x, float top) => PlaceRect(rect, x, top);

        static void PlaceRect(RectTransform rect, float x, float top)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
        }

        // ================================================================ build

        void Build()
        {
            canvas = UiCanvasFactory.Create("Hud_Canvas", UiTheme.Sort.Hud, transform, out safeRoot);

            dayPill = Pill("DayPill", DayWidth);
            dayLine1 = Line(dayPill, "Line1", DayRole, UiTheme.TextOnInverse, Pad, 14f, DayWidth - 2f * Pad, 48f);
            dayLine2 = Line(dayPill, "Line2", UiTheme.Caption, UiTheme.TextOnInverseMuted, Pad, 62f, DayWidth - 2f * Pad, 40f);

            todayPill = Pill("TodayPill", DayWidth);
            todayLine1 = Line(todayPill, "Line1", UiTheme.Caption, UiTheme.TextOnInverseMuted, Pad, 12f, DayWidth - 2f * Pad, 40f);
            Image arrow = UiKit.IconImage(todayPill, "Arrow", "arrowRight", 36f, UiTheme.TextOnInverse);
            PlaceCentre((RectTransform)arrow.transform, Pad + 18f, 78f);
            todayValue = Line(todayPill, "Now", UiTheme.HudValue, UiTheme.TextOnInverse, Pad + 36f + 10f, 56f, 200f, 48f);

            cashPill = Pill("CashPill", CashWidth);
            Image coin = UiKit.IconImage(cashPill, "Coin", "coin", IconSize, UiTheme.Coin);
            PlaceCentre((RectTransform)coin.transform, Pad + IconSize * 0.5f, PillHeight * 0.5f);
            Line(cashPill, "Label", UiTheme.Caption, UiTheme.TextOnInverseMuted, IconTextX, 12f, CashWidth - IconTextX - Pad, 40f).text = "Cash";
            cashValue = Line(cashPill, "Value", UiTheme.HudValue, UiTheme.TextOnInverse, IconTextX, 56f, CashWidth - IconTextX - Pad + 20f, 48f);

            savingsPill = Pill("SavingsPill", SavingsWidth);
            Image pouch = UiKit.IconImage(savingsPill, "Pouch", "pouch", IconSize, UiTheme.Coin);
            PlaceCentre((RectTransform)pouch.transform, Pad + IconSize * 0.5f, PillHeight * 0.5f);
            float savingsText = SavingsWidth - IconTextX - Pad;
            Line(savingsPill, "Label", UiTheme.Caption, UiTheme.TextOnInverseMuted, IconTextX, 12f, savingsText, 40f).text = "Savings";
            savingsGoal = Line(savingsPill, "Goal", UiTheme.Caption, UiTheme.TextOnInverseMuted, IconTextX, 12f, savingsText, 40f);
            savingsGoal.alignment = TextAnchor.UpperRight;
            savingsValue = Line(savingsPill, "Value", UiTheme.HudValue, UiTheme.TextOnInverse, IconTextX, 56f, 160f, 48f);
            savingsBar = Bar(savingsPill, IconTextX + 154f + 16f, 74f, 102f, 12f);

            energyPill = Pill("EnergyPill", EnergyWidth);
            Image bolt = UiKit.SpriteImage(energyPill, "Bolt", UiKit.EnergyBolt, IconSize, UiTheme.Coin);
            PlaceCentre((RectTransform)bolt.transform, Pad + IconSize * 0.5f, PillHeight * 0.5f);
            Line(energyPill, "Label", UiTheme.Caption, UiTheme.TextOnInverseMuted, IconTextX, 12f, EnergyWidth - IconTextX - Pad, 40f).text = "Energy";
            energyBar = Bar(energyPill, IconTextX, 73f, 80f, 14f);
            energyValue = Line(energyPill, "Value", UiTheme.HudValue, UiTheme.TextOnInverse, IconTextX + 80f + 12f, 56f, 100f, 48f);

            billPill = Pill("BillPill", BillWidth);
            billLine1 = Line(billPill, "Line1", UiTheme.Label, UiTheme.TextOnInverse, Pad, 14f, BillWidth - 2f * Pad, 46f);
            billLine2 = Line(billPill, "Line2", UiTheme.Caption, UiTheme.TextOnInverseMuted, Pad, 62f, BillWidth - 2f * Pad, 40f);
            billDot = UiKit.SpriteImage(billPill, "OwedDot", UiKit.Circle, 20f, UiTheme.AttentionDot);
            var dotRect = (RectTransform)billDot.transform;
            dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 1f);
            dotRect.pivot = new Vector2(0f, 0.5f);
            billDot.gameObject.SetActive(false);

            BuildPause();

            cashTag = Tag("CashTag");
            savingsTag = Tag("SavingsTag");

            UiAnchors.Register("hud.cash", cashPill);
            UiAnchors.Register("hud.savings", savingsPill);
            UiAnchors.Register("hud.today", todayPill);
            UiAnchors.Register("hud.energy", energyPill);
            UiAnchors.Register("hud.bill", billPill);
            UiAnchors.Register("hud.pause", pauseButton);

            Layout(UiTheme.ReferenceWidth);
        }

        RectTransform Pill(string name, float width)
        {
            Image image = UiKit.Panel(safeRoot, name, UiTheme.InverseHud, UiTheme.RadiusPill(PillHeight), false);
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, PillHeight);
            return rect;
        }

        static Text Line(RectTransform parent, string name, UiTheme.TextRole role, Color color, float x, float top, float width, float height)
        {
            Text text = UiKit.Label(parent, name, "", role, color, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -top);
            return text;
        }

        static Image Bar(RectTransform parent, float x, float top, float width, float height)
        {
            Image track = UiKit.Panel(parent, "BarTrack", UiTheme.WithAlpha(UiTheme.Paper, 0.25f), height * 0.5f, false);
            track.raycastTarget = false;
            RectTransform rect = track.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -top);

            RectTransform fillRect = UiKit.Rect(rect, "Fill");
            var fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = UiKit.White;
            fill.color = UiTheme.Coin;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            fill.raycastTarget = false;
            return fill;
        }

        static void PlaceCentre(RectTransform rect, float x, float yFromTop)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, -yFromTop);
        }

        void BuildPause()
        {
            var go = new GameObject("Pause", typeof(RectTransform));
            go.layer = safeRoot.gameObject.layer;
            pauseButton = (RectTransform)go.transform;
            pauseButton.SetParent(safeRoot, false);
            pauseButton.anchorMin = pauseButton.anchorMax = pauseButton.pivot = new Vector2(0f, 1f);
            pauseButton.sizeDelta = new Vector2(PauseSize, PauseSize);
            var background = go.AddComponent<Image>();
            background.sprite = UiKit.Circle;
            background.color = UiTheme.InverseHud;
            background.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(() =>
            {
                UiKit.NotifyButtonClicked();
                GameEvents.RaisePauseRequested();
            });
            UiKit.IconImage(pauseButton, "Icon", "pause", IconSize, UiTheme.TextOnInverse);
        }

        Text Tag(string name)
        {
            Text tag = UiKit.Label(safeRoot, name, "", UiTheme.HudValue.WithWeight(UiFontWeight.Bold).WithSize(36), UiTheme.TextOnInverse);
            tag.horizontalOverflow = HorizontalWrapMode.Overflow;
            RectTransform rect = tag.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(240f, 48f);
            var outline = tag.gameObject.AddComponent<Shadow>();
            outline.effectColor = UiTheme.WithAlpha(UiTheme.Shadow, 0.6f);
            outline.effectDistance = new Vector2(2f, -2f);
            tag.gameObject.SetActive(false);
            return tag;
        }
    }
}
