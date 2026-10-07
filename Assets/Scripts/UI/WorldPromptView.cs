using MaliGo.UI.Kit;
using MaliGo.World;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// The one world prompt (DESIGN_SPEC §5.4.2, sort 10): a paper pill at the bottom centre, its bottom edge 330 u
    /// above the safe bottom, 120 u tall (hit rect 160), 480-1000 u wide. It shows the arbiter's current
    /// interactable: icon (Mali's portrait for the "mali" icon), text, and a "Tap" / "E" chip; greyed (muted text, no chip)
    /// when the interactable is disabled. Tapping it asks the arbiter to interact (which calls
    /// <c>OnDisabledTap</c> on a greyed one). Text, icon and state are re-read every frame and only written when
    /// they change. Not a modal; the arbiter clears <c>Current</c> while a modal is open.
    /// Anchors: <c>world.prompt</c> while visible, <c>world.prompt.home</c> while visible and current is Home.
    /// </summary>
    public class WorldPromptView : MonoBehaviour
    {
        public const string AnchorPrompt = "world.prompt";
        public const string AnchorPromptHome = "world.prompt.home";

        const float BottomEdge = 330f;
        const float VisibleHeight = 120f;
        const float HitHeight = 160f;
        const float MinWidth = 480f;
        const float MaxWidth = 1000f;
        const float Pad = 24f;
        const float IconSize = 56f;
        const float Gap = 16f;
        const float ChipWidth = 88f;
        const float ChipHeight = 56f;

        static WorldPromptView instance;

        Canvas canvas;
        CanvasGroup group;
        RectTransform root;
        RectTransform pill;
        Image icon;
        Text label;
        RectTransform chip;
        Text chipLabel;
        Button button;

        IInteractable shown;
        bool visible;
        string lastText;
        string lastIcon;
        bool lastEnabled = true;
        string lastChip;
        bool homeAnchorRegistered;
        bool promptAnchorRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>Creates the prompt once per scene.</summary>
        public static WorldPromptView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            WorldPromptView existing = FindFirstObjectByType<WorldPromptView>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return new GameObject("WorldPromptView").AddComponent<WorldPromptView>();
        }

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            Build();
        }

        void OnDisable()
        {
            UnregisterAnchors();
        }

        void OnDestroy()
        {
            UnregisterAnchors();
            if (instance == this)
            {
                instance = null;
            }
        }

        void Update()
        {
            InteractionArbiter arbiter = InteractionArbiter.Instance;
            IInteractable current = arbiter != null ? arbiter.Current : null;
            if (current != null && (current as Object) == null)
            {
                current = null;
            }

            if (!ReferenceEquals(current, shown))
            {
                shown = current;
                if (current != null)
                {
                    lastText = null;
                    lastIcon = null;
                    Apply(current);
                    group.alpha = 0f;
                    UiTween.Fade(group, 1f, UiTheme.Motion.Fade);
                }
                else
                {
                    UiTween.Fade(group, 0f, UiTheme.Motion.Fade);
                }

                SetVisible(current != null);
            }
            else if (current != null)
            {
                Apply(current);
            }

            UpdateAnchors();
        }

        void SetVisible(bool value)
        {
            visible = value;
            group.interactable = value;
            group.blocksRaycasts = value;
        }

        void Apply(IInteractable current)
        {
            string text = current.PromptText ?? "";
            string iconName = current.PromptIcon ?? "";
            bool enabled = current.IsEnabled;
            string chipText = ChipText();

            bool layoutChanged = false;
            if (text != lastText)
            {
                lastText = text;
                label.text = text;
                layoutChanged = true;
            }

            if (iconName != lastIcon)
            {
                lastIcon = iconName;
                bool isMali = iconName == "mali";
                Sprite sprite = isMali ? UiKit.MaliPortrait : (string.IsNullOrEmpty(iconName) ? null : UiKit.Icon(iconName));
                icon.sprite = sprite;
                icon.color = sprite == null ? Color.clear : (isMali ? Color.white : UiTheme.AccentPrimary);
            }

            if (enabled != lastEnabled || layoutChanged)
            {
                lastEnabled = enabled;
                label.color = enabled ? UiTheme.TextPrimary : UiTheme.TextMuted;
                chip.gameObject.SetActive(enabled);
                layoutChanged = true;
            }

            if (chipText != lastChip)
            {
                lastChip = chipText;
                chipLabel.text = chipText;
            }

            if (layoutChanged)
            {
                Layout(text, enabled);
            }
        }

        void Layout(string text, bool withChip)
        {
            float textWidth = UiTextLayout.MeasureWidth(label, text);
            float right = withChip ? Gap + ChipWidth + Pad : Pad;
            float width = Mathf.Clamp(Pad + IconSize + Gap + textWidth + right, MinWidth, MaxWidth);
            root.sizeDelta = new Vector2(width, HitHeight);
            label.rectTransform.offsetMin = new Vector2(Pad + IconSize + Gap, 0f);
            label.rectTransform.offsetMax = new Vector2(-right, 0f);
        }

        static string ChipText()
        {
            if (Application.isMobilePlatform)
            {
                return "Tap";
            }

            return UnityEngine.InputSystem.Keyboard.current != null ? "E" : "Tap";
        }

        void UpdateAnchors()
        {
            bool showPrompt = visible && shown != null;
            if (showPrompt != promptAnchorRegistered)
            {
                promptAnchorRegistered = showPrompt;
                if (showPrompt)
                {
                    UiAnchors.Register(AnchorPrompt, pill);
                }
                else
                {
                    UiAnchors.Unregister(AnchorPrompt, pill);
                }
            }

            bool showHome = showPrompt && shown is HomeInteraction;
            if (showHome != homeAnchorRegistered)
            {
                homeAnchorRegistered = showHome;
                if (showHome)
                {
                    UiAnchors.Register(AnchorPromptHome, pill);
                }
                else
                {
                    UiAnchors.Unregister(AnchorPromptHome, pill);
                }
            }
        }

        void UnregisterAnchors()
        {
            if (pill == null)
            {
                return;
            }

            UiAnchors.Unregister(AnchorPrompt, pill);
            UiAnchors.Unregister(AnchorPromptHome, pill);
            promptAnchorRegistered = false;
            homeAnchorRegistered = false;
        }

        void OnTapped()
        {
            if (visible && shown != null)
            {
                InteractionArbiter.RequestInteract();
            }
        }

        void Build()
        {
            canvas = UiCanvasFactory.Create("WorldPrompt_Canvas", UiTheme.Sort.WorldPrompt, transform, out RectTransform safeRoot);

            var rootObject = new GameObject("Prompt", typeof(RectTransform));
            rootObject.layer = safeRoot.gameObject.layer;
            root = (RectTransform)rootObject.transform;
            root.SetParent(safeRoot, false);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(MinWidth, HitHeight);
            root.anchoredPosition = new Vector2(0f, BottomEdge - (HitHeight - VisibleHeight) * 0.5f);

            group = rootObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;

            var hit = rootObject.AddComponent<Image>();
            hit.sprite = UiKit.White;
            hit.color = Color.clear;
            hit.raycastTarget = true;

            button = rootObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() =>
            {
                UiKit.NotifyButtonClicked();
                OnTapped();
            });

            Image pillImage = UiKit.Panel(root, "Pill", UiTheme.Paper, UiTheme.RadiusPill(VisibleHeight), UiTheme.ShadowCard);
            pillImage.raycastTarget = false;
            pill = pillImage.rectTransform;
            float inset = (HitHeight - VisibleHeight) * 0.5f;
            pill.offsetMin = new Vector2(0f, inset);
            pill.offsetMax = new Vector2(0f, -inset);

            icon = UiKit.SpriteImage(pill, "Icon", null, IconSize, UiTheme.AccentPrimary);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(Pad, 0f);

            label = UiKit.Label(pill, "Text", "", UiTheme.Body.WithWeight(UiFontWeight.Bold), UiTheme.TextPrimary,
                TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            Image chipImage = UiKit.Panel(pill, "Chip", UiTheme.Gold, UiTheme.RadiusPill(ChipHeight), false);
            chipImage.raycastTarget = false;
            chip = chipImage.rectTransform;
            chip.anchorMin = chip.anchorMax = new Vector2(1f, 0.5f);
            chip.pivot = new Vector2(1f, 0.5f);
            chip.sizeDelta = new Vector2(ChipWidth, ChipHeight);
            chip.anchoredPosition = new Vector2(-Pad, 0f);
            chipLabel = UiKit.Label(chip, "Label", "Tap", UiTheme.Caption.WithWeight(UiFontWeight.Black),
                UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            chipLabel.horizontalOverflow = HorizontalWrapMode.Overflow;

            SetVisible(false);
        }
    }
}
