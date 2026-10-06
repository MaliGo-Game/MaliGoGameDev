using MaliGo.UI.Kit;
using MaliGo.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// On-screen joystick (bottom-left) and action button (bottom-right) for touch (DESIGN_SPEC §5.4.2, sort 25).
    /// The joystick feeds <c>MaliGoPlayerController.SetVirtualJoystickInput</c>; the action button asks the
    /// <see cref="InteractionArbiter"/> to interact and shows the current interactable's verb ("Open", "Look",
    /// "Work", "Talk"), dimmed to 40% with no label when there is nothing to use. Both hide (alpha 0, not
    /// interactable) while any modal is open (§7.2). The joystick is reset when hidden, on pause, on focus loss and
    /// on disable. Anchors: <c>controls.joystick</c>, <c>controls.act</c>.
    /// </summary>
    public class MobileControlsUI : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public const string AnchorJoystick = "controls.joystick";
        public const string AnchorAct = "controls.act";

        const float BaseDiameter = 240f;
        const float HandleDiameter = 104f;
        const float ActionDiameter = 168f;
        const float CornerOffset = 190f;

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
        int activeJoystickPointerId = int.MinValue;
        bool hidden;
        string lastVerb;

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
            }
        }

        void OnDisable()
        {
            ResetJoystick();
            UiAnchors.Unregister(AnchorJoystick, joystickBase);
            UiAnchors.Unregister(AnchorAct, actionRect);
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

        void RefreshActionButton()
        {
            InteractionArbiter arbiter = InteractionArbiter.Instance;
            IInteractable current = arbiter != null ? arbiter.Current : null;
            if (current != null && (current as Object) == null)
            {
                current = null;
            }

            string verb = current != null ? current.ActionVerb ?? "" : "";
            if (verb == lastVerb)
            {
                return;
            }

            lastVerb = verb;
            actionLabel.text = verb;
            actionLabel.gameObject.SetActive(current != null);
            actionGroup.alpha = current != null ? 1f : 0.4f;
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

            MobileInputBridge.RequestInteract();
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
            actionRect = CreateCircle(parent, "ActionButton", ButtonColor, ActionDiameter, new Vector2(1f, 0f),
                new Vector2(-CornerOffset, CornerOffset));
            actionGroup = actionRect.gameObject.AddComponent<CanvasGroup>();

            var button = actionRect.gameObject.AddComponent<Button>();
            button.targetGraphic = actionRect.GetComponent<Image>();
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
            lastVerb = "";
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
