using System;
using System.Collections.Generic;
using MaliGo.PlayerIdentity;
using MaliGo.Data;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// The Home and Bank sheets (DESIGN_SPEC §5.4.6, sort 50): a 1560 x 860 paper sheet over a 55% scrim with a
    /// title, a close button, a status card on the left (label / value rows, optional progress bar) and an action
    /// column on the right (one primary button, or groups of chips). The sheet is a modal (§7.2): it pushes on
    /// open, pops on close, on disable and on destroy; Android back closes it. It stays open across actions and
    /// refreshes its values in place whenever the player data changes.
    /// </summary>
    public class ActionPanelUI : MonoBehaviour
    {
        const float SheetWidth = 1560f;
        const float SheetHeight = 860f;
        const float StatusWidth = 700f;
        const float ActionsWidth = 640f;
        const float ContentTop = 152f;
        const float RowHeight = 72f;
        const float DividerHeight = 20f;
        const float CaptionRowHeight = 48f;
        const float CardPadding = 24f;
        const float ChipWidth = 196f;
        const float ProgressWidth = 600f;
        const float ProgressHeight = 16f;

        /// <summary>
        /// One action. A plain button (no <see cref="Group"/>) is drawn 640 x 144, the first one primary. Buttons
        /// with a <see cref="Group"/> are drawn as 196 x 144 chips under that group's heading; a chip whose
        /// <see cref="Enabled"/> returns false is dimmed to 45% and the group shows <see cref="DisabledCaption"/>.
        /// </summary>
        public struct ActionButton
        {
            public string Label;
            public Action OnClick;
            public string Icon;
            public string Group;
            public Func<bool> Enabled;
            public string DisabledCaption;

            public ActionButton(string label, Action onClick)
            {
                Label = label;
                OnClick = onClick;
                Icon = null;
                Group = null;
                Enabled = null;
                DisabledCaption = null;
            }

            public ActionButton(string label, Action onClick, string icon) : this(label, onClick)
            {
                Icon = icon;
            }

            /// <summary>A chip under the heading <paramref name="group"/>.</summary>
            public static ActionButton Chip(string group, string label, string icon, Action onClick, Func<bool> enabled,
                                            string disabledCaption)
            {
                return new ActionButton(label, onClick, icon)
                {
                    Group = group,
                    Enabled = enabled,
                    DisabledCaption = disabledCaption
                };
            }
        }

        sealed class RowView
        {
            public RectTransform Rect;
            public Text Label;
            public Text Value;
            public Image Divider;
        }

        sealed class ChipView
        {
            public Button Button;
            public CanvasGroup Group;
            public Func<bool> Enabled;
        }

        sealed class GroupView
        {
            public Text Caption;
            public string DisabledCaption;
            public readonly List<ChipView> Chips = new List<ChipView>();
        }

        Canvas canvas;
        CanvasGroup canvasGroup;
        RectTransform sheet;
        Text titleText;
        RectTransform statusCard;
        RectTransform rowsRoot;
        Text bodyText;
        RectTransform progressRoot;
        Image progressFill;
        RectTransform actionsRoot;

        readonly List<RowView> rowViews = new List<RowView>();
        readonly List<GroupView> groupViews = new List<GroupView>();

        Func<string> bodyProvider;
        Func<IReadOnlyList<(string label, string value, bool attention)>> rowsProvider;
        Func<float> progressProvider;
        bool open;
        bool subscribed;

        /// <summary>Raised when the sheet closes (close button, back, or <see cref="Hide"/>).</summary>
        public event Action Closed;

        public bool IsOpen => open;

        void Awake()
        {
            BuildUiIfNeeded();
            HideImmediate();
        }

        void OnDisable()
        {
            Unsubscribe();
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
            Unsubscribe();
            UiModal.Pop(this);
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
        }

        // ================================================================ public API

        /// <summary>Title, a live status text and the actions (kept for compatibility).</summary>
        public void Show(string title, Func<string> bodyTextProvider, IReadOnlyList<ActionButton> actions)
        {
            BuildUiIfNeeded();
            bodyProvider = bodyTextProvider;
            rowsProvider = null;
            progressProvider = null;
            Open(title, actions);
        }

        /// <summary>Title, live status rows (label, value, attention) and the actions. A row with a null label and
        /// value is drawn as a divider; a row with an empty value is drawn as a caption line.</summary>
        public void ShowRows(string title, Func<IReadOnlyList<(string label, string value, bool attention)>> rows,
                             IReadOnlyList<ActionButton> actions)
        {
            ShowRows(title, rows, actions, null);
        }

        /// <summary>As the three-argument ShowRows, plus a 600 x 16 progress bar (0..1) under the rows.</summary>
        public void ShowRows(string title, Func<IReadOnlyList<(string label, string value, bool attention)>> rows,
                             IReadOnlyList<ActionButton> actions, Func<float> progress)
        {
            BuildUiIfNeeded();
            bodyProvider = null;
            rowsProvider = rows;
            progressProvider = progress;
            Open(title, actions);
        }

        /// <summary>Closes the sheet and pops it from the modal stack at once (the fade-out is visual only).</summary>
        public void Hide()
        {
            bool wasOpen = open;
            open = false;
            Unsubscribe();
            UiModal.Pop(this);

            if (canvas == null || !canvas.gameObject.activeSelf)
            {
                if (wasOpen)
                {
                    Closed?.Invoke();
                }
                return;
            }

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            UiTween.Fade(canvasGroup, 0f, UiTheme.Motion.SheetOut, UiTween.Ease.Accelerate, () =>
            {
                if (!open && canvas != null)
                {
                    canvas.gameObject.SetActive(false);
                }
            });

            if (wasOpen)
            {
                Closed?.Invoke();
            }
        }

        public void HideImmediate()
        {
            open = false;
            Unsubscribe();
            UiModal.Pop(this);
            if (canvas != null)
            {
                UiTween.Stop(canvasGroup);
                canvasGroup.alpha = 0f;
                canvas.gameObject.SetActive(false);
            }
        }

        /// <summary>Re-reads the rows / body, the progress and every chip's enabled state.</summary>
        public void Refresh()
        {
            if (canvas == null)
            {
                return;
            }

            RefreshStatus();
            RefreshChips();
        }

        // ================================================================ open / close

        void Open(string title, IReadOnlyList<ActionButton> actions)
        {
            titleText.text = title ?? "";
            BuildActions(actions);
            RefreshStatus();
            RefreshChips();

            bool wasVisible = canvas.gameObject.activeSelf && open;
            open = true;
            canvas.gameObject.SetActive(true);
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            UiModal.Push(this, Hide);
            Subscribe();

            if (!wasVisible)
            {
                UiTween.Stop(canvasGroup);
                canvasGroup.alpha = 0f;
                UiTween.Fade(canvasGroup, 1f, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
                sheet.anchoredPosition = new Vector2(0f, -UiTheme.Motion.SheetRise);
                UiTween.Move(sheet, Vector2.zero, UiTheme.Motion.SheetIn, UiTween.Ease.Decelerate);
            }
        }

        void Subscribe()
        {
            if (subscribed || PlayerDataManager.Instance == null)
            {
                return;
            }

            PlayerDataManager.Instance.OnPlayerDataChanged += HandlePlayerDataChanged;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.OnPlayerDataChanged -= HandlePlayerDataChanged;
            }

            subscribed = false;
        }

        void HandlePlayerDataChanged(PlayerData data)
        {
            if (open)
            {
                Refresh();
            }
        }

        // ================================================================ status card

        void RefreshStatus()
        {
            float contentHeight;
            if (rowsProvider != null)
            {
                bodyText.gameObject.SetActive(false);
                rowsRoot.gameObject.SetActive(true);
                IReadOnlyList<(string label, string value, bool attention)> rows = null;
                try
                {
                    rows = rowsProvider();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActionPanelUI] Building the rows failed: {ex}");
                }

                contentHeight = LayoutRows(rows);
            }
            else
            {
                rowsRoot.gameObject.SetActive(false);
                bodyText.gameObject.SetActive(true);
                string body = bodyProvider != null ? bodyProvider() : "";
                float width = StatusWidth - 2f * CardPadding;
                bodyText.text = UiTextLayout.WrapKeepingAmounts(bodyText, body ?? "", width);
                contentHeight = Mathf.Max(RowHeight, UiTextLayout.CountLines(bodyText, body ?? "", width)
                                          * UiTheme.Body.Size * UiTheme.Body.LineSpacing);
                SetTopLeft(bodyText.rectTransform, CardPadding, CardPadding, width, contentHeight);
            }

            float height = CardPadding + contentHeight + CardPadding;
            if (progressProvider != null)
            {
                progressRoot.gameObject.SetActive(true);
                float value = 0f;
                try
                {
                    value = Mathf.Clamp01(progressProvider());
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActionPanelUI] Reading the progress failed: {ex}");
                }

                progressFill.fillAmount = value;
                SetTopLeft(progressRoot, CardPadding, CardPadding + contentHeight + 10f, ProgressWidth, ProgressHeight);
                height += 10f + ProgressHeight;
            }
            else
            {
                progressRoot.gameObject.SetActive(false);
            }

            float maxHeight = SheetHeight - ContentTop - UiTheme.SheetPadding;
            SetTopLeft(statusCard, UiTheme.SheetPadding, ContentTop, StatusWidth, Mathf.Min(height, maxHeight));
        }

        float LayoutRows(IReadOnlyList<(string label, string value, bool attention)> rows)
        {
            int count = rows?.Count ?? 0;
            while (rowViews.Count < count)
            {
                rowViews.Add(CreateRow(rowViews.Count));
            }

            float y = 0f;
            float width = StatusWidth - 2f * CardPadding;
            for (int i = 0; i < rowViews.Count; i++)
            {
                RowView view = rowViews[i];
                if (i >= count)
                {
                    view.Rect.gameObject.SetActive(false);
                    continue;
                }

                (string label, string value, bool attention) = rows[i];
                view.Rect.gameObject.SetActive(true);

                float height;
                if (label == null && value == null)
                {
                    height = DividerHeight;
                    view.Divider.gameObject.SetActive(true);
                    view.Label.gameObject.SetActive(false);
                    view.Value.gameObject.SetActive(false);
                }
                else if (string.IsNullOrEmpty(value))
                {
                    height = CaptionRowHeight;
                    view.Divider.gameObject.SetActive(false);
                    view.Label.gameObject.SetActive(true);
                    view.Value.gameObject.SetActive(false);
                    SetText(view.Label, label);
                    view.Label.alignment = TextAnchor.UpperLeft;
                }
                else
                {
                    height = RowHeight;
                    view.Divider.gameObject.SetActive(false);
                    view.Label.gameObject.SetActive(true);
                    view.Value.gameObject.SetActive(true);
                    SetText(view.Label, label);
                    view.Label.alignment = TextAnchor.MiddleLeft;
                    SetText(view.Value, value);
                    view.Value.color = attention ? UiTheme.Attention : UiTheme.TextPrimary;
                }

                SetTopLeft(view.Rect, 0f, y, width, height);
                y += height;
            }

            SetTopLeft(rowsRoot, CardPadding, CardPadding, width, y);
            return y;
        }

        static void SetText(Text text, string value)
        {
            value ??= "";
            if (text.text != value)
            {
                text.text = value;
            }
        }

        RowView CreateRow(int index)
        {
            RectTransform rect = UiKit.Rect(rowsRoot, "Row " + index);
            Text label = UiKit.Label(rect, "Label", "", UiTheme.Caption, UiTheme.TextMuted, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            Text value = UiKit.Label(rect, "Value", "", UiTheme.HudValue, UiTheme.TextPrimary, TextAnchor.MiddleRight);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;

            var dividerObject = new GameObject("Divider", typeof(RectTransform));
            var dividerRect = (RectTransform)dividerObject.transform;
            dividerRect.SetParent(rect, false);
            dividerRect.anchorMin = new Vector2(0f, 0.5f);
            dividerRect.anchorMax = new Vector2(1f, 0.5f);
            dividerRect.pivot = new Vector2(0.5f, 0.5f);
            dividerRect.sizeDelta = new Vector2(0f, 2f);
            dividerRect.anchoredPosition = Vector2.zero;
            var divider = dividerObject.AddComponent<Image>();
            divider.sprite = UiKit.White;
            divider.color = UiTheme.BorderSubtle;
            divider.raycastTarget = false;

            return new RowView { Rect = rect, Label = label, Value = value, Divider = divider };
        }

        // ================================================================ actions

        void BuildActions(IReadOnlyList<ActionButton> actions)
        {
            for (int i = actionsRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = actionsRoot.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }

            groupViews.Clear();
            if (actions == null)
            {
                return;
            }

            float y = 0f;
            bool firstPlain = true;
            GroupView currentGroup = null;
            string currentGroupName = null;
            int chipIndex = 0;

            foreach (ActionButton action in actions)
            {
                if (string.IsNullOrEmpty(action.Group))
                {
                    if (currentGroup != null)
                    {
                        y = CloseGroup(currentGroup, y);
                        currentGroup = null;
                        currentGroupName = null;
                    }

                    Button button = firstPlain
                        ? UiKit.PrimaryButton(actionsRoot, action.Label, () => Run(action), ActionsWidth)
                        : UiKit.SecondaryButton(actionsRoot, action.Label, () => Run(action), ActionsWidth);
                    var rect = (RectTransform)button.transform;
                    SetTopLeft(rect, 0f, y, ActionsWidth, UiTheme.TargetMin);
                    if (!string.IsNullOrEmpty(action.Icon))
                    {
                        Image icon = UiKit.IconImage(rect, "Icon", action.Icon, 56f,
                            firstPlain ? UiTheme.TextOnInverse : UiTheme.TextPrimary);
                        PlaceLeft(icon.rectTransform, 32f);
                    }

                    firstPlain = false;
                    y += UiTheme.TargetMin + UiTheme.TappableGap;
                    continue;
                }

                if (currentGroup == null || currentGroupName != action.Group)
                {
                    if (currentGroup != null)
                    {
                        y = CloseGroup(currentGroup, y);
                    }

                    currentGroupName = action.Group;
                    currentGroup = new GroupView { DisabledCaption = action.DisabledCaption };
                    groupViews.Add(currentGroup);
                    Text heading = UiKit.Label(actionsRoot, "Group " + action.Group, action.Group, UiTheme.Label,
                        UiTheme.TextSecondary, TextAnchor.MiddleLeft);
                    SetTopLeft(heading.rectTransform, 0f, y, ActionsWidth, 50f);
                    y += 50f + UiTheme.Space10;
                    chipIndex = 0;
                }

                currentGroup.Chips.Add(CreateChip(action, chipIndex, y));
                if (string.IsNullOrEmpty(currentGroup.DisabledCaption))
                {
                    currentGroup.DisabledCaption = action.DisabledCaption;
                }

                chipIndex++;
            }

            if (currentGroup != null)
            {
                CloseGroup(currentGroup, y);
            }
        }

        // Chips of a group sit in one row at the current y; the caption goes under the row.
        float CloseGroup(GroupView group, float y)
        {
            y += UiTheme.TargetMin;
            group.Caption = UiKit.Label(actionsRoot, "Caption", group.DisabledCaption ?? "", UiTheme.Label,
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            SetTopLeft(group.Caption.rectTransform, 0f, y + 4f, ActionsWidth, 46f);
            group.Caption.gameObject.SetActive(false);
            return y + 50f + UiTheme.Space30;
        }

        ChipView CreateChip(ActionButton action, int index, float y)
        {
            Button button = UiKit.SecondaryButton(actionsRoot, action.Label, () => Run(action), ChipWidth);
            var rect = (RectTransform)button.transform;
            SetTopLeft(rect, index * (ChipWidth + UiTheme.TappableGap), y, ChipWidth, UiTheme.TargetMin);

            Transform fill = rect.Find("Fill");
            if (fill != null && fill.TryGetComponent(out Image fillImage))
            {
                fillImage.color = UiTheme.Sunken;
            }

            Transform labelTransform = rect.Find("Label");
            if (!string.IsNullOrEmpty(action.Icon) && labelTransform is RectTransform labelRect)
            {
                Image icon = UiKit.IconImage(rect, "Icon", action.Icon, 44f, UiTheme.TextPrimary);
                PlaceLeft(icon.rectTransform, 18f);
                labelRect.offsetMin = new Vector2(68f, 0f);
                labelRect.offsetMax = new Vector2(-12f, 0f);
            }

            var group = button.gameObject.AddComponent<CanvasGroup>();
            return new ChipView { Button = button, Group = group, Enabled = action.Enabled };
        }

        void Run(ActionButton action)
        {
            if (!open)
            {
                return;
            }

            try
            {
                action.OnClick?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ActionPanelUI] Action '{action.Label}' failed: {ex}");
            }

            if (open)
            {
                Refresh();
            }
        }

        void RefreshChips()
        {
            foreach (GroupView group in groupViews)
            {
                bool anyDisabled = false;
                foreach (ChipView chip in group.Chips)
                {
                    bool enabled = true;
                    try
                    {
                        enabled = chip.Enabled == null || chip.Enabled();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[ActionPanelUI] Chip state failed: {ex}");
                    }

                    if (chip.Button != null)
                    {
                        chip.Button.interactable = enabled;
                    }

                    if (chip.Group != null)
                    {
                        chip.Group.alpha = enabled ? 1f : 0.45f;
                    }

                    anyDisabled |= !enabled;
                }

                if (group.Caption != null)
                {
                    group.Caption.gameObject.SetActive(anyDisabled && !string.IsNullOrEmpty(group.DisabledCaption));
                }
            }
        }

        // ================================================================ build

        void BuildUiIfNeeded()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = UiCanvasFactory.Create("ActionPanel_Canvas", UiTheme.Sort.Sheets, transform, out RectTransform safeRoot);
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            Image scrim = UiCanvasFactory.FullBleed(canvas, "Scrim", UiTheme.Scrim);
            scrim.raycastTarget = true;

            Image sheetImage = UiKit.Panel(safeRoot, "Sheet", UiTheme.Paper, UiTheme.RadiusSheet, true);
            sheetImage.raycastTarget = true;
            sheet = sheetImage.rectTransform;
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight);
            sheet.anchoredPosition = Vector2.zero;

            titleText = UiKit.Label(sheet, "Title", "", UiTheme.Title, UiTheme.TextPrimary, TextAnchor.UpperLeft);
            titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(titleText.rectTransform, UiTheme.SheetPadding, UiTheme.SheetPadding,
                SheetWidth - 2f * UiTheme.SheetPadding - UiTheme.TargetMin, 72f);

            Button close = UiKit.TextButton(sheet, "", Hide, UiTheme.TargetMin, UiTheme.TargetMin);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-24f, -24f);
            UiKit.IconImage(closeRect, "Icon", "cross", 56f, UiTheme.TextPrimary);

            Image card = UiKit.Panel(sheet, "Status", UiTheme.Sunken, UiTheme.RadiusCard, false);
            card.raycastTarget = false;
            statusCard = card.rectTransform;

            rowsRoot = UiKit.Rect(statusCard, "Rows");
            bodyText = UiKit.Label(statusCard, "Body", "", UiTheme.Body, UiTheme.TextSecondary, TextAnchor.UpperLeft);

            progressRoot = UiKit.Rect(statusCard, "Progress");
            var trackImage = progressRoot.gameObject.AddComponent<Image>();
            trackImage.sprite = UiKit.White;
            trackImage.color = UiTheme.BorderSubtle;
            trackImage.raycastTarget = false;
            RectTransform fillRect = UiKit.Rect(progressRoot, "Fill");
            progressFill = fillRect.gameObject.AddComponent<Image>();
            progressFill.sprite = UiKit.White;
            progressFill.color = UiTheme.AccentPrimary;
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.raycastTarget = false;
            progressRoot.gameObject.SetActive(false);

            actionsRoot = UiKit.Rect(sheet, "Actions");
            SetTopLeft(actionsRoot, SheetWidth - UiTheme.SheetPadding - ActionsWidth, ContentTop, ActionsWidth,
                SheetHeight - ContentTop - UiTheme.SheetPadding);

            canvas.gameObject.SetActive(false);
        }

        /// <summary>Anchors <paramref name="rect"/> to its parent's top-left at (x, -y) with the given size.</summary>
        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        static void PlaceLeft(RectTransform rect, float x)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
        }
    }
}
