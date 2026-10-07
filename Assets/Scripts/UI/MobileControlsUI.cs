using MaliGo.Characters;
using MaliGo.UI.Kit;
using MaliGo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// On-screen joystick (bottom-left) and the combined action / Talk button (bottom-right) for touch
    /// (DESIGN_SPEC §5.4.2, sort 25), with Mali docked on top of the button.
    ///
    /// The joystick feeds <c>MaliGoPlayerController.SetVirtualJoystickInput</c>. The gold button has one job at a
    /// time: when the <see cref="InteractionArbiter"/> offers something in the world it shows that interactable's
    /// verb ("Open", "Look", "Work") and a tap asks the arbiter to interact; otherwise it is Mali's Talk button. It
    /// is a compact round "Talk", and widens to the left into a "Talk to Mali" pill while
    /// <see cref="MaliCompanionInteraction.HasSomethingToSay"/> is true (UiTween, instant with reduce motion). With
    /// neither a world target nor Mali it is dimmed to 40% with no label.
    ///
    /// Mali's portrait sits on top of the button's round end (the right end, which never moves) with a slow idle
    /// bob (off with reduce motion); tapping her also talks to her. Her dialogue box points its tail at this dock
    /// (<c>MaliDialogueView</c> reads the <c>controls.mali</c> anchor).
    ///
    /// Everything hides (alpha 0, not interactable) while any modal is open (§7.2). The joystick is reset when
    /// hidden, on pause, on focus loss and on disable. Anchors: <c>controls.joystick</c>, <c>controls.act</c>,
    /// <c>controls.mali</c>.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public const string AnchorJoystick = "controls.joystick";
        public const string AnchorAct = "controls.act";
        public const string AnchorMali = "controls.mali";

        public const string TalkLabel = "Talk";
        public const string TalkToMaliLabel = "Talk to Mali";

        const float BaseDiameter = 240f;
        const float HandleDiameter = 104f;
        const float ActionDiameter = 168f;
        const float CornerOffset = 190f;
        const float PillPadding = 44f;
        const float ResizeSeconds = 0.22f;
        const float DockSize = 132f;
        const float DockSink = 28f;     // how far Mali's portrait overlaps the top of the button
        const float DockBobDistance = 4f;
        const float DockBobPeriod = 2.4f;

        static readonly Color BaseColor = UiTheme.WithAlpha(UiTheme.Inverse, 0.45f);
        static readonly Color HandleColor = UiTheme.WithAlpha(UiTheme.TextOnInverse, 0.75f);
        static readonly Color ButtonColor = UiTheme.WithAlpha(UiTheme.Gold, 0.90f);

        Canvas canvas;
        CanvasGroup controlsGroup;
        RectTransform joystickBase;
        RectTransform joystickHandle;
        RectTransform actionRect;
        CanvasGroup actionGroup;
        Text actionLabel;
        RectTransform maliDock;
        RectTransform maliFigure;
        Vector2 maliFigureRest;
        bool maliBobbing;
        int activeJoystickPointerId = int.MinValue;
        bool hidden;
        string lastLabel;
        bool lastDimmed;
        bool lastExpanded;
        bool lastDockShown;
        float targetWidth = ActionDiameter;

        MaliGoPlayerController cachedPlayerController;

        void Awake()
        {
            BuildUi();
        }

        void OnEnable()
        {
            if (joystickBase != null)
            {
                UiAnchors.Register(AnchorJoystick, joystickBase);
                UiAnchors.Register(AnchorAct, actionRect);
                UiAnchors.Register(AnchorMali, maliDock);
            }
        }

        void OnDisable()
        {
            ResetJoystick();
            UiAnchors.Unregister(AnchorJoystick, joystickBase);
            UiAnchors.Unregister(AnchorAct, actionRect);
            UiAnchors.Unregister(AnchorMali, maliDock);
        }

        void OnDestroy()
        {
            UiTween.Stop(actionRect);
            UiTween.Stop(actionLabel);
            UiTween.Stop(maliFigure);
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                ResetJoystick();
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused)
            {
                ResetJoystick();
            }
        }

        void Update()
        {
            if (cachedPlayerController == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                cachedPlayerController = player != null ? player.GetComponent<MaliGoPlayerController>() : null;
            }

            bool modalOpen = UiModal.IsAnyOpen;
            if (modalOpen != hidden)
            {
                hidden = modalOpen;
                controlsGroup.alpha = hidden ? 0f : 1f;
                controlsGroup.interactable = !hidden;
                controlsGroup.blocksRaycasts = !hidden;
                if (hidden)
                {
                    ResetJoystick();
                }
            }

            RefreshActionButton();
        }

        /// <summary>The arbiter's live current interactable, or null.</summary>
        static IInteractable WorldTarget()
        {
            InteractionArbiter arbiter = InteractionArbiter.Instance;
            IInteractable current = arbiter != null ? arbiter.Current : null;
            return current != null && (current as Object) != null ? current : null;
        }

        /// <summary>Mali when she can be talked to, or null.</summary>
        static MaliCompanionInteraction Companion()
        {
            MaliCompanionInteraction companion = MaliCompanionInteraction.Instance;
            return companion != null && companion.IsAvailable ? companion : null;
        }

        void RefreshActionButton()
        {
            IInteractable world = WorldTarget();
            MaliCompanionInteraction companion = Companion();

            string label;
            bool expanded = false;
            if (world != null)
            {
                label = world.ActionVerb ?? "";
            }
            else if (companion != null)
            {
                expanded = companion.HasSomethingToSay;
                label = expanded ? TalkToMaliLabel : TalkLabel;
            }
            else
            {
                label = "";
            }

            bool dimmed = world == null && companion == null;
            RefreshDock(companion != null);

            if (label == lastLabel && dimmed == lastDimmed && expanded == lastExpanded)
            {
                return;
            }

            bool expanding = expanded && !lastExpanded;
            lastLabel = label;
            lastDimmed = dimmed;
            lastExpanded = expanded;

            actionLabel.text = label;
            actionLabel.gameObject.SetActive(!dimmed);
            actionGroup.alpha = dimmed ? 0.4f : 1f;
            Resize(expanded ? PillWidth(label) : ActionDiameter, expanding);
        }

        float PillWidth(string label)
        {
            return Mathf.Max(ActionDiameter, UiTextLayout.MeasureWidth(actionLabel, label) + PillPadding * 2f);
        }

        /// <summary>
        /// Widens or narrows the button towards <paramref name="width"/> (its right end stays put). Expanding hides
        /// the longer label until the pill is wide enough, then fades it in; any other change shows it at once.
        /// </summary>
        void Resize(float width, bool expanding)
        {
            UiTween.Stop(actionLabel);
            if (Mathf.Abs(width - targetWidth) < 0.5f && Mathf.Abs(actionRect.sizeDelta.x - width) < 0.5f)
            {
                SetLabelAlpha(1f);
                return;
            }

            SetLabelAlpha(expanding ? 0f : 1f);
            targetWidth = width;
            float from = actionRect.sizeDelta.x;
            UiTween.CountUp(actionRect, from, width,
                value => actionRect.sizeDelta = new Vector2(value, ActionDiameter),
                ResizeSeconds,
                () =>
                {
                    if (expanding)
                    {
                        UiTween.Fade(actionLabel, 1f, UiTheme.Motion.Fade);
                    }
                });
        }

        void SetLabelAlpha(float alpha)
        {
            Color c = actionLabel.color;
            c.a = alpha;
            actionLabel.color = c;
        }

        /// <summary>Shows Mali's dock while she is around, and keeps her idle bob in step with reduce motion.</summary>
        void RefreshDock(bool shown)
        {
            if (shown != lastDockShown)
            {
                lastDockShown = shown;
                maliDock.gameObject.SetActive(shown);
            }

            bool bob = shown && !UiTween.ReduceMotion;
            if (bob == maliBobbing)
            {
                return;
            }

            maliBobbing = bob;
            UiTween.Stop(maliFigure);
            maliFigure.anchoredPosition = maliFigureRest;
            if (bob)
            {
                UiTween.Bob(maliFigure, DockBobDistance, DockBobPeriod);
            }
        }

        // ================================================================ joystick

        public void OnPointerDown(PointerEventData eventData)
        {
            if (hidden || activeJoystickPointerId != int.MinValue)
            {
                return;
            }

            activeJoystickPointerId = eventData.pointerId;
            UpdateHandle(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != activeJoystickPointerId)
            {
                return;
            }

            if (hidden)
            {
                ResetJoystick();
                return;
            }

            UpdateHandle(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activeJoystickPointerId)
            {
                return;
            }

            ResetJoystick();
        }

        void ResetJoystick()
        {
            activeJoystickPointerId = int.MinValue;
            if (joystickHandle != null)
            {
                joystickHandle.anchoredPosition = Vector2.zero;
            }

            if (cachedPlayerController != null)
            {
                cachedPlayerController.SetVirtualJoystickInput(Vector2.zero);
            }
        }

        void UpdateHandle(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickBase, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            float maxRadius = (BaseDiameter - HandleDiameter) * 0.5f;
            Vector2 clamped = Vector2.ClampMagnitude(localPoint, maxRadius);
            joystickHandle.anchoredPosition = clamped;

            Vector2 normalized = maxRadius > 0.01f ? clamped / maxRadius : Vector2.zero;
            if (cachedPlayerController != null)
            {
                cachedPlayerController.SetVirtualJoystickInput(normalized);
            }
        }

        void OnActionPressed()
        {
            if (hidden)
            {
                return;
            }

            if (WorldTarget() != null)
            {
                MobileInputBridge.RequestInteract();
                return;
            }

            Companion()?.Talk();
        }

        void OnMaliPressed()
        {
            if (!hidden)
            {
                Companion()?.Talk();
            }
        }

        // ================================================================ build

        void BuildUi()
        {
            canvas = UiCanvasFactory.Create("MobileControls_Canvas", UiTheme.Sort.MobileControls, transform,
                out RectTransform safeRoot);
            controlsGroup = canvas.gameObject.AddComponent<CanvasGroup>();

            joystickBase = CreateCircle(safeRoot, "JoystickBase", BaseColor, BaseDiameter, new Vector2(0f, 0f),
                new Vector2(CornerOffset, CornerOffset));
            joystickBase.GetComponent<Image>().raycastTarget = true;
            var forwarder = joystickBase.gameObject.AddComponent<JoystickDragForwarder>();
            forwarder.owner = this;

            joystickHandle = CreateCircle(joystickBase, "JoystickHandle", HandleColor, HandleDiameter,
                new Vector2(0.5f, 0.5f), Vector2.zero);
            joystickHandle.GetComponent<Image>().raycastTarget = false;

            BuildActionButton(safeRoot);
        }

        void BuildActionButton(RectTransform parent)
        {
            // Pivot on the right end so the pill grows to the left and the round end (under Mali) never moves.
            actionRect = CreateCircle(parent, "ActionButton", ButtonColor, ActionDiameter, new Vector2(1f, 0f),
                new Vector2(-CornerOffset + ActionDiameter * 0.5f, CornerOffset));
            actionRect.pivot = new Vector2(1f, 0.5f);
            Image background = actionRect.GetComponent<Image>();
            UiKit.SetRadius(background, UiTheme.RadiusPill(ActionDiameter));
            actionGroup = actionRect.gameObject.AddComponent<CanvasGroup>();

            var button = actionRect.gameObject.AddComponent<Button>();
            button.targetGraphic = background;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() =>
            {
                UiKit.NotifyButtonClicked();
                OnActionPressed();
            });

            actionLabel = UiKit.Label(actionRect, "Label", "", UiTheme.Button.WithSize(36), UiTheme.TextPrimary,
                TextAnchor.MiddleCenter);
            actionLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            actionLabel.gameObject.SetActive(false);
            actionGroup.alpha = 0.4f;
            lastLabel = "";
            lastDimmed = true;
            targetWidth = ActionDiameter;

            BuildMaliDock(parent);
        }

        /// <summary>Mali's portrait sitting on the button's round end, drawn in front of it; a tap on her talks
        /// to her.</summary>
        void BuildMaliDock(RectTransform parent)
        {
            maliDock = UiKit.Rect(parent, "MaliDock");
            maliDock.anchorMin = maliDock.anchorMax = new Vector2(1f, 0f);
            maliDock.pivot = new Vector2(0.5f, 0f);
            maliDock.sizeDelta = new Vector2(DockSize, DockSize);
            maliDock.anchoredPosition = new Vector2(-CornerOffset, CornerOffset + ActionDiameter * 0.5f - DockSink);
            maliDock.SetAsLastSibling();

            Image figure = UiKit.SpriteImage(maliDock, "Mali", UiKit.MaliPortrait, DockSize, Color.white);
            figure.raycastTarget = figure.sprite != null;
            maliFigure = figure.rectTransform;
            maliFigureRest = maliFigure.anchoredPosition;

            var button = figure.gameObject.AddComponent<Button>();
            button.targetGraphic = figure;
            button.transition = Selectable.Transition.None;
            var navigation = button.navigation;
            navigation.mode = Navigation.Mode.None;
            button.navigation = navigation;
            button.onClick.AddListener(() =>
            {
                UiKit.NotifyButtonClicked();
                OnMaliPressed();
            });

            maliDock.gameObject.SetActive(false);
            lastDockShown = false;
        }

        static RectTransform CreateCircle(RectTransform parent, string name, Color color, float diameter, Vector2 anchor,
                                          Vector2 anchoredPosition)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            obj.layer = parent.gameObject.layer;
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(diameter, diameter);
            rect.anchoredPosition = anchoredPosition;

            var image = obj.AddComponent<Image>();
            image.sprite = UiKit.Circle;
            image.color = color;
            image.type = Image.Type.Simple;
            return rect;
        }

        /// <summary>
        /// The joystick base is its own GameObject built by BuildUi() before this component can be attached with a
        /// live 'owner' reference, so drag events are forwarded here.
        /// </summary>
        class JoystickDragForwarder : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public MobileControlsUI owner;

            public void OnPointerDown(PointerEventData eventData) => owner.OnPointerDown(eventData);
            public void OnDrag(PointerEventData eventData) => owner.OnDrag(eventData);
            public void OnPointerUp(PointerEventData eventData) => owner.OnPointerUp(eventData);
        }
    }
}
