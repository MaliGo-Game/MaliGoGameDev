using System;
using System.Text;
using MaliGo.Core;
using MaliGo.Settings;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaliGo.Characters
{
    /// <summary>
    /// Mali's two dialogue boxes on one canvas (sort 40, design spec 5.4.3 and 5.4.4), built in code from the UI kit.
    ///
    /// Blocking box: scrim 25 %, portrait rising above the box, gold name tag, Dialogue 46 text paginated at
    /// 3 lines (UiTextLayout.Paginate), an optional chip row in the tag row, a typewriter that lays the page out
    /// first and hides the tail with a transparent colour tag (so words never jump lines), a full-screen tap
    /// catcher (first tap completes the page, the next advances or closes; 150 ms lockout after every page
    /// change) and Android back = tap. It is a UiModal while visible.
    ///
    /// Compact box: Body 40, at most 2 lines, no scrim, never blocks input; a tap on it dismisses it and it hides
    /// itself after typing ends + clamp(1.5 s + 0.05 s x characters, 3 s, 8 s). Not a modal.
    ///
    /// Docked to Mali: while the HUD shows Mali docked above the Talk button (the <c>controls.mali</c> anchor,
    /// <c>MobileControlsUI</c>), both boxes speak from her. They sit to the left of the dock with a speech tail on
    /// their right edge pointing at her, and drop their own portrait (the docked figure is the speaker). The
    /// blocking box hides the controls (it is a modal), so it draws a still copy of Mali exactly where the dock is.
    /// The compact box is raised beside her, clear of the joystick and the Talk button. Without the anchor
    /// (no controls in the scene) the boxes keep their centred layout with the portrait inside.
    ///
    /// The controller decides what is shown when (MaliDialogueController); this view only draws one line at a
    /// time and calls back when it has been closed. All timing is unscaled.
    /// </summary>
    public class MaliDialogueView : MonoBehaviour
    {
        public const string AnchorId = "mali.dialogue";

        // Blocking box (5.4.3).
        const float BlockingMaxWidth = 1640f;
        const float BlockingSideMargin = 96f;
        const float BlockingHeight = 300f;
        const float BoxBottom = 40f;
        const float BlockingPortrait = 280f;
        const float BlockingPortraitLeft = 24f;
        const float BlockingPortraitBottom = 92f;
        const float BlockingTextLeft = 330f;
        const float BlockingTextRight = 120f;
        const float BlockingTextTop = 84f;
        const float BlockingTextBottom = 24f;
        const float TagHeight = 60f;
        const float TagX = 330f;
        const float TagY = -24f;
        const float TagPadding = 24f;
        const float GoldEdge = 6f;
        const float ChipHeight = 56f;
        const float ChipGap = 12f;
        const float ChipPadding = 20f;
        const float CueSize = 48f;
        const float CueRight = 48f;
        const float CueBottom = 36f;
        const int BlockingMaxLines = 3;

        // Compact box (5.4.4).
        const float CompactMinWidth = 1000f;
        const float CompactMaxWidth = 1640f;
        const float CompactSideSpace = 720f;
        const float CompactHeight = 220f;
        const float CompactPortrait = 160f;
        const float CompactPortraitLeft = 24f;
        const float CompactTextLeft = 208f;
        const float CompactTextRight = 32f;
        const float CompactTagX = 208f;
        const float CompactTagY = -20f;
        const float CompactTagHeight = 56f;
        const float CompactTextTop = 88f;
        const float CompactTextBottom = 16f;
        const int CompactMaxLines = 2;

        // Docked to Mali in the HUD.
        const string DockAnchorId = "controls.mali";
        const float DockedSideMargin = 48f;
        const float DockedBlockingMinWidth = 900f;
        const float DockedCompactMinWidth = 760f;
        const float DockedCompactLeftReserve = 360f; // the joystick's corner
        const float DockedTextLeft = 48f;
        const float DockedCompactTextLeft = 32f;
        const float DockClearance = 24f;   // keeps the box clear of the Talk button's round end under Mali
        const float TailSize = 44f;
        const float TailGap = 30f;          // ~ how far the tail pokes out of the box
        const float TailInset = 44f;        // the tail stays this far from the box's top and bottom corners
        const float CompactLift = 0.6f;     // fraction of the compact box below Mali's centre

        const float PageLockout = 0.15f;
        const string HiddenOpen = "<color=#00000000>";
        const string HiddenClose = "</color>";

        enum Mode
        {
            None,
            Blocking,
            Passing
        }

        Canvas canvas;
        RectTransform safeRoot;

        Image scrim;
        GameObject blockingRoot;
        RectTransform blockingBox;
        CanvasGroup blockingGroup;
        Text blockingText;
        RectTransform blockingTag;
        RectTransform chipRow;
        RectTransform cueRect;
        Image cueImage;
        Vector2 cueRest;

        GameObject compactRoot;
        RectTransform compactBox;
        CanvasGroup compactGroup;
        Text compactText;

        Image blockingPortrait;
        RectTransform blockingTextArea;
        RectTransform blockingTail;
        Image compactPortrait;
        RectTransform compactTextArea;
        RectTransform compactTag;
        RectTransform compactTail;
        RectTransform speaker;

        static readonly Vector3[] DockCorners = new Vector3[4];

        bool docked;
        bool dockLayoutApplied;
        bool lastAppliedDocked;
        Rect dockRect;

        Mode mode = Mode.None;
        string[] pages = Array.Empty<string>();
        int pageIndex;
        string page = "";
        int revealed;
        bool pageComplete;
        float nextCharAt;
        float lockoutUntil;
        float autoHideAt;
        float lastSafeWidth = -1f;
        Action onClosed;
        bool modalPushed;
        bool anchorRegistered;

        /// <summary>True while either box shows a line.</summary>
        public bool IsVisible => mode != Mode.None;

        /// <summary>True while the blocking box shows a line.</summary>
        public bool IsBlockingVisible => mode == Mode.Blocking;

        void Awake()
        {
            BuildUiIfNeeded();
            HideImmediate();
        }

        void OnDisable()
        {
            PopModal();
            UnregisterAnchor();
        }

        void OnDestroy()
        {
            PopModal();
            UnregisterAnchor();
            if (cueRect != null)
            {
                UiTween.Stop(cueRect);
            }
        }

        // ================================================================ public API

        /// <summary>Kept for existing callers: shows the line in the compact box (or the blocking box if it needs
        /// more than 2 lines), with no close callback.</summary>
        public void Show(string line)
        {
            if (FitsPassing(line))
            {
                ShowPassing(line, null);
            }
            else
            {
                ShowBlocking(line, null, null);
            }
        }

        /// <summary>True when the text fits the compact box's 2 lines at its current width.</summary>
        public bool FitsPassing(string text)
        {
            BuildUiIfNeeded();
            Relayout(true);
            return UiTextLayout.CountLines(compactText, text ?? "", CompactTextWidth()) <= CompactMaxLines;
        }

        /// <summary>Shows a line in the blocking box. <paramref name="closed"/> runs once the player closes the last
        /// page (or the line is hidden).</summary>
        public void ShowBlocking(string text, string[] chips, Action closed)
        {
            BuildUiIfNeeded();
            ResetCurrent();
            mode = Mode.Blocking;
            onClosed = closed;

            Relayout(true);
            scrim.gameObject.SetActive(true);
            blockingRoot.SetActive(true);
            compactRoot.SetActive(false);
            BuildChips(chips);

            pages = UiTextLayout.Paginate(blockingText, text ?? "", BlockingTextWidth(), BlockingMaxLines);
            pageIndex = 0;
            StartPage();

            blockingGroup.alpha = 0f;
            UiTween.Fade(blockingGroup, 1f, UiTheme.Motion.Fade);

            if (!modalPushed)
            {
                UiModal.Push(this, Tap);
                modalPushed = true;
            }

            RegisterAnchor(blockingBox);
        }

        /// <summary>Shows a line in the compact box (it must fit 2 lines; see <see cref="FitsPassing"/>).</summary>
        public void ShowPassing(string text, Action closed)
        {
            BuildUiIfNeeded();
            ResetCurrent();
            mode = Mode.Passing;
            onClosed = closed;

            Relayout(true);
            scrim.gameObject.SetActive(false);
            blockingRoot.SetActive(false);
            compactRoot.SetActive(true);

            pages = new[] { UiTextLayout.Wrap(compactText, text ?? "", CompactTextWidth()) };
            pageIndex = 0;
            StartPage();

            compactGroup.alpha = 0f;
            UiTween.Fade(compactGroup, 1f, UiTheme.Motion.Fade);
            RegisterAnchor(compactBox);
        }

        /// <summary>Hides whatever is showing and runs its close callback.</summary>
        public void Hide()
        {
            Close();
        }

        /// <summary>Hides both boxes at once without running any callback.</summary>
        public void HideImmediate()
        {
            onClosed = null;
            ResetCurrent();
            mode = Mode.None;
            if (scrim != null)
            {
                scrim.gameObject.SetActive(false);
            }

            if (blockingRoot != null)
            {
                blockingRoot.SetActive(false);
            }

            if (compactRoot != null)
            {
                compactRoot.SetActive(false);
            }

            PopModal();
            UnregisterAnchor();
        }

        /// <summary>
        /// A tap on the blocking box (or Android back): completes the page if it is still typing, else goes to
        /// the next page, else closes. On the compact box a tap dismisses it.
        /// </summary>
        public void Tap()
        {
            if (mode == Mode.Passing)
            {
                Close();
                return;
            }

            if (mode != Mode.Blocking || Time.unscaledTime < lockoutUntil)
            {
                return;
            }

            if (!pageComplete)
            {
                CompletePage();
                return;
            }

            if (pageIndex + 1 < pages.Length)
            {
                pageIndex++;
                StartPage();
                return;
            }

            Close();
        }

        // ================================================================ typing

        void Update()
        {
            if (mode == Mode.None)
            {
                return;
            }

            Relayout(false);
            float now = Time.unscaledTime;

            if (!pageComplete)
            {
                int cps = MaliGoFeatures.Typewriter ? GameSettings.CharsPerSecond : 0;
                if (cps <= 0)
                {
                    CompletePage();
                }
                else
                {
                    bool changed = false;
                    while (revealed < page.Length && now >= nextCharAt)
                    {
                        char c = page[revealed];
                        revealed++;
                        changed = true;
                        float delay = 1f / cps;
                        if (IsSentenceEnd(page, revealed - 1))
                        {
                            delay += GameSettings.PauseAfterSentence;
                        }
                        else if (c == ',')
                        {
                            delay += GameSettings.PauseAfterComma;
                        }

                        nextCharAt += delay;
                    }

                    if (revealed >= page.Length)
                    {
                        CompletePage();
                    }
                    else if (changed)
                    {
                        ApplyRevealed();
                    }
                }
            }

            if (mode == Mode.Passing && pageComplete && now >= autoHideAt)
            {
                Close();
            }
        }

        static bool IsSentenceEnd(string text, int index)
        {
            char c = text[index];
            if (c != '.' && c != '!' && c != '?')
            {
                return false;
            }

            return index + 1 >= text.Length || text[index + 1] == ' ' || text[index + 1] == '\n'
                   || text[index + 1] == '"' || text[index + 1] == '”';
        }

        void StartPage()
        {
            page = pages.Length > 0 ? pages[Mathf.Clamp(pageIndex, 0, pages.Length - 1)] ?? "" : "";
            revealed = 0;
            pageComplete = false;
            nextCharAt = Time.unscaledTime;
            lockoutUntil = Time.unscaledTime + PageLockout;
            SetCueVisible(false);
            ApplyRevealed();
        }

        void CompletePage()
        {
            revealed = page.Length;
            pageComplete = true;
            ApplyRevealed();

            if (mode == Mode.Blocking)
            {
                bool last = pageIndex + 1 >= pages.Length;
                cueImage.sprite = UiKit.Icon(last ? "checkmark" : "down");
                cueImage.color = cueImage.sprite != null ? UiTheme.AccentPrimary : Color.clear;
                SetCueVisible(true);
            }
            else if (mode == Mode.Passing)
            {
                int characters = page.Replace("\n", " ").Length;
                autoHideAt = Time.unscaledTime + Mathf.Clamp(1.5f + 0.05f * characters, 3f, 8f);
            }
        }

        void ApplyRevealed()
        {
            Text target = mode == Mode.Blocking ? blockingText : compactText;
            if (target == null)
            {
                return;
            }

            if (revealed >= page.Length)
            {
                target.text = page;
                return;
            }

            // Substring + concat, not StringBuilder.Append(string, int, int), which hung the IL2CPP Android build
            // (see MoneyFormat.Group).
            target.text = string.Concat(page.Substring(0, revealed), HiddenOpen, page.Substring(revealed), HiddenClose);
        }

        void SetCueVisible(bool visible)
        {
            if (cueRect == null)
            {
                return;
            }

            UiTween.Stop(cueRect);
            cueRect.anchoredPosition = cueRest;
            cueRect.gameObject.SetActive(visible);
            if (visible)
            {
                UiTween.Bob(cueRect);
            }
        }

        void Close()
        {
            if (mode == Mode.None)
            {
                return;
            }

            Action closed = onClosed;
            HideImmediate();
            closed?.Invoke();
        }

        void ResetCurrent()
        {
            pages = Array.Empty<string>();
            page = "";
            revealed = 0;
            pageComplete = false;
            if (cueRect != null)
            {
                SetCueVisible(false);
            }
        }

        // ================================================================ modal and anchor

        void PopModal()
        {
            if (modalPushed)
            {
                modalPushed = false;
                UiModal.Pop(this);
            }
        }

        void RegisterAnchor(RectTransform rect)
        {
            UnregisterAnchor();
            UiAnchors.Register(AnchorId, rect);
            anchorRegistered = true;
        }

        void UnregisterAnchor()
        {
            if (!anchorRegistered)
            {
                return;
            }

            anchorRegistered = false;
            if (blockingBox != null)
            {
                UiAnchors.Unregister(AnchorId, blockingBox);
            }

            if (compactBox != null)
            {
                UiAnchors.Unregister(AnchorId, compactBox);
            }
        }

        // ================================================================ layout

        float SafeWidth()
        {
            float width = safeRoot != null ? safeRoot.rect.width : 0f;
            return width > 1f ? width : UiTheme.ReferenceWidth;
        }

        float SafeHeight()
        {
            float height = safeRoot != null ? safeRoot.rect.height : 0f;
            return height > 1f ? height : UiTheme.ReferenceHeight;
        }

        /// <summary>Right edge of a docked box, in safe-area units from the centre (the tail pokes out past it).</summary>
        float DockedRight() => dockRect.xMin - DockClearance - TailGap;

        /// <summary>Mali's centre height above the safe area's bottom edge.</summary>
        float DockCentreFromBottom() => dockRect.center.y + SafeHeight() * 0.5f;

        float BlockingWidth()
        {
            float centred = Mathf.Min(BlockingMaxWidth, SafeWidth() - BlockingSideMargin);
            if (!docked)
            {
                return centred;
            }

            float available = DockedRight() - (-SafeWidth() * 0.5f + DockedSideMargin);
            return Mathf.Clamp(available, Mathf.Min(DockedBlockingMinWidth, centred), centred);
        }

        float CompactWidth()
        {
            float centred = Mathf.Clamp(SafeWidth() - CompactSideSpace, CompactMinWidth, CompactMaxWidth);
            if (!docked)
            {
                return centred;
            }

            float available = DockedRight() - (-SafeWidth() * 0.5f + DockedCompactLeftReserve);
            return Mathf.Clamp(available, DockedCompactMinWidth, CompactMaxWidth);
        }

        float BlockingTextWidth() => BlockingWidth() - (docked ? DockedTextLeft : BlockingTextLeft) - BlockingTextRight;

        float CompactTextWidth() => CompactWidth() - (docked ? DockedCompactTextLeft : CompactTextLeft) - CompactTextRight;

        void Relayout(bool force)
        {
            float width = SafeWidth();
            bool wasDocked = docked;
            Rect lastDock = dockRect;
            docked = TryGetDock(out dockRect);
            bool dockMoved = docked != wasDocked
                             || (docked && (Vector2.SqrMagnitude(dockRect.min - lastDock.min) > 0.25f
                                            || Vector2.SqrMagnitude(dockRect.size - lastDock.size) > 0.25f));
            if (!force && !dockMoved && Mathf.Abs(width - lastSafeWidth) < 0.5f)
            {
                return;
            }

            lastSafeWidth = width;
            ApplyDockLayout();

            if (blockingBox != null)
            {
                float boxWidth = BlockingWidth();
                blockingBox.sizeDelta = new Vector2(boxWidth, BlockingHeight);
                blockingBox.anchoredPosition = new Vector2(docked ? DockedRight() - boxWidth * 0.5f : 0f, BoxBottom);
                PlaceTail(blockingTail, BoxBottom, BlockingHeight);
                if (docked)
                {
                    speaker.anchoredPosition = dockRect.center;
                    speaker.sizeDelta = dockRect.size;
                }
            }

            if (compactBox != null)
            {
                float boxWidth = CompactWidth();
                float bottom = docked ? Mathf.Max(BoxBottom, DockCentreFromBottom() - CompactHeight * CompactLift) : BoxBottom;
                compactBox.sizeDelta = new Vector2(boxWidth, CompactHeight);
                compactBox.anchoredPosition = new Vector2(docked ? DockedRight() - boxWidth * 0.5f : 0f, bottom);
                PlaceTail(compactTail, bottom, CompactHeight);
            }
        }

        /// <summary>The docked Mali's rect in safe-area units (centre origin), when the HUD shows her.</summary>
        bool TryGetDock(out Rect rect)
        {
            rect = default;
            if (safeRoot == null || !UiAnchors.TryGet(DockAnchorId, out RectTransform dock) || !dock.gameObject.activeInHierarchy)
            {
                return false;
            }

            // Both canvases are screen-space overlay, so world corners are screen pixels.
            Vector3[] corners = DockCorners;
            dock.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(safeRoot, corner, null, out Vector2 local))
                {
                    return false;
                }

                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return rect.width > 1f && rect.height > 1f;
        }

        /// <summary>Switches portraits, text insets, tails and the speaker copy between the docked and centred
        /// layouts (only when that changes).</summary>
        void ApplyDockLayout()
        {
            if (blockingBox == null || compactBox == null || (dockLayoutApplied && docked == lastAppliedDocked))
            {
                return;
            }

            dockLayoutApplied = true;
            lastAppliedDocked = docked;

            blockingPortrait.gameObject.SetActive(!docked);
            compactPortrait.gameObject.SetActive(!docked);
            blockingTail.gameObject.SetActive(docked);
            compactTail.gameObject.SetActive(docked);
            speaker.gameObject.SetActive(docked);

            float blockingLeft = docked ? DockedTextLeft : BlockingTextLeft;
            blockingTextArea.offsetMin = new Vector2(blockingLeft, BlockingTextBottom);
            blockingTag.anchoredPosition = new Vector2(blockingLeft, TagY);
            chipRow.anchoredPosition = new Vector2(blockingLeft + blockingTag.sizeDelta.x + ChipPadding, chipRow.anchoredPosition.y);

            float compactLeft = docked ? DockedCompactTextLeft : CompactTextLeft;
            compactTextArea.offsetMin = new Vector2(compactLeft, CompactTextBottom);
            compactTag.anchoredPosition = new Vector2(compactLeft, CompactTagY);
        }

        /// <summary>Puts a tail on the box's right edge at Mali's height, kept off the box's corners.</summary>
        void PlaceTail(RectTransform tail, float boxBottom, float boxHeight)
        {
            if (tail == null || !docked)
            {
                return;
            }

            float y = Mathf.Clamp(DockCentreFromBottom() - boxBottom, TailInset, boxHeight - TailInset);
            tail.anchoredPosition = new Vector2(0f, y);
        }

        float TagLeft() => docked ? DockedTextLeft : TagX;

        // ================================================================ building

        void BuildUiIfNeeded()
        {
            if (canvas != null)
            {
                return;
            }

            canvas = UiCanvasFactory.Create("MaliDialogue_Canvas", UiTheme.Sort.MaliDialogue, transform, out safeRoot);

            scrim = UiCanvasFactory.FullBleed(canvas, "Scrim", UiTheme.ScrimLight);
            scrim.raycastTarget = true;
            scrim.gameObject.AddComponent<TapCatcher>().Tapped = Tap;

            BuildBlocking();
            BuildCompact();
        }

        void BuildBlocking()
        {
            RectTransform root = UiKit.Rect(safeRoot, "Blocking");
            blockingRoot = root.gameObject;

            // The whole safe area also catches taps (the scrim covers the rest of the screen).
            var catcher = root.gameObject.AddComponent<Image>();
            catcher.sprite = UiKit.White;
            catcher.color = Color.clear;
            catcher.raycastTarget = true;
            root.gameObject.AddComponent<TapCatcher>().Tapped = Tap;

            blockingBox = UiKit.Rect(root, "Box");
            blockingBox.anchorMin = blockingBox.anchorMax = new Vector2(0.5f, 0f);
            blockingBox.pivot = new Vector2(0.5f, 0f);
            blockingBox.anchoredPosition = new Vector2(0f, BoxBottom);
            blockingBox.sizeDelta = new Vector2(BlockingMaxWidth, BlockingHeight);
            blockingGroup = blockingBox.gameObject.AddComponent<CanvasGroup>();
            blockingGroup.blocksRaycasts = false; // taps go to the catcher behind

            // Speech tail toward the docked Mali: a diamond half under the box, so only a point shows.
            blockingTail = BuildTail(blockingBox);

            // Gold top edge: the gold panel shows 6 u above the warm panel.
            Image edge = UiKit.Panel(blockingBox, "GoldEdge", UiTheme.Gold, UiTheme.RadiusSheet, UiTheme.ShadowSheet);
            edge.raycastTarget = false;
            Image fill = UiKit.Panel(blockingBox, "Fill", UiTheme.Warm, UiTheme.RadiusSheet, false);
            fill.raycastTarget = false;
            fill.rectTransform.offsetMax = new Vector2(0f, -GoldEdge);

            Image portrait = UiKit.SpriteImage(blockingBox, "Portrait", UiKit.MaliPortrait, BlockingPortrait, Color.white);
            blockingPortrait = portrait;
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 0f);
            portraitRect.pivot = new Vector2(0f, 0f);
            portraitRect.anchoredPosition = new Vector2(BlockingPortraitLeft, BlockingPortraitBottom);

            blockingTag = BuildTag(blockingBox, TagX, TagY, TagHeight);

            chipRow = UiKit.Rect(blockingBox, "Chips");
            chipRow.anchorMin = chipRow.anchorMax = new Vector2(0f, 1f);
            chipRow.pivot = new Vector2(0f, 1f);
            chipRow.anchoredPosition = new Vector2(TagX + blockingTag.sizeDelta.x + ChipPadding, TagY - (TagHeight - ChipHeight) * 0.5f);
            chipRow.sizeDelta = new Vector2(0f, ChipHeight);

            RectTransform textArea = UiKit.Rect(blockingBox, "TextArea");
            blockingTextArea = textArea;
            textArea.offsetMin = new Vector2(BlockingTextLeft, BlockingTextBottom);
            textArea.offsetMax = new Vector2(-BlockingTextRight, -BlockingTextTop);
            blockingText = UiKit.Label(textArea, "Text", "", UiTheme.Dialogue, UiTheme.TextPrimary);
            blockingText.horizontalOverflow = HorizontalWrapMode.Overflow; // pages are pre-broken

            cueImage = UiKit.IconImage(blockingBox, "Cue", "down", CueSize, UiTheme.AccentPrimary);
            cueRect = cueImage.rectTransform;
            cueRect.anchorMin = cueRect.anchorMax = new Vector2(1f, 0f);
            cueRect.pivot = new Vector2(1f, 0f);
            cueRest = new Vector2(-CueRight, CueBottom);
            cueRect.anchoredPosition = cueRest;
            cueRect.gameObject.SetActive(false);

            // The docked Mali, drawn over the scrim where the HUD dock is (the controls hide behind this modal).
            Image speakerImage = UiKit.SpriteImage(root, "Speaker", UiKit.MaliPortrait, 132f, Color.white);
            speaker = speakerImage.rectTransform;
            speaker.gameObject.SetActive(false);
        }

        void BuildCompact()
        {
            RectTransform root = UiKit.Rect(safeRoot, "Compact");
            compactRoot = root.gameObject;

            compactBox = UiKit.Rect(root, "Box");
            compactBox.anchorMin = compactBox.anchorMax = new Vector2(0.5f, 0f);
            compactBox.pivot = new Vector2(0.5f, 0f);
            compactBox.anchoredPosition = new Vector2(0f, BoxBottom);
            compactBox.sizeDelta = new Vector2(CompactMinWidth, CompactHeight);
            compactGroup = compactBox.gameObject.AddComponent<CanvasGroup>();

            compactTail = BuildTail(compactBox);

            Image fill = UiKit.Panel(compactBox, "Fill", UiTheme.Warm, UiTheme.RadiusSheet, UiTheme.ShadowCard);
            fill.raycastTarget = true; // a tap on the box dismisses it; nothing else is blocked
            fill.gameObject.AddComponent<TapCatcher>().Tapped = Tap;

            Image portrait = UiKit.SpriteImage(compactBox, "Portrait", UiKit.MaliPortrait, CompactPortrait, Color.white);
            compactPortrait = portrait;
            RectTransform portraitRect = portrait.rectTransform;
            portraitRect.anchorMin = portraitRect.anchorMax = new Vector2(0f, 0.5f);
            portraitRect.pivot = new Vector2(0f, 0.5f);
            portraitRect.anchoredPosition = new Vector2(CompactPortraitLeft, 0f);

            compactTag = BuildTag(compactBox, CompactTagX, CompactTagY, CompactTagHeight);

            RectTransform textArea = UiKit.Rect(compactBox, "TextArea");
            compactTextArea = textArea;
            textArea.offsetMin = new Vector2(CompactTextLeft, CompactTextBottom);
            textArea.offsetMax = new Vector2(-CompactTextRight, -CompactTextTop);
            compactText = UiKit.Label(textArea, "Text", "", UiTheme.Body, UiTheme.TextPrimary);
            compactText.horizontalOverflow = HorizontalWrapMode.Overflow; // pre-broken by UiTextLayout.Wrap
        }

        /// <summary>
        /// The speech tail: a Warm square turned 45 degrees, centred on the box's right edge and drawn first, so the
        /// box covers its inner half and a point shows toward the docked Mali. Hidden until docked.
        /// </summary>
        static RectTransform BuildTail(RectTransform box)
        {
            RectTransform tail = UiKit.Rect(box, "Tail");
            tail.anchorMin = tail.anchorMax = new Vector2(1f, 0f);
            tail.pivot = new Vector2(0.5f, 0.5f);
            tail.sizeDelta = new Vector2(TailSize, TailSize);
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image image = tail.gameObject.AddComponent<Image>();
            image.sprite = UiKit.White;
            image.color = UiTheme.Warm;
            image.raycastTarget = false;
            tail.SetAsFirstSibling();
            tail.gameObject.SetActive(false);
            return tail;
        }

        /// <summary>The gold "Mali" name tag: a pill with Label 36 Black ink, padding 24.</summary>
        static RectTransform BuildTag(RectTransform box, float x, float y, float height)
        {
            RectTransform tag = UiKit.Rect(box, "NameTag");
            tag.anchorMin = tag.anchorMax = new Vector2(0f, 1f);
            tag.pivot = new Vector2(0f, 1f);
            tag.anchoredPosition = new Vector2(x, y);

            Image pill = tag.gameObject.AddComponent<Image>();
            pill.color = UiTheme.Gold;
            pill.raycastTarget = false;
            UiKit.SetRadius(pill, UiTheme.RadiusPill(height));

            Text label = UiKit.Label(tag, "Label", "Mali", UiTheme.Label.WithWeight(UiFontWeight.Black), UiTheme.TextPrimary,
                                     TextAnchor.MiddleCenter);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float width = UiTextLayout.MeasureWidth(label, "Mali") + TagPadding * 2f;
            tag.sizeDelta = new Vector2(width, height);
            return tag;
        }

        /// <summary>Chip row after a choice: pills 56 tall on Sunken, Caption 32, 12 apart, right of the name tag.</summary>
        void BuildChips(string[] chips)
        {
            for (int i = chipRow.childCount - 1; i >= 0; i--)
            {
                Destroy(chipRow.GetChild(i).gameObject);
            }

            if (chips == null || chips.Length == 0)
            {
                return;
            }

            chipRow.anchoredPosition = new Vector2(TagLeft() + blockingTag.sizeDelta.x + ChipPadding, chipRow.anchoredPosition.y);
            float limit = BlockingWidth() - chipRow.anchoredPosition.x - BlockingTextRight;
            float x = 0f;
            foreach (string chip in chips)
            {
                if (string.IsNullOrEmpty(chip))
                {
                    continue;
                }

                RectTransform pill = UiKit.Rect(chipRow, "Chip");
                pill.anchorMin = pill.anchorMax = new Vector2(0f, 1f);
                pill.pivot = new Vector2(0f, 1f);
                Image background = pill.gameObject.AddComponent<Image>();
                background.color = UiTheme.Sunken;
                background.raycastTarget = false;
                UiKit.SetRadius(background, UiTheme.RadiusPill(ChipHeight));

                Text label = UiKit.Label(pill, "Label", chip, UiTheme.Caption, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                float width = UiTextLayout.MeasureWidth(label, chip) + ChipPadding * 2f;
                if (x + width > limit && x > 0f)
                {
                    Destroy(pill.gameObject);
                    break;
                }

                pill.sizeDelta = new Vector2(width, ChipHeight);
                pill.anchoredPosition = new Vector2(x, 0f);
                x += width + ChipGap;
            }
        }

        /// <summary>Forwards a click on its graphic.</summary>
        sealed class TapCatcher : MonoBehaviour, IPointerClickHandler
        {
            public Action Tapped;

            public void OnPointerClick(PointerEventData eventData)
            {
                Tapped?.Invoke();
            }
        }
    }
}
