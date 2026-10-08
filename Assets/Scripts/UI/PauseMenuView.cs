using System;
using MaliGo.BankFeed;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using MaliGo.Settings;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// Pause (DESIGN_SPEC §5.4.10, §7.9, §4.8; sort 70) and its reset confirm (sort 75).
    ///
    /// Opens on <c>GameEvents.PauseRequested</c> (HUD pause button) or <c>UiModal.BackWithNoModal</c> (Android back
    /// with nothing open); back closes it. <c>Time.timeScale = 0</c> while open, restored on close, disable and
    /// destroy. 1500 x 760 sheet in two columns: left Sound / Music / Reduce motion toggles, right Text speed
    /// (Slow / Normal / Instant) and Start over... Every setting is written to <c>GameSettings</c> (PlayerPrefs) at
    /// once and applies live. Start over -> confirm -> <c>PlayerDataManager.DeleteSave()</c> -> timeScale 1 ->
    /// load CharacterCreation (PlayerPrefs untouched; the scene load clears the modal stack, §7.2).
    /// "Forget my bank data" (docs/BANK_FEED.md) shows only while a bank habit summary is saved: a row under Start
    /// over (the sheet grows by one row), which empties the summary at once (<c>PlayerDataManager.ForgetBankHabits</c>),
    /// says so in a notice and hides itself. The spending profile and the week are kept.
    /// Both panels are modals: they push only when opened (never in Awake/OnEnable) and pop on close,
    /// <c>OnDisable</c> and <c>OnDestroy</c>.
    /// </summary>
    public class PauseMenuView : MonoBehaviour
    {
        public const string CharacterCreationScene = "CharacterCreation";

        const float SheetWidth = 1500f;
        const float SheetHeight = 760f;
        const float Pad = UiTheme.SheetPadding;
        const float TopRow = 144f;
        const float ColumnGap = 84f;
        const float ColumnWidth = 660f;
        const float RowHeight = 144f;
        const float RowGap = 20f;
        const float ButtonWidth = 360f;
        const float SegmentWidth = 200f;
        const float TrackWidth = 120f;
        const float TrackHeight = 72f;
        const float ConfirmWidth = 1000f;
        const float ConfirmHeight = 600f;

        static PauseMenuView instance;

        Canvas canvas;
        CanvasGroup group;
        RectTransform sheet;
        Canvas confirmCanvas;
        CanvasGroup confirmGroup;
        RectTransform confirmSheet;
        readonly object confirmOwner = new object();

        SettingToggle soundToggle;
        SettingToggle musicToggle;
        SettingToggle motionToggle;
        readonly SegmentButton[] segments = new SegmentButton[3];
        Button forgetBankButton;

        bool open;
        bool confirmOpen;
        bool subscribed;

        public bool IsOpen => open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>The pause menu of this scene (created once per scene; not DontDestroyOnLoad).</summary>
        public static PauseMenuView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            PauseMenuView existing = FindFirstObjectByType<PauseMenuView>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return new GameObject("PauseMenu").AddComponent<PauseMenuView>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            BuildUi();
            Subscribe();
        }

        void OnDisable()
        {
            UiModal.Pop(confirmOwner);
            UiModal.Pop(this);
            if (open || confirmOpen)
            {
                Time.timeScale = 1f;
            }

            open = false;
            confirmOpen = false;
            if (canvas != null)
            {
                canvas.gameObject.SetActive(false);
            }

            if (confirmCanvas != null)
            {
                confirmCanvas.gameObject.SetActive(false);
            }
        }

        void OnDestroy()
        {
            Unsubscribe();
            UiModal.Pop(confirmOwner);
            UiModal.Pop(this);
            if (open || confirmOpen)
            {
                Time.timeScale = 1f;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;
            GameEvents.PauseRequested += Open;
            UiModal.BackWithNoModal += Open;
            GameSettings.Changed += Refresh;
        }

        void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
            GameEvents.PauseRequested -= Open;
            UiModal.BackWithNoModal -= Open;
            GameSettings.Changed -= Refresh;
        }

        // ================================================================ open / close

        /// <summary>Opens Pause (a no-op if it is already open).</summary>
        public void Open()
        {
            if (open || this == null || !isActiveAndEnabled)
            {
                return;
            }

            open = true;
            Time.timeScale = 0f;
            Refresh();
            canvas.gameObject.SetActive(true);
            group.interactable = true;
            group.blocksRaycasts = true;
            UiModal.Push(this, Close);

            UiTween.Stop(group);
            group.alpha = 0f;
            UiTween.Fade(group, 1f, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
            sheet.anchoredPosition = new Vector2(0f, -UiTheme.Motion.SheetRise);
            UiTween.Move(sheet, Vector2.zero, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
        }

        /// <summary>Resume: closes Pause (and its confirm) and restores <c>Time.timeScale = 1</c>.</summary>
        public void Close()
        {
            CloseConfirm();
            if (!open)
            {
                return;
            }

            open = false;
            UiModal.Pop(this);
            Time.timeScale = 1f;
            UiTween.Stop(group);
            UiTween.Stop(sheet);
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            canvas.gameObject.SetActive(false);
        }

        void OpenConfirm()
        {
            if (!open || confirmOpen)
            {
                return;
            }

            confirmOpen = true;
            confirmCanvas.gameObject.SetActive(true);
            UiModal.Push(confirmOwner, CloseConfirm);
            UiTween.Stop(confirmGroup);
            confirmGroup.alpha = 0f;
            UiTween.Fade(confirmGroup, 1f, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
            confirmSheet.anchoredPosition = new Vector2(0f, -UiTheme.Motion.SheetRise);
            UiTween.Move(confirmSheet, Vector2.zero, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
        }

        void CloseConfirm()
        {
            if (!confirmOpen)
            {
                return;
            }

            confirmOpen = false;
            UiModal.Pop(confirmOwner);
            UiTween.Stop(confirmGroup);
            UiTween.Stop(confirmSheet);
            confirmGroup.alpha = 0f;
            confirmCanvas.gameObject.SetActive(false);
        }

        void StartOver()
        {
            if (!confirmOpen)
            {
                return;
            }

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.DeleteSave();
            }

            confirmOpen = false;
            open = false;
            UiModal.Pop(confirmOwner);
            UiModal.Pop(this);
            Time.timeScale = 1f;
            SceneManager.LoadScene(CharacterCreationScene);
        }

        // ================================================================ settings

        void Refresh()
        {
            if (soundToggle == null)
            {
                return;
            }

            soundToggle.Set(GameSettings.SoundOn);
            musicToggle.Set(GameSettings.MusicOn);
            motionToggle.Set(GameSettings.ReduceMotion);
            int speed = GameSettings.TextSpeed;
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i].SetSelected(i == speed);
            }

            RefreshForgetBank();
        }

        // ================================================================ bank data

        /// <summary>The Forget row shows only while a summary is saved; the sheet grows by a row for it.</summary>
        void RefreshForgetBank()
        {
            if (forgetBankButton == null || sheet == null)
            {
                return;
            }

            PlayerDataManager manager = PlayerDataManager.Instance;
            bool show = manager != null && BankHabits.Has(manager.CurrentPlayer);
            forgetBankButton.gameObject.SetActive(show);
            sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight + (show ? RowHeight + RowGap : 0f));
        }

        void ForgetBank()
        {
            if (!open || PlayerDataManager.Instance == null)
            {
                return;
            }

            PlayerDataManager.Instance.ForgetBankHabits();
            RefreshForgetBank();
            NoticeBanner.Show(BankFeedCopy.ForgottenNotice);
        }

        // ================================================================ build

        void BuildUi()
        {
            canvas = UiCanvasFactory.Create("Pause_Canvas", UiTheme.Sort.Pause, transform, out RectTransform safeRoot);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            Image scrim = UiCanvasFactory.FullBleed(canvas, "Scrim", UiTheme.Scrim);
            scrim.raycastTarget = true;

            Image sheetImage = UiKit.Panel(safeRoot, "Sheet", UiTheme.Paper, UiTheme.RadiusSheet, true);
            sheetImage.raycastTarget = true;
            sheet = sheetImage.rectTransform;
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight);

            // Top row: title left, Resume right.
            Text title = UiKit.Label(sheet, "Title", "Paused", UiTheme.Title, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(title.rectTransform, Pad, Pad, 700f, TopRow);

            Button resume = UiKit.PrimaryButton(sheet, "Resume", Close, ButtonWidth);
            SetTopLeft((RectTransform)resume.transform, SheetWidth - Pad - ButtonWidth, Pad, ButtonWidth, TopRow);

            float columnsTop = Pad + TopRow + UiTheme.Space30;
            float leftX = Pad;
            float rightX = Pad + ColumnWidth + ColumnGap;

            // Left column: three toggle rows.
            soundToggle = new SettingToggle(sheet, "Sound", "audioOn", "audioOff", leftX, columnsTop, v => GameSettings.SoundOn = v);
            musicToggle = new SettingToggle(sheet, "Music", "musicOn", "musicOff", leftX, columnsTop + RowHeight + RowGap,
                v => GameSettings.MusicOn = v);
            float lastRowTop = columnsTop + 2f * (RowHeight + RowGap);
            motionToggle = new SettingToggle(sheet, "Reduce motion", null, null, leftX, lastRowTop, v => GameSettings.ReduceMotion = v);

            // Right column: Text speed and its segmented control, Start over at the bottom.
            Text speedLabel = UiKit.Label(sheet, "Text speed", "Text speed", UiTheme.Label, UiTheme.TextSecondary);
            SetTopLeft(speedLabel.rectTransform, rightX, columnsTop, ColumnWidth, 44f);
            string[] names = { "Slow", "Normal", "Instant" };
            for (int i = 0; i < names.Length; i++)
            {
                int value = i;
                segments[i] = new SegmentButton(sheet, names[i], rightX + i * (SegmentWidth + 10f), columnsTop + 44f + 12f,
                    () => GameSettings.TextSpeed = value);
            }

            Button startOver = UiKit.SecondaryButton(sheet, "Start over…", OpenConfirm, ButtonWidth);
            SetTopLeft((RectTransform)startOver.transform, rightX, lastRowTop, ButtonWidth, RowHeight);
            Text startOverText = startOver.GetComponentInChildren<Text>();
            if (startOverText != null)
            {
                startOverText.color = UiTheme.Attention;
            }

            forgetBankButton = UiKit.SecondaryButton(sheet, BankFeedCopy.ForgetButton, ForgetBank, ColumnWidth);
            SetTopLeft((RectTransform)forgetBankButton.transform, rightX, lastRowTop + RowHeight + RowGap, ColumnWidth,
                RowHeight);

            canvas.gameObject.SetActive(false);
            BuildConfirm();
            Refresh();
        }

        void BuildConfirm()
        {
            confirmCanvas = UiCanvasFactory.Create("ResetConfirm_Canvas", UiTheme.Sort.ResetConfirm, transform,
                out RectTransform safeRoot);
            confirmGroup = confirmCanvas.gameObject.AddComponent<CanvasGroup>();
            Image scrim = UiCanvasFactory.FullBleed(confirmCanvas, "Scrim", UiTheme.ScrimLight);
            scrim.raycastTarget = true;

            Image sheetImage = UiKit.Panel(safeRoot, "Sheet", UiTheme.Paper, UiTheme.RadiusSheet, true);
            sheetImage.raycastTarget = true;
            confirmSheet = sheetImage.rectTransform;
            confirmSheet.anchorMin = confirmSheet.anchorMax = confirmSheet.pivot = new Vector2(0.5f, 0.5f);
            confirmSheet.sizeDelta = new Vector2(ConfirmWidth, ConfirmHeight);

            float inner = ConfirmWidth - 2f * Pad;
            Text title = UiKit.Label(confirmSheet, "Title", "Start over?", UiTheme.Title, UiTheme.TextPrimary);
            SetTopLeft(title.rectTransform, Pad, Pad, inner, 72f);

            Text body = UiKit.Label(confirmSheet, "Body",
                "This deletes your week and your progress. Your settings stay. You can't undo this.",
                UiTheme.Body, UiTheme.TextSecondary);
            SetTopLeft(body.rectTransform, Pad, Pad + 72f + UiTheme.Space20, inner,
                ConfirmHeight - (Pad + 72f + UiTheme.Space20) - Pad - UiTheme.TargetMin - UiTheme.Space20);

            const float confirmButtonWidth = 440f;
            Button delete = UiKit.SecondaryButton(confirmSheet, "Delete and start over", StartOver, confirmButtonWidth);
            var deleteRect = (RectTransform)delete.transform;
            deleteRect.anchorMin = deleteRect.anchorMax = deleteRect.pivot = new Vector2(0f, 0f);
            deleteRect.anchoredPosition = new Vector2(Pad, Pad);
            Text deleteText = delete.GetComponentInChildren<Text>();
            if (deleteText != null)
            {
                deleteText.color = UiTheme.Attention;
            }

            Button keep = UiKit.PrimaryButton(confirmSheet, "Keep playing", CloseConfirm, 360f);
            var keepRect = (RectTransform)keep.transform;
            keepRect.anchorMin = keepRect.anchorMax = keepRect.pivot = new Vector2(1f, 0f);
            keepRect.anchoredPosition = new Vector2(-Pad, Pad);

            confirmCanvas.gameObject.SetActive(false);
        }

        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        // ================================================================ controls

        /// <summary>A 660 x 144 row (the whole row is the hit target): optional icon, label, a 120 x 72 track
        /// (on <c>AccentPrimary</c>, off <c>Sunken</c> + outline) and "On"/"Off" beside it.</summary>
        sealed class SettingToggle
        {
            readonly Image outline;
            readonly Image track;
            readonly RectTransform knob;
            readonly Text state;
            readonly Image icon;
            readonly string iconOn;
            readonly string iconOff;
            readonly Action<bool> onChange;
            bool value;

            public SettingToggle(RectTransform parent, string label, string iconOn, string iconOff, float x, float y, Action<bool> onChange)
            {
                this.iconOn = iconOn;
                this.iconOff = iconOff;
                this.onChange = onChange;

                RectTransform row = UiKit.Rect(parent, "Toggle " + label);
                SetTopLeft(row, x, y, ColumnWidth, RowHeight);
                var hit = row.gameObject.AddComponent<Image>();
                hit.sprite = UiKit.White;
                hit.color = Color.clear;
                hit.raycastTarget = true;
                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = hit;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
                button.onClick.AddListener(() =>
                {
                    UiKit.NotifyButtonClicked();
                    Set(!value);
                    onChange?.Invoke(value);
                });

                float textX = 0f;
                if (!string.IsNullOrEmpty(iconOn))
                {
                    icon = UiKit.IconImage(row, "Icon", iconOn, 64f, UiTheme.TextSecondary);
                    RectTransform iconRect = icon.rectTransform;
                    iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, 0.5f);
                    iconRect.anchoredPosition = Vector2.zero;
                    textX = 64f + UiTheme.Space20;
                }

                Text name = UiKit.Label(row, "Label", label, UiTheme.Body.WithWeight(UiFontWeight.Bold), UiTheme.TextPrimary,
                    TextAnchor.MiddleLeft);
                name.horizontalOverflow = HorizontalWrapMode.Overflow;
                name.rectTransform.offsetMin = new Vector2(textX, 0f);
                name.rectTransform.offsetMax = new Vector2(-(TrackWidth + 120f), 0f);

                state = UiKit.Label(row, "State", "Off", UiTheme.Label, UiTheme.TextSecondary, TextAnchor.MiddleLeft);
                RectTransform stateRect = state.rectTransform;
                stateRect.anchorMin = new Vector2(1f, 0f);
                stateRect.anchorMax = new Vector2(1f, 1f);
                stateRect.pivot = new Vector2(1f, 0.5f);
                stateRect.sizeDelta = new Vector2(90f, 0f);
                stateRect.anchoredPosition = Vector2.zero;

                outline = UiKit.Panel(row, "Track outline", UiTheme.BorderControl, TrackHeight * 0.5f, false);
                RectTransform outlineRect = outline.rectTransform;
                outlineRect.anchorMin = outlineRect.anchorMax = new Vector2(1f, 0.5f);
                outlineRect.pivot = new Vector2(1f, 0.5f);
                outlineRect.sizeDelta = new Vector2(TrackWidth, TrackHeight);
                outlineRect.anchoredPosition = new Vector2(-100f, 0f);
                outline.raycastTarget = false;

                track = UiKit.Panel(outlineRect, "Track", UiTheme.Sunken, TrackHeight * 0.5f - UiTheme.Stroke, false);
                track.rectTransform.offsetMin = new Vector2(UiTheme.Stroke, UiTheme.Stroke);
                track.rectTransform.offsetMax = new Vector2(-UiTheme.Stroke, -UiTheme.Stroke);
                track.raycastTarget = false;

                Image knobImage = UiKit.SpriteImage(outlineRect, "Knob", UiKit.Circle, TrackHeight - 16f, UiTheme.Card);
                knob = knobImage.rectTransform;
            }

            public void Set(bool on)
            {
                value = on;
                track.color = on ? UiTheme.AccentPrimary : UiTheme.Sunken;
                outline.color = on ? UiTheme.AccentPrimary : UiTheme.BorderControl;
                state.text = on ? "On" : "Off";
                float travel = (TrackWidth - TrackHeight) * 0.5f;
                knob.anchoredPosition = new Vector2(on ? travel : -travel, 0f);
                if (icon != null)
                {
                    Sprite sprite = UiKit.Icon(on ? iconOn : iconOff);
                    if (sprite != null)
                    {
                        icon.sprite = sprite;
                        icon.color = UiTheme.TextSecondary;
                    }
                }
            }
        }

        /// <summary>One 200 x 144 segment of the Text speed control.</summary>
        sealed class SegmentButton
        {
            readonly Image outline;
            readonly Image fill;
            readonly Text label;

            public SegmentButton(RectTransform parent, string text, float x, float y, Action onTap)
            {
                outline = UiKit.Panel(parent, "Segment " + text, UiTheme.BorderControl, UiTheme.RadiusButton, false);
                SetTopLeft(outline.rectTransform, x, y, SegmentWidth, RowHeight);
                outline.raycastTarget = true;

                fill = UiKit.Panel(outline.rectTransform, "Fill", UiTheme.Card, UiTheme.RadiusButton - UiTheme.Stroke, false);
                fill.rectTransform.offsetMin = new Vector2(UiTheme.Stroke, UiTheme.Stroke);
                fill.rectTransform.offsetMax = new Vector2(-UiTheme.Stroke, -UiTheme.Stroke);
                fill.raycastTarget = false;

                label = UiKit.Label(outline.rectTransform, "Label", text, UiTheme.Label, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;

                var button = outline.gameObject.AddComponent<Button>();
                button.targetGraphic = outline;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
                button.onClick.AddListener(() =>
                {
                    UiKit.NotifyButtonClicked();
                    onTap?.Invoke();
                });
            }

            public void SetSelected(bool selected)
            {
                outline.color = selected ? UiTheme.AccentPrimary : UiTheme.BorderControl;
                fill.color = selected ? UiTheme.AccentPrimary : UiTheme.Card;
                label.color = selected ? UiTheme.TextOnInverse : UiTheme.TextPrimary;
            }
        }
    }
}
