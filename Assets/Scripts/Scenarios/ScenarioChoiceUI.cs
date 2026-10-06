using System;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// The scenario choice sheet (design spec 5.4.5, sort 50), built in code from the UI kit.
    ///
    /// Left column: place caption, title, situation, Mali's mini portrait with her intro line, and what the
    /// player has now. Right column: one card per choice in authored order, with value columns generated from
    /// the deltas (Cash | [Savings] | Energy | Later). XP, stress, the behaviour tag, choice descriptions and any
    /// "recommended" mark are never shown. A choice the player cannot afford is drawn at 60 % with
    /// "Not enough cash" / "Not enough savings" under its label and cannot be tapped. "Not now" and Android back
    /// close the sheet without choosing (the scenario stays active). It is a UiModal while open.
    /// </summary>
    public class ScenarioChoiceUI : MonoBehaviour
    {
        const float MaxSheetWidth = 2000f;
        const float SheetSideMargin = 120f;
        const float SheetHeight = 940f;
        const float Padding = UiTheme.SheetPadding;
        const float LeftShare = 0.36f;
        const float ColumnGap = 40f;
        const float NotNowWidth = 280f;
        const float NotNowHeight = 88f; // the tap area is padded to 144 by the kit
        const float HeaderHeight = 56f;
        const float CardHeight = 150f;
        const float CardGap = 20f;
        const float CardInset = 24f;
        const float LabelGap = 20f;
        const float MiniPortrait = 120f;
        const float RingWidth = 6f;
        const float DisabledAlpha = 0.6f;
        const float OpenLockout = 0.3f;

        Canvas canvas;
        RectTransform safeRoot;
        Image scrim;
        RectTransform sheet;
        CanvasGroup sheetGroup;
        RectTransform content;

        Action<ScenarioChoice> onChoiceSelected;
        Action onCancelled;
        bool open;
        bool closing;
        bool modalPushed;

        /// <summary>True while the sheet is shown.</summary>
        public bool IsOpen => open;

        void Awake()
        {
            BuildUiIfNeeded();
            HideImmediate();
        }

        void OnDisable()
        {
            PopModal();
        }

        void OnDestroy()
        {
            PopModal();
            if (sheet != null)
            {
                UiTween.Stop(sheet);
            }

            if (sheetGroup != null)
            {
                UiTween.Stop(sheetGroup);
            }
        }

        // ================================================================ public API

        /// <summary>Kept signature: shows the sheet; <paramref name="onSelected"/> runs with the tapped choice.</summary>
        public void Show(ScenarioDefinition scenario, Action<ScenarioChoice> onSelected)
        {
            Show(scenario, onSelected, null);
        }

        /// <summary>Shows the sheet. <paramref name="onSelected"/> runs with the tapped choice;
        /// <paramref name="onClosedWithoutChoice"/> runs after "Not now" or Android back.</summary>
        public void Show(ScenarioDefinition scenario, Action<ScenarioChoice> onSelected, Action onClosedWithoutChoice)
        {
            if (scenario == null)
            {
                return;
            }

            BuildUiIfNeeded();
            onChoiceSelected = onSelected;
            onCancelled = onClosedWithoutChoice;
            closing = false;
            open = true;

            scrim.gameObject.SetActive(true);
            sheet.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Populate(scenario, PlayerDataAccess.GetCurrentPlayer());

            // SheetIn: rise 40 u and fade in; input locked for the first 300 ms.
            UiTween.Stop(sheet);
            UiTween.Stop(sheetGroup);
            sheetGroup.alpha = 0f;
            sheetGroup.interactable = false;
            sheet.anchoredPosition = new Vector2(0f, -UiTheme.Motion.SheetRise);
            UiTween.Move(sheet, Vector2.zero, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
            UiTween.Fade(sheetGroup, 1f, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
            UiTween.Delay(this, OpenLockout, () =>
            {
                if (open && !closing && sheetGroup != null)
                {
                    sheetGroup.interactable = true;
                }
            });

            if (!modalPushed)
            {
                UiModal.Push(this, NotNow);
                modalPushed = true;
            }
        }

        /// <summary>Closes the sheet without running any callback.</summary>
        public void Hide()
        {
            open = false;
            closing = false;
            onChoiceSelected = null;
            onCancelled = null;
            if (scrim != null)
            {
                scrim.gameObject.SetActive(false);
            }

            if (sheet != null)
            {
                UiTween.Stop(sheet);
                sheet.gameObject.SetActive(false);
            }

            PopModal();
        }

        public void HideImmediate()
        {
            Hide();
        }

        // ================================================================ choosing

        void Choose(ScenarioChoice choice)
        {
            if (!open || closing || !sheetGroup.interactable)
            {
                return;
            }

            Action<ScenarioChoice> selected = onChoiceSelected;
            CloseAnimated();
            selected?.Invoke(choice);
        }

        void NotNow()
        {
            if (!open || closing)
            {
                return;
            }

            Action cancelled = onCancelled;
            CloseAnimated();
            cancelled?.Invoke();
        }

        /// <summary>SheetOut (200 ms), then hidden. Input is off and the modal popped at once.</summary>
        void CloseAnimated()
        {
            closing = true;
            onChoiceSelected = null;
            onCancelled = null;
            sheetGroup.interactable = false;
            PopModal();
            UiTween.Move(sheet, new Vector2(0f, -UiTheme.Motion.SheetRise), UiTheme.Motion.SheetOut, UiTween.Ease.Accelerate);
            UiTween.Fade(sheetGroup, 0f, UiTheme.Motion.SheetOut, UiTween.Ease.Accelerate, () =>
            {
                if (closing)
                {
                    Hide();
                }
            });
        }

        void PopModal()
        {
            if (modalPushed)
            {
                modalPushed = false;
                UiModal.Pop(this);
            }
        }

        // ================================================================ building

        void BuildUiIfNeeded()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = UiCanvasFactory.Create("ScenarioChoice_Canvas", UiTheme.Sort.Sheets, transform, out safeRoot);
            scrim = UiCanvasFactory.FullBleed(canvas, "Scrim", UiTheme.Scrim);
            scrim.raycastTarget = true; // the world behind is not tappable while the sheet is up

            sheet = UiKit.Rect(safeRoot, "Sheet");
            sheet.anchorMin = sheet.anchorMax = new Vector2(0.5f, 0.5f);
            sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = new Vector2(MaxSheetWidth, SheetHeight);
            sheetGroup = sheet.gameObject.AddComponent<CanvasGroup>();

            Image background = UiKit.Panel(sheet, "Background", UiTheme.Paper, UiTheme.RadiusSheet, true);
            background.raycastTarget = true;
        }

        float SafeWidth()
        {
            float width = safeRoot != null ? safeRoot.rect.width : 0f;
            return width > 1f ? width : UiTheme.ReferenceWidth;
        }

        void Populate(ScenarioDefinition scenario, PlayerData data)
        {
            if (content != null)
            {
                Destroy(content.gameObject);
            }

            float sheetWidth = Mathf.Min(MaxSheetWidth, SafeWidth() - SheetSideMargin);
            sheet.sizeDelta = new Vector2(sheetWidth, SheetHeight);

            content = UiKit.Rect(sheet, "Content");
            content.offsetMin = new Vector2(Padding, Padding);
            content.offsetMax = new Vector2(-Padding, -Padding);

            float innerWidth = sheetWidth - Padding * 2f;
            float leftWidth = Mathf.Round(innerWidth * LeftShare);
            float rightWidth = innerWidth - leftWidth - ColumnGap;

            BuildLeft(scenario, data, leftWidth);
            BuildRight(scenario, data, leftWidth + ColumnGap, rightWidth);
        }

        void BuildLeft(ScenarioDefinition scenario, PlayerData data, float width)
        {
            RectTransform left = Column(content, "Left", 0f, width);
            float y = 0f;

            UiTheme.TextRole caption = UiTheme.Caption;
            Text place = TextAt(left, "Place", scenario.placeLabel, caption, UiTheme.TextMuted, 0f, y, width);
            y += LineHeight(caption) * Mathf.Max(1, UiTextLayout.CountLines(place, place.text, width)) + UiTheme.Space10;

            UiTheme.TextRole title = UiTheme.Title;
            Text titleText = TextAt(left, "Title", MaliText.Fill(scenario.title, data), title, UiTheme.TextPrimary, 0f, y, width);
            y += LineHeight(title) * UiTextLayout.CountLines(titleText, titleText.text, width) + UiTheme.Space20;

            UiTheme.TextRole body = UiTheme.Body;
            Text situation = TextAt(left, "Situation", MaliText.Fill(scenario.description, data), body, UiTheme.TextPrimary, 0f, y, width);
            y += LineHeight(body) * UiTextLayout.CountLines(situation, situation.text, width) + UiTheme.Space30;

            if (!string.IsNullOrWhiteSpace(scenario.introDialogue))
            {
                Image portrait = UiKit.SpriteImage(left, "MaliMini", UiKit.MaliPortrait, MiniPortrait, Color.white);
                RectTransform portraitRect = portrait.rectTransform;
                portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 1f);
                portraitRect.pivot = new Vector2(0f, 1f);
                portraitRect.anchoredPosition = new Vector2(0f, -y);

                float introX = MiniPortrait + UiTheme.Space20;
                UiTheme.TextRole introRole = UiTheme.Label.WithWeight(UiFontWeight.SemiBold);
                TextAt(left, "Intro", MaliText.Fill(scenario.introDialogue, data), introRole, UiTheme.TextSecondary, introX, y,
                       width - introX);
            }

            FinancialStats stats = data?.financialStats;
            string cash = stats != null ? MoneyFormat.Digits(stats.cash) : "0";
            string savings = stats != null ? MoneyFormat.Digits(stats.savings) : "0";
            string energy = stats != null ? Mathf.RoundToInt(stats.energy).ToString(System.Globalization.CultureInfo.InvariantCulture) : "0";

            UiTheme.TextRole small = UiTheme.Caption;
            Text status = TextFromBottom(left, "Status", "Savings R" + savings + " · Energy " + energy, small, UiTheme.TextMuted, 0f, width);
            float statusHeight = LineHeight(small);
            UiTheme.TextRole bold = UiTheme.Body.WithWeight(UiFontWeight.Bold);
            Text have = TextFromBottom(left, "Cash", "You have R" + cash + " cash", bold, UiTheme.TextPrimary, statusHeight + 4f, width);
            have.horizontalOverflow = HorizontalWrapMode.Overflow;
            status.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        void BuildRight(ScenarioDefinition scenario, PlayerData data, float x, float width)
        {
            RectTransform right = Column(content, "Right", x, width);

            Button notNow = UiKit.SecondaryButton(right, "Not now", NotNow, NotNowWidth, NotNowHeight);
            var notNowRect = (RectTransform)notNow.transform;
            notNowRect.anchorMin = notNowRect.anchorMax = new Vector2(1f, 1f);
            notNowRect.pivot = new Vector2(1f, 1f);
            notNowRect.anchoredPosition = Vector2.zero;

            bool savingsColumn = ScenarioOutcome.HasSavingsColumn(scenario);
            float[] widths = savingsColumn ? new[] { 150f, 150f, 120f, 190f } : new[] { 160f, 130f, 200f };
            string[] titles = savingsColumn ? new[] { "Cash", "Savings", "Energy", "Later" } : new[] { "Cash", "Energy", "Later" };
            float columnsWidth = 0f;
            foreach (float w in widths)
            {
                columnsWidth += w;
            }

            float labelBox = width - CardInset - columnsWidth - CardInset - LabelGap;

            // Header row: column titles right-aligned in their columns.
            float headerTop = NotNowHeight;
            RectTransform header = Row(right, "Header", headerTop, HeaderHeight);
            float columnRight = CardInset;
            for (int i = widths.Length - 1; i >= 0; i--)
            {
                Text titleText = UiKit.Label(header, "Title " + titles[i], titles[i], UiTheme.Caption, UiTheme.TextMuted, TextAnchor.LowerRight);
                PlaceColumn(titleText.rectTransform, columnRight, widths[i]);
                titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
                columnRight += widths[i];
            }

            float y = headerTop + HeaderHeight;
            ScenarioChoice[] choices = scenario.choices ?? Array.Empty<ScenarioChoice>();
            bool loan = scenario.moneyCategory == MoneyCategory.Loan;
            foreach (ScenarioChoice choice in choices)
            {
                if (choice == null)
                {
                    continue;
                }

                BuildCard(right, data, choice, y, labelBox, widths, savingsColumn, loan);
                y += CardHeight + CardGap;
            }
        }

        void BuildCard(RectTransform parent, PlayerData data, ScenarioChoice choice, float y, float labelBox, float[] widths,
                       bool savingsColumn, bool loan)
        {
            RectTransform card = Row(parent, "Choice " + choice.choiceId, y, CardHeight);

            // Pressed ring (6 u Coin), shown while held.
            Image ring = UiKit.Panel(card, "Ring", UiTheme.Coin, UiTheme.RadiusCard + RingWidth, false);
            ring.rectTransform.offsetMin = new Vector2(-RingWidth, -RingWidth);
            ring.rectTransform.offsetMax = new Vector2(RingWidth, RingWidth);
            ring.raycastTarget = false;
            ring.gameObject.SetActive(false);

            Image background = UiKit.Panel(card, "Card", UiTheme.Card, UiTheme.RadiusCard, true);
            background.raycastTarget = true;

            ChoiceAvailability availability = ScenarioOutcome.Availability(data, choice);
            bool enabled = availability == ChoiceAvailability.Available;

            RectTransform faded = UiKit.Rect(card, "Values");
            var fadedGroup = faded.gameObject.AddComponent<CanvasGroup>();
            fadedGroup.alpha = enabled ? 1f : DisabledAlpha;
            fadedGroup.blocksRaycasts = false;
            fadedGroup.interactable = false;

            // Label (and the disabled line under it), centred vertically as one block.
            UiTheme.TextRole labelRole = UiTheme.ChoiceLabel;
            Text label = UiKit.Label(faded, "Label", "", labelRole, UiTheme.TextPrimary);
            UiTextLayout.WrapKeepingAmounts(label, MaliText.Fill(choice.label, data), labelBox);
            int lines = Mathf.Max(1, UiTextLayout.CountLines(label, label.text, labelBox));
            float labelHeight = LineHeight(labelRole) * lines;
            UiTheme.TextRole noteRole = UiTheme.Label;
            float blockHeight = labelHeight + (enabled ? 0f : LineHeight(noteRole));
            float top = Mathf.Max(0f, (CardHeight - blockHeight) * 0.5f);
            PlaceTopLeft(label.rectTransform, CardInset, top, labelBox, labelHeight);

            if (!enabled)
            {
                string note = availability == ChoiceAvailability.NotEnoughSavings ? "Not enough savings" : "Not enough cash";
                Text noteText = UiKit.Label(card, "Disabled", note, noteRole, UiTheme.TextSecondary);
                noteText.horizontalOverflow = HorizontalWrapMode.Overflow;
                PlaceTopLeft(noteText.rectTransform, CardInset, top + labelHeight, labelBox, LineHeight(noteRole));
            }

            // Value columns, right to left: Later, Energy, [Savings], Cash.
            bool transfer = ScenarioOutcome.IsTransfer(choice);
            float columnRight = CardInset;
            int index = widths.Length - 1;

            string later = ScenarioOutcome.LaterColumn(data, choice, out string laterSmall);
            BuildLaterValue(faded, later, laterSmall, columnRight, widths[index]);
            columnRight += widths[index--];

            BuildValue(faded, "Energy", ScenarioOutcome.EnergyColumn(choice.energyDelta), UiTheme.TextPrimary, columnRight, widths[index]);
            columnRight += widths[index--];

            if (savingsColumn)
            {
                BuildValue(faded, "Savings", ScenarioOutcome.MoneyColumn(choice.savingsDelta),
                           MoneyColour(choice.savingsDelta, transfer, loan), columnRight, widths[index]);
                columnRight += widths[index--];
            }

            BuildValue(faded, "Cash", ScenarioOutcome.MoneyColumn(choice.cashDelta), MoneyColour(choice.cashDelta, transfer, loan),
                       columnRight, widths[index]);

            if (!enabled)
            {
                return;
            }

            var press = background.gameObject.AddComponent<CardPress>();
            press.Ring = ring;
            press.Target = card;
            press.Clicked = () => Choose(choice);
        }

        static Color MoneyColour(float delta, bool transfer, bool loan)
        {
            if (Mathf.Abs(delta) < MoneyRecorder.Tolerance)
            {
                return UiTheme.TextPrimary;
            }

            if (transfer || loan)
            {
                return UiTheme.MoneyTransfer;
            }

            return delta < 0f ? UiTheme.MoneyOut : UiTheme.MoneyIn;
        }

        static void BuildValue(RectTransform card, string name, string value, Color colour, float rightInset, float width)
        {
            Text text = UiKit.Label(card, name, value, UiTheme.HudValue, colour, TextAnchor.MiddleRight);
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            PlaceColumn(text.rectTransform, rightInset, width);
        }

        static void BuildLaterValue(RectTransform card, string value, string small, float rightInset, float width)
        {
            if (string.IsNullOrEmpty(small))
            {
                BuildValue(card, "Later", value, UiTheme.TextPrimary, rightInset, width);
                return;
            }

            UiTheme.TextRole first = UiTheme.Label;
            UiTheme.TextRole second = UiTheme.Caption;
            float block = LineHeight(first) + LineHeight(second);
            float top = (CardHeight - block) * 0.5f;

            Text line1 = UiKit.Label(card, "Later", value, first, UiTheme.TextPrimary, TextAnchor.UpperRight);
            line1.horizontalOverflow = HorizontalWrapMode.Overflow;
            PlaceColumnBand(line1.rectTransform, rightInset, width, top, LineHeight(first));

            Text line2 = UiKit.Label(card, "LaterDays", small, second, UiTheme.TextPrimary, TextAnchor.UpperRight);
            line2.horizontalOverflow = HorizontalWrapMode.Overflow;
            PlaceColumnBand(line2.rectTransform, rightInset, width, top + LineHeight(first), LineHeight(second));
        }

        // ================================================================ layout helpers

        static float LineHeight(UiTheme.TextRole role) => role.Size * role.LineSpacing;

        static RectTransform Column(RectTransform parent, string name, float x, float width)
        {
            RectTransform column = UiKit.Rect(parent, name);
            column.anchorMin = new Vector2(0f, 0f);
            column.anchorMax = new Vector2(0f, 1f);
            column.pivot = new Vector2(0f, 1f);
            column.offsetMin = new Vector2(x, 0f);
            column.offsetMax = new Vector2(x + width, 0f);
            return column;
        }

        /// <summary>A full-width row <paramref name="height"/> tall, <paramref name="top"/> u below the parent's top.</summary>
        static RectTransform Row(RectTransform parent, string name, float top, float height)
        {
            RectTransform row = UiKit.Rect(parent, name);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(1f, 1f);
            row.pivot = new Vector2(0.5f, 1f);
            row.offsetMin = new Vector2(0f, -top - height);
            row.offsetMax = new Vector2(0f, -top);
            return row;
        }

        static void PlaceTopLeft(RectTransform rect, float x, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>A column <paramref name="width"/> wide whose right edge is <paramref name="rightInset"/> u from the
        /// parent's right edge, full height.</summary>
        static void PlaceColumn(RectTransform rect, float rightInset, float width)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-rightInset - width, 0f);
            rect.offsetMax = new Vector2(-rightInset, 0f);
        }

        static void PlaceColumnBand(RectTransform rect, float rightInset, float width, float top, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-rightInset, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static Text TextAt(RectTransform parent, string name, string value, UiTheme.TextRole role, Color colour, float x, float top,
                           float width)
        {
            Text text = UiKit.Label(parent, name, "", role, colour);
            UiTextLayout.WrapKeepingAmounts(text, value ?? "", width);
            int lines = Mathf.Max(1, UiTextLayout.CountLines(text, text.text, width));
            PlaceTopLeft(text.rectTransform, x, top, width, LineHeight(role) * lines);
            return text;
        }

        static Text TextFromBottom(RectTransform parent, string name, string value, UiTheme.TextRole role, Color colour, float bottom,
                                   float width)
        {
            Text text = UiKit.Label(parent, name, value, role, colour, TextAnchor.LowerLeft);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottom);
            rect.sizeDelta = new Vector2(width, LineHeight(role));
            return text;
        }

        /// <summary>Press feedback for a choice card: scale 0.97 and a Coin ring while held; a click chooses.</summary>
        sealed class CardPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
        {
            public Image Ring;
            public RectTransform Target;
            public Action Clicked;

            public void OnPointerDown(PointerEventData eventData)
            {
                SetPressed(true);
            }

            public void OnPointerUp(PointerEventData eventData)
            {
                SetPressed(false);
            }

            public void OnPointerExit(PointerEventData eventData)
            {
                SetPressed(false);
            }

            public void OnPointerClick(PointerEventData eventData)
            {
                SetPressed(false);
                UiKit.NotifyButtonClicked();
                Clicked?.Invoke();
            }

            void OnDisable()
            {
                if (Target != null)
                {
                    UiTween.Stop(Target);
                    Target.localScale = Vector3.one;
                }

                if (Ring != null)
                {
                    Ring.gameObject.SetActive(false);
                }
            }

            void SetPressed(bool pressed)
            {
                if (Ring != null)
                {
                    Ring.gameObject.SetActive(pressed);
                }

                if (Target != null)
                {
                    UiTween.Scale(Target, pressed ? UiTheme.Motion.PressScale : 1f, UiTheme.Motion.Press, UiTween.Ease.EaseOut);
                }
            }
        }
    }
}
