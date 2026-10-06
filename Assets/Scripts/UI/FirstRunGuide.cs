using MaliGo.Characters;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using MaliGo.Settings;
using MaliGo.UI.Kit;
using MaliGo.World;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// First-run coach marks (DESIGN_SPEC §4.7, §5.4.11, §7.10; sort 80). Four marks, in order, each shown once:
    /// 1 joystick (when Mali's morning line hides, or 2.0 s after the world loads with no line showing; ends after
    /// moving 1 world unit or Got it), 2 world prompt (first time a prompt is visible; ends on Got it or a prompt
    /// tap), 3 HUD cash (after the first non-bill <c>MoneyChanged</c>; Got it), 4 Home prompt (Home prompt visible
    /// and today has an event or a played scenario; Got it, or the prompt is tapped = the first
    /// <c>UiModal.Changed</c> with a modal open while the mark is up).
    ///
    /// Not modal, but hidden (alpha 0, not blocking) whenever <c>UiModal.IsAnyOpen</c>, re-evaluated on
    /// <c>UiModal.Changed</c>; a hidden mark comes back when the modal closes unless its end condition was met.
    /// Scrim 55% as four rects around one target rect (+24 u) with a 6 u Coin ring; the hole passes input through.
    /// Bubble 900 u, Warm, radius 36: Mali 120 portrait, Body 40 line, Got it / Skip tips. Each shown mark sets its
    /// bit in <c>maligo.firstRunSteps</c>; mark 4 done or Skip tips sets <c>maligo.firstRunDone = 1</c>.
    /// Created by the bootstrap only when <c>MaliGoFeatures.FirstRunGuide &amp;&amp; !GameSettings.FirstRunDone</c>.
    /// </summary>
    public class FirstRunGuide : MonoBehaviour
    {
        public const string AnchorJoystick = "controls.joystick";
        public const string AnchorPrompt = "world.prompt";
        public const string AnchorCash = "hud.cash";
        public const string AnchorHomePrompt = "world.prompt.home";

        public const string Line1 = "Drag here to walk around the neighbourhood.";
        public const string Line2 = "When something's nearby, its name shows here. Tap it to see what's going on.";
        public const string Line3 = "This is your cash. Savings is next to it. Every choice shows up here straight away.";
        public const string Line4 = "Sleeping at home ends the day. Then you'll see where your money went.";

        const float NoLineDelay = 2.0f;
        const float MoveToEnd = 1.0f;
        const float HolePad = 24f;
        const float Ring = 6f;
        const float BubbleWidth = 900f;
        const float BubblePad = 30f;
        const float Portrait = 120f;
        const float TextHeight = 156f;   // 3 lines of Body 40
        const float ButtonWidth = 240f;
        const float BubbleGap = 24f;

        static FirstRunGuide instance;

        Canvas canvas;
        RectTransform canvasRect;
        RectTransform safeRoot;
        CanvasGroup group;
        readonly Image[] scrim = new Image[4];
        readonly Image[] ring = new Image[4];
        RectTransform bubble;
        Text lineText;

        int step;            // 1..4, the mark being waited for or shown; 0 = finished
        bool active;         // the current step's mark has been triggered (shown or hidden by a modal)
        bool stepEnded;
        float elapsed;
        bool dialogueHidden;
        bool moneyPending;
        Vector3 moveOrigin;
        Transform player;
        MaliDialogueController dialogue;
        InteractionArbiter arbiter;
        bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>The guide of this scene (created once per scene). Does nothing visible once the first run is done.</summary>
        public static FirstRunGuide Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            FirstRunGuide existing = FindFirstObjectByType<FirstRunGuide>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return new GameObject("FirstRunGuide").AddComponent<FirstRunGuide>();
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
            SetVisible(false);
            step = FirstUnshownStep();
            if (step == 0 || GameSettings.FirstRunDone)
            {
                Finish();
                return;
            }

            GameEvents.MoneyChanged += OnMoneyChanged;
            UiModal.Changed += OnModalChanged;
            subscribed = true;
        }

        void OnDestroy()
        {
            if (subscribed)
            {
                GameEvents.MoneyChanged -= OnMoneyChanged;
                UiModal.Changed -= OnModalChanged;
                subscribed = false;
            }

            if (dialogue != null)
            {
                dialogue.OnDialogueHidden -= OnDialogueHidden;
            }

            if (arbiter != null)
            {
                arbiter.Interacted -= OnInteracted;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        static int FirstUnshownStep()
        {
            for (int s = 1; s <= GameSettings.FirstRunStepCount; s++)
            {
                if (!GameSettings.HasFirstRunStep(s))
                {
                    return s;
                }
            }

            return 0;
        }

        // ================================================================ events

        void OnDialogueHidden()
        {
            dialogueHidden = true;
        }

        void OnMoneyChanged(MoneyEvent e)
        {
            if (e == null || (e.sourceId != null && e.sourceId.StartsWith("bill:", System.StringComparison.Ordinal)))
            {
                return;
            }

            if (step == 3 && !active)
            {
                moneyPending = true;
            }
        }

        void OnInteracted(IInteractable interactable)
        {
            if (active && step == 2)
            {
                EndStep();
            }
        }

        void OnModalChanged()
        {
            if (active && step == 4 && UiModal.IsAnyOpen && group != null && group.alpha > 0f)
            {
                EndStep();
                return;
            }

            Refresh();
        }

        // ================================================================ frame

        void Update()
        {
            if (step == 0)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            HookSceneObjects();

            if (!active)
            {
                if (!UiModal.IsAnyOpen && ShouldTrigger())
                {
                    Activate();
                }

                return;
            }

            if (step == 1 && player != null && !UiModal.IsAnyOpen)
            {
                Vector3 delta = player.position - moveOrigin;
                delta.y = 0f;
                if (delta.magnitude >= MoveToEnd)
                {
                    EndStep();
                    return;
                }
            }

            Refresh();
        }

        void HookSceneObjects()
        {
            if (dialogue == null)
            {
                dialogue = FindFirstObjectByType<MaliDialogueController>();
                if (dialogue != null)
                {
                    dialogue.OnDialogueHidden += OnDialogueHidden;
                }
            }

            if (arbiter == null && InteractionArbiter.Instance != null)
            {
                arbiter = InteractionArbiter.Instance;
                arbiter.Interacted += OnInteracted;
            }

            if (player == null)
            {
                MaliGoPlayerController controller = FindFirstObjectByType<MaliGoPlayerController>();
                if (controller != null)
                {
                    player = controller.transform;
                }
            }
        }

        bool ShouldTrigger()
        {
            switch (step)
            {
                case 1:
                {
                    if (!UiAnchors.TryGet(AnchorJoystick, out _))
                    {
                        return false;
                    }

                    bool lineShowing = dialogue != null && dialogue.IsShowingDialogue;
                    return (dialogueHidden && !lineShowing) || (elapsed >= NoLineDelay && !lineShowing);
                }
                case 2:
                    return UiAnchors.TryGet(AnchorPrompt, out _);
                case 3:
                    return moneyPending && UiAnchors.TryGet(AnchorCash, out _);
                case 4:
                    return UiAnchors.TryGet(AnchorHomePrompt, out _) && TodayHasActivity();
                default:
                    return false;
            }
        }

        static bool TodayHasActivity()
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data == null)
            {
                return false;
            }

            if (data.today != null && data.today.events != null && data.today.events.Length > 0)
            {
                return true;
            }

            if (data.chapter != null && data.chapter.choices != null)
            {
                foreach (ChoiceRecord choice in data.chapter.choices)
                {
                    if (choice != null && choice.day == data.currentDay)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        void Activate()
        {
            active = true;
            stepEnded = false;
            GameSettings.MarkFirstRunStep(step);
            if (player != null)
            {
                moveOrigin = player.position;
            }

            lineText.text = LineFor(step);
            Refresh();
        }

        static string LineFor(int s)
        {
            switch (s)
            {
                case 1: return Line1;
                case 2: return Line2;
                case 3: return Line3;
                default: return Line4;
            }
        }

        static string AnchorFor(int s)
        {
            switch (s)
            {
                case 1: return AnchorJoystick;
                case 2: return AnchorPrompt;
                case 3: return AnchorCash;
                default: return AnchorHomePrompt;
            }
        }

        void GotIt()
        {
            if (active)
            {
                EndStep();
            }
        }

        void SkipTips()
        {
            GameSettings.FirstRunDone = true;
            Finish();
        }

        void EndStep()
        {
            if (!active || stepEnded)
            {
                return;
            }

            stepEnded = true;
            active = false;
            SetVisible(false);
            if (step >= GameSettings.FirstRunStepCount)
            {
                GameSettings.FirstRunDone = true;
                Finish();
                return;
            }

            step++;
            while (step <= GameSettings.FirstRunStepCount && GameSettings.HasFirstRunStep(step))
            {
                step++;
            }

            if (step > GameSettings.FirstRunStepCount)
            {
                GameSettings.FirstRunDone = true;
                Finish();
            }
        }

        void Finish()
        {
            step = 0;
            active = false;
            SetVisible(false);
            enabled = false;
            Destroy(gameObject);
        }

        // ================================================================ drawing

        /// <summary>Shows the active mark over its target, or hides it while a modal is open or the target is gone.</summary>
        void Refresh()
        {
            if (group == null)
            {
                return;
            }

            if (!active || UiModal.IsAnyOpen || !UiAnchors.TryGet(AnchorFor(step), out RectTransform target)
                || !target.gameObject.activeInHierarchy)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            Rect hole = LocalRect(target);
            hole.xMin -= HolePad;
            hole.yMin -= HolePad;
            hole.xMax += HolePad;
            hole.yMax += HolePad;
            LayoutScrim(hole);
            LayoutBubble(hole);
        }

        void SetVisible(bool visible)
        {
            if (group == null)
            {
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        /// <summary><paramref name="target"/>'s rect in this canvas's local space (both are overlay canvases, so
        /// world corners are screen pixels).</summary>
        Rect LocalRect(RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, corner, null, out Vector2 local);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        void LayoutScrim(Rect hole)
        {
            Rect full = canvasRect.rect;
            float left = Mathf.Clamp(hole.xMin, full.xMin, full.xMax);
            float right = Mathf.Clamp(hole.xMax, full.xMin, full.xMax);
            float bottom = Mathf.Clamp(hole.yMin, full.yMin, full.yMax);
            float top = Mathf.Clamp(hole.yMax, full.yMin, full.yMax);

            Place(scrim[0], Rect.MinMaxRect(full.xMin, top, full.xMax, full.yMax));        // above
            Place(scrim[1], Rect.MinMaxRect(full.xMin, full.yMin, full.xMax, bottom));     // below
            Place(scrim[2], Rect.MinMaxRect(full.xMin, bottom, left, top));                // left
            Place(scrim[3], Rect.MinMaxRect(right, bottom, full.xMax, top));               // right

            Place(ring[0], Rect.MinMaxRect(left - Ring, top, right + Ring, top + Ring));
            Place(ring[1], Rect.MinMaxRect(left - Ring, bottom - Ring, right + Ring, bottom));
            Place(ring[2], Rect.MinMaxRect(left - Ring, bottom, left, top));
            Place(ring[3], Rect.MinMaxRect(right, bottom, right + Ring, top));
        }

        static void Place(Image image, Rect r)
        {
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(r.xMin, r.yMin);
            rect.sizeDelta = new Vector2(Mathf.Max(0f, r.width), Mathf.Max(0f, r.height));
        }

        void LayoutBubble(Rect hole)
        {
            Vector2 size = bubble.sizeDelta;
            Rect safe = LocalRect(safeRoot);
            bool targetLow = hole.center.y < canvasRect.rect.center.y;
            float y = targetLow ? hole.yMax + Ring + BubbleGap : hole.yMin - Ring - BubbleGap - size.y;
            float x = hole.center.x - size.x * 0.5f;
            x = Mathf.Clamp(x, safe.xMin + UiTheme.ScreenMargin, Mathf.Max(safe.xMin + UiTheme.ScreenMargin, safe.xMax - UiTheme.ScreenMargin - size.x));
            y = Mathf.Clamp(y, safe.yMin + UiTheme.ScreenMargin, Mathf.Max(safe.yMin + UiTheme.ScreenMargin, safe.yMax - UiTheme.ScreenMargin - size.y));
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = Vector2.zero;
            bubble.anchoredPosition = new Vector2(x, y);
        }

        void BuildUi()
        {
            canvas = UiCanvasFactory.Create("FirstRun_Canvas", UiTheme.Sort.CoachMarks, transform, out safeRoot);
            canvasRect = (RectTransform)canvas.transform;
            group = canvas.gameObject.AddComponent<CanvasGroup>();

            for (int i = 0; i < 4; i++)
            {
                scrim[i] = NewRect("Scrim " + i, UiTheme.Scrim);
                scrim[i].raycastTarget = true;
            }

            for (int i = 0; i < 4; i++)
            {
                ring[i] = NewRect("Ring " + i, UiTheme.Coin);
                ring[i].raycastTarget = false;
            }

            float textX = BubblePad + Portrait + UiTheme.Space20;
            float height = BubblePad + TextHeight + UiTheme.Space10 + UiTheme.TargetMin + UiTheme.Space10;
            Image bubbleImage = UiKit.Panel(canvasRect, "Bubble", UiTheme.Warm, UiTheme.RadiusCard, true);
            bubbleImage.raycastTarget = true;
            bubble = bubbleImage.rectTransform;
            bubble.anchorMin = bubble.anchorMax = new Vector2(0.5f, 0.5f);
            bubble.pivot = Vector2.zero;
            bubble.sizeDelta = new Vector2(BubbleWidth, height);

            Image portrait = UiKit.SpriteImage(bubble, "Mali", UiKit.MaliPortrait, Portrait, Color.white);
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = portraitRect.anchorMax = portraitRect.pivot = new Vector2(0f, 1f);
            portraitRect.anchoredPosition = new Vector2(BubblePad, -BubblePad);

            lineText = UiKit.Label(bubble, "Line", "", UiTheme.Body, UiTheme.TextPrimary);
            RectTransform lineRect = lineText.rectTransform;
            lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot = new Vector2(0f, 1f);
            lineRect.sizeDelta = new Vector2(BubbleWidth - textX - BubblePad, TextHeight);
            lineRect.anchoredPosition = new Vector2(textX, -BubblePad);

            Button gotIt = UiKit.PrimaryButton(bubble, "Got it", GotIt, ButtonWidth);
            var gotRect = (RectTransform)gotIt.transform;
            gotRect.anchorMin = gotRect.anchorMax = gotRect.pivot = new Vector2(1f, 0f);
            gotRect.anchoredPosition = new Vector2(-BubblePad, UiTheme.Space10);

            Button skip = UiKit.TextButton(bubble, "Skip tips", SkipTips, ButtonWidth);
            var skipRect = (RectTransform)skip.transform;
            skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = new Vector2(1f, 0f);
            skipRect.anchoredPosition = new Vector2(-BubblePad - ButtonWidth - UiTheme.Space20, UiTheme.Space10);
        }

        Image NewRect(string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            var image = go.AddComponent<Image>();
            image.sprite = UiKit.White;
            image.color = color;
            return image;
        }
    }
}
