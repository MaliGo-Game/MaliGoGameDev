using System;
using System.Collections;
using System.Collections.Generic;
using MaliGo.App;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.Scenarios;
using MaliGo.Settings;
using MaliGo.UI;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.PlayerIdentity
{
    /// <summary>
    /// Character creation (DESIGN_SPEC §1.1, §4.6, §5.4.13; sort 0): five screens, no financial questions beyond
    /// the two profile taps.
    /// 1 the promise and the name (Next disabled while the name is empty; the old-save notice when
    ///   <c>PlayerDataManager.WasResetForUpdate</c>, then the flag is cleared);
    /// 2 "Pick your look": six looks (two builds x light/medium/deep) beside (landscape) or under (portrait) a
    ///   turning <see cref="LookPreview"/>;
    /// 3 the two profile taps, then "We've built your week around where your money goes." and the first three
    ///   places from <c>ChapterSchedule.WeekPlaces</c>; Next needs a pick in both rows and runs
    ///   <c>SpendingProfiles.SetFromOnboarding</c> (skipped when <c>MaliGoFeatures.ProfileTaps</c> is off: the
    ///   default profile is kept);
    /// 4 the savings goal (<c>GoalPresets</c>);
    /// 5 Mali's three paragraphs, typed and paginated at 3 lines, with a big round continue button under the text;
    ///   Let's go sets <c>isCharacterCreated</c> and <c>hasMetMali</c>, runs <c>ChapterFlow.StartChapter(data, 1)</c>,
    ///   saves, turns the screen to landscape and loads the world.
    ///
    /// Orientation: <c>MaliGoFeatures.PortraitOnboarding</c> (the A/B switch) asks <see cref="OrientationLock"/> for
    /// portrait. The layout follows the screen's actual shape, not the switch: a 1500 x 880 sheet centred in
    /// landscape, or a sheet filling the safe area in portrait with everything in one column and a full-width
    /// button row at the bottom. When the shape changes (the portrait lock lands a few frames after start, or the
    /// Editor's Game view is resized) the sheet is rebuilt, keeping the player's entries. On the name screen the
    /// field and Next stay in the upper half in both layouts, and the sheet lifts if the soft keyboard still
    /// reaches them.
    /// </summary>
    public class CharacterCreationUI : MonoBehaviour
    {
        // Mali's paragraphs (§1.1 0:40). Kept here, not in OnboardingCopy (§4.6).
        public const string MaliParagraph1 = "Hi {name}, I'm Mali. It's the week before payday: R600 in your pocket and R400 in savings.";
        public const string MaliParagraph2 = "You're saving for your {goalName}: R{goalTarget}. The R150 shift on the main road opens once the day's first thing is sorted. It takes 60 of your 100 energy.";
        public const string MaliParagraph3 = "I won't tell you what to do. I'll show you where your money went, every night.";

        const int ScreenName = 1;
        const int ScreenLook = 2;
        const int ScreenProfile = 3;
        const int ScreenGoal = 4;
        const int ScreenMali = 5;

        // Landscape sheet.
        const float SheetWidth = 1500f;
        const float SheetHeight = 880f;
        const float Margin = 60f;
        const float TextWidth = 1380f;
        const float ButtonMargin = 40f;
        const float NavButtonWidth = 320f;
        const float LetsGoWidth = 400f;
        const float RingWidth = 6f;
        const float MaliTextWidth = 820f;
        const float MaliTextHeight = 220f;
        const int MaliLinesPerPage = 3;
        const float MaliPortraitSize = 520f;
        const float MaliNextSize = 144f;
        const float NameFieldWidth = 900f;

        // Portrait sheet: fills the safe area inside the screen margin; one column.
        const float PortraitTop = 100f;
        const float PortraitBackWidth = 280f;
        const int PortraitPromiseSize = 64;
        const float MaliPortraitSizeTall = 440f;
        const float MaliNextSizeTall = 168f;

        // Shared.
        const float NameFieldHeight = 144f;
        const float NameFieldGap = 40f;
        const float CardGap = 20f;
        const float MaliNextBob = 10f;
        const float KeyboardGap = 24f;
        const float KeyboardLiftSpeed = 3000f;
        // How long the turn to landscape (after Let's go) or to portrait (before the old-save notice) may take
        // before the flow goes on regardless; in the Editor the screen never turns.
        const float RotateTimeout = 1.5f;

        static readonly string[] SkinTones = { "light", "medium", "deep" };
        static readonly Color[] ToneSwatches =
        {
            new Color(198f / 255f, 140f / 255f, 100f / 255f),
            new Color(150f / 255f, 96f / 255f, 62f / 255f),
            new Color(96f / 255f, 60f / 255f, 40f / 255f)
        };

        readonly List<int> screens = new List<int>();
        int screenIndex;

        PlayerData draft;
        string draftName = "";
        int lookIndex;
        string focusPick;
        string travelPick;
        string goalPick;

        Canvas canvas;
        RectTransform safeRoot;
        RectTransform sheet;
        RectTransform dots;
        RectTransform content;
        Button backButton;
        Button nextButton;
        Button letsGoButton;
        InputField nameInput;
        LookPreview lookPreview;
        PlayerCharacterCatalog catalog;
        bool catalogLoaded;
        readonly List<CardView> lookCards = new List<CardView>();
        readonly List<CardView> focusCards = new List<CardView>();
        readonly List<CardView> travelCards = new List<CardView>();
        readonly List<CardView> goalCards = new List<CardView>();
        CanvasGroup summaryGroup;
        Text summaryPlaces;
        bool summaryShown;
        bool completing;
        bool noticePending;
        float noticeWaited;

        // Layout of the current sheet: its shape, its size in u, the safe area it was built for, and its rest
        // position (the keyboard lift moves it up from there).
        bool portraitLayout;
        float sheetWidth;
        float sheetHeight;
        Vector2 layoutSafeArea;
        Vector2 sheetRest;
        float keyboardLift;
        RectTransform keyboardTarget;
        readonly Vector3[] corners = new Vector3[4];

        // Screen 5 typewriter.
        readonly List<string> maliPages = new List<string>();
        int maliPage;
        int maliResumePage = -1;
        Text maliText;
        float maliTextWidth = MaliTextWidth;
        Button maliNext;
        Vector2 maliNextRest;
        float typed;
        float pauseLeft;
        bool pageDone;

        void Awake()
        {
            GameFlowController.EnsurePlayerDataManager();
            // Portrait when the A/B switch is on; usually the boot lock has already done it.
            OrientationLock.ForOnboarding();
            draft = PlayerData.CreateNew();

            screens.Add(ScreenName);
            screens.Add(ScreenLook);
            if (MaliGoFeatures.ProfileTaps)
            {
                screens.Add(ScreenProfile);
            }

            screens.Add(ScreenGoal);
            screens.Add(ScreenMali);

            BuildCanvas();
            BuildSheet();
            ShowScreen(0);
            // Android back (Escape) goes to the previous screen; no modal is ever open here.
            UiModal.BackWithNoModal += OnBack;
        }

        void Start()
        {
            // Shown from Update once the onboarding orientation has landed, so the banner is laid out for the
            // screen it stays on.
            noticePending = PlayerDataManager.WasResetForUpdate;
        }

        void OnDestroy()
        {
            UiModal.BackWithNoModal -= OnBack;
            UiTween.Stop(this);
            if (maliNext != null)
            {
                UiTween.Stop(maliNext.transform);
            }

            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
        }

        int CurrentScreen => screens[screenIndex];

        /// <summary>The sheet's inner width (TextWidth in landscape).</summary>
        float ContentWidth => sheetWidth - 2f * Margin;

        /// <summary>Portrait: the lowest y content may reach, above the bottom button row.</summary>
        float PortraitContentBottom => sheetHeight - ButtonMargin - UiTheme.TargetMin - UiTheme.Space40;

        // ================================================================ shell

        void BuildCanvas()
        {
            canvas = UiCanvasFactory.Create("CharacterCreation_Canvas", UiTheme.Sort.CharacterCreation, transform,
                out safeRoot);
            UiCanvasFactory.FullBleed(canvas, "Backdrop", UiTheme.Inverse);
        }

        /// <summary>(Re)builds the sheet, its dots and nav buttons for the screen's current shape.</summary>
        void BuildSheet()
        {
            if (sheet != null)
            {
                Destroy(sheet.gameObject);
            }

            portraitLayout = UiCanvasFactory.ScreenIsPortrait;
            layoutSafeArea = UiCanvasFactory.SafeAreaSize();

            Image sheetImage = UiKit.Panel(safeRoot, "Sheet", UiTheme.Paper, UiTheme.RadiusSheet, true);
            sheetImage.raycastTarget = true;
            sheet = sheetImage.rectTransform;
            if (portraitLayout)
            {
                // The canvas is 1080 u wide and as tall as the phone; the sheet fills its safe area.
                float gutter = UiTheme.ScreenMargin;
                sheet.anchorMin = Vector2.zero;
                sheet.anchorMax = Vector2.one;
                sheet.pivot = new Vector2(0.5f, 0.5f);
                sheet.offsetMin = new Vector2(gutter, gutter);
                sheet.offsetMax = new Vector2(-gutter, -gutter);
                sheetWidth = Mathf.Max(0f, layoutSafeArea.x - 2f * gutter);
                sheetHeight = Mathf.Max(0f, layoutSafeArea.y - 2f * gutter);
            }
            else
            {
                sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
                sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight);
                sheetWidth = SheetWidth;
                sheetHeight = SheetHeight;
            }

            sheetRest = sheet.anchoredPosition;
            keyboardLift = 0f;

            content = UiKit.Rect(sheet, "Content");
            dots = UiKit.Rect(sheet, "Dots");

            backButton = UiKit.SecondaryButton(sheet, OnboardingCopy.BackButton, OnBack,
                portraitLayout ? PortraitBackWidth : NavButtonWidth);
            nextButton = UiKit.PrimaryButton(sheet, OnboardingCopy.NextButton, OnNext, NavButtonWidth);
            letsGoButton = UiKit.PrimaryButton(sheet, OnboardingCopy.LetsGoButton, Complete, LetsGoWidth);
            letsGoButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// Landscape: Back bottom-left, Next / Let's go bottom-right. Portrait: one full-width row at the bottom,
        /// Back on the left when shown and the forward button filling the rest. The name screen then moves Next up
        /// next to its field.
        /// </summary>
        void LayoutNav()
        {
            var backRect = (RectTransform)backButton.transform;
            var nextRect = (RectTransform)nextButton.transform;
            var goRect = (RectTransform)letsGoButton.transform;
            if (portraitLayout)
            {
                PlaceCorner(backRect, Vector2.zero, new Vector2(Margin, ButtonMargin), PortraitBackWidth);
                float left = backButton.gameObject.activeSelf ? Margin + PortraitBackWidth + UiTheme.TappableGap : Margin;
                PlaceBottomRow(nextRect, left);
                PlaceBottomRow(goRect, left);
            }
            else
            {
                PlaceCorner(backRect, Vector2.zero, new Vector2(ButtonMargin, ButtonMargin), NavButtonWidth);
                PlaceCorner(nextRect, new Vector2(1f, 0f), new Vector2(-ButtonMargin, ButtonMargin), NavButtonWidth);
                PlaceCorner(goRect, new Vector2(1f, 0f), new Vector2(-ButtonMargin, ButtonMargin), LetsGoWidth);
            }
        }

        static void PlaceCorner(RectTransform rect, Vector2 corner, Vector2 position, float width)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.sizeDelta = new Vector2(width, UiTheme.TargetMin);
            rect.anchoredPosition = position;
        }

        static void PlaceBottomRow(RectTransform rect, float left)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(-(left + Margin), UiTheme.TargetMin);
            rect.anchoredPosition = new Vector2((left - Margin) * 0.5f, ButtonMargin);
        }

        void BuildDots()
        {
            for (int i = dots.childCount - 1; i >= 0; i--)
            {
                Destroy(dots.GetChild(i).gameObject);
            }

            const float size = 20f;
            const float gap = 20f;
            int count = screens.Count;
            float total = count * size + (count - 1) * gap;
            for (int i = 0; i < count; i++)
            {
                Image dot = UiKit.SpriteImage(dots, "Dot " + (i + 1), UiKit.Circle, size,
                    i == screenIndex ? UiTheme.AccentPrimary : UiTheme.BorderSubtle);
                RectTransform rect = dot.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(-total * 0.5f + i * (size + gap), -40f);
            }
        }

        void ShowScreen(int index)
        {
            screenIndex = Mathf.Clamp(index, 0, screens.Count - 1);
            ClearContent();
            BuildDots();

            int screen = CurrentScreen;
            backButton.gameObject.SetActive(screen != ScreenName);
            nextButton.gameObject.SetActive(screen != ScreenMali);
            letsGoButton.gameObject.SetActive(false);
            LayoutNav();

            switch (screen)
            {
                case ScreenName: BuildNameScreen(); break;
                case ScreenLook: BuildLookScreen(); break;
                case ScreenProfile: BuildProfileScreen(); break;
                case ScreenGoal: BuildGoalScreen(); break;
                case ScreenMali: BuildMaliScreen(); break;
            }

            UpdateNext();
        }

        /// <summary>Rebuilds the sheet for the screen's new shape, keeping the entries and Mali's page.</summary>
        void Relayout()
        {
            Capture();
            if (CurrentScreen == ScreenMali && maliPages.Count > 0)
            {
                maliResumePage = maliPage;
            }

            ClearContent();
            BuildSheet();
            ShowScreen(screenIndex);
        }

        void ClearContent()
        {
            lookCards.Clear();
            focusCards.Clear();
            travelCards.Clear();
            goalCards.Clear();
            nameInput = null;
            keyboardTarget = null;
            lookPreview = null;
            summaryGroup = null;
            summaryPlaces = null;
            summaryShown = false;
            maliText = null;
            if (maliNext != null)
            {
                UiTween.Stop(maliNext.transform);
                maliNext = null;
            }

            maliPages.Clear();

            if (sheet != null)
            {
                keyboardLift = 0f;
                sheet.anchoredPosition = sheetRest;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }
        }

        void UpdateNext()
        {
            if (nextButton == null)
            {
                return;
            }

            bool ok;
            switch (CurrentScreen)
            {
                case ScreenName: ok = CleanName(nameInput != null ? nameInput.text : draftName).Length > 0; break;
                case ScreenProfile: ok = focusPick != null && travelPick != null; break;
                case ScreenGoal: ok = goalPick != null; break;
                default: ok = true; break;
            }

            nextButton.interactable = ok;
        }

        void OnBack()
        {
            if (screenIndex == 0 || completing)
            {
                return;
            }

            Capture();
            ShowScreen(screenIndex - 1);
        }

        void OnNext()
        {
            if (completing || !nextButton.interactable)
            {
                return;
            }

            Capture();
            switch (CurrentScreen)
            {
                case ScreenName:
                    if (draftName.Length == 0)
                    {
                        return;
                    }

                    draft.characterName = draftName;
                    break;
                case ScreenLook:
                    draft.appearance = AppearanceFor(lookIndex);
                    break;
                case ScreenProfile:
                    if (focusPick == null || travelPick == null)
                    {
                        return;
                    }

                    draft.spendingProfile ??= new SpendingProfile();
                    SpendingProfiles.SetFromOnboarding(draft.spendingProfile, focusPick, travelPick,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                    break;
                case ScreenGoal:
                    if (goalPick == null)
                    {
                        return;
                    }

                    draft.goals = new[] { GoalPresets.CreateGoal(goalPick) };
                    break;
            }

            ShowScreen(screenIndex + 1);
        }

        void Capture()
        {
            if (CurrentScreen == ScreenName && nameInput != null)
            {
                draftName = CleanName(nameInput.text);
            }
        }

        static string CleanName(string raw)
        {
            string name = (raw ?? "").Trim();
            if (name.Length > OnboardingCopy.NameMaxLength)
            {
                name = name.Substring(0, OnboardingCopy.NameMaxLength).Trim();
            }

            return name;
        }

        /// <summary>Style 1-3: feminine light/medium/deep; Style 4-6: masculine light/medium/deep (§4.6).</summary>
        public static AppearanceData AppearanceFor(int lookIndex)
        {
            int i = Mathf.Clamp(lookIndex, 0, 5);
            return new AppearanceData
            {
                genderPresentation = i < 3 ? "feminine" : "masculine",
                skinTone = SkinTones[i % 3]
            };
        }

        void Complete()
        {
            if (completing || CurrentScreen != ScreenMali)
            {
                return;
            }

            completing = true;
            if (string.IsNullOrEmpty(draft.characterName))
            {
                draft.characterName = draftName;
            }

            draft.appearance ??= AppearanceFor(lookIndex);
            if (draft.goals == null || draft.goals.Length == 0)
            {
                draft.goals = new[] { GoalPresets.CreateGoal(goalPick) };
            }

            draft.isCharacterCreated = true;
            draft.hasMetMali = true;
            ChapterFlow.StartChapter(draft, 1);

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.SetPlayerData(draft, saveImmediately: true);
            }

            StartCoroutine(EnterWorld());
        }

        /// <summary>
        /// The world is landscape only. From portrait the screen is turned first, behind the plain green backdrop
        /// (the sheet is hidden so it is never seen relaid out sideways), and the world loads once the screen is
        /// landscape, so its HUD, controls and camera are built at their final size. Gives up waiting after
        /// <see cref="RotateTimeout"/>; the world's canvases follow a late turn anyway (SafeAreaFitter,
        /// CanvasOrientationScaler, and the camera's aspect follows the screen).
        /// </summary>
        IEnumerator EnterWorld()
        {
            OrientationLock.ForGame();
            if (UiCanvasFactory.ScreenIsPortrait)
            {
                if (sheet != null)
                {
                    sheet.gameObject.SetActive(false);
                }

                float waited = 0f;
                while (UiCanvasFactory.ScreenIsPortrait && waited < RotateTimeout)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                // One more frame, so Screen.safeArea has caught up with the new size.
                yield return null;
            }

            GameFlowController.LoadWorldScene();
        }

        // ================================================================ per frame

        void Update()
        {
            if (!completing && NeedsRelayout())
            {
                Relayout();
            }

            UpdateNotice();
            UpdateKeyboardLift();
            UpdateMaliTyping();
        }

        bool NeedsRelayout()
        {
            if (UiCanvasFactory.ScreenIsPortrait != portraitLayout)
            {
                return true;
            }

            // The portrait sheet is sized from the safe area, which can settle a frame after a turn.
            return portraitLayout && (UiCanvasFactory.SafeAreaSize() - layoutSafeArea).sqrMagnitude > 4f;
        }

        void UpdateNotice()
        {
            if (!noticePending)
            {
                return;
            }

            noticeWaited += Time.unscaledDeltaTime;
            bool settled = UiCanvasFactory.ScreenIsPortrait == OrientationLock.OnboardingIsPortrait;
            if (!settled && noticeWaited < RotateTimeout)
            {
                return;
            }

            noticePending = false;
            NoticeBanner.Show(OnboardingCopy.UpdatedNotice);
            PlayerDataManager.WasResetForUpdate = false;
        }

        /// <summary>
        /// While the soft keyboard is up on the name screen, lifts the sheet just enough to keep the field (and,
        /// in portrait, the Next button under it) above the keyboard. The layouts keep both in the upper half, so
        /// this only moves anything on short screens or with a tall keyboard. Eases back down when it closes.
        /// </summary>
        void UpdateKeyboardLift()
        {
            if (sheet == null || completing)
            {
                return;
            }

            float target = 0f;
            if (CurrentScreen == ScreenName && nameInput != null && keyboardTarget != null && nameInput.isFocused &&
                TouchScreenKeyboard.isSupported && TouchScreenKeyboard.visible && canvas != null &&
                canvas.scaleFactor > 0f)
            {
                // The keyboard covers the bottom of the screen; its height is in pixels.
                float keyboardTop = TouchScreenKeyboard.area.height / canvas.scaleFactor;
                if (keyboardTop > 0f)
                {
                    // Screen Space Overlay: world corners are screen pixels (bottom-left origin).
                    keyboardTarget.GetWorldCorners(corners);
                    float bottom = corners[0].y / canvas.scaleFactor - keyboardLift;
                    target = Mathf.Max(0f, keyboardTop + KeyboardGap - bottom);
                }
            }

            if (Mathf.Approximately(target, keyboardLift))
            {
                return;
            }

            keyboardLift = UiTween.ReduceMotion
                ? target
                : Mathf.MoveTowards(keyboardLift, target, KeyboardLiftSpeed * Time.unscaledDeltaTime);
            sheet.anchoredPosition = sheetRest + new Vector2(0f, keyboardLift);
        }

        // ================================================================ screen 1: promise + name

        void BuildNameScreen()
        {
            // The field and Next stay in the upper half in both layouts, clear of the soft keyboard.
            string promiseText = OnboardingCopy.PromiseLine1 + "\n" + OnboardingCopy.PromiseLine2;
            UiTheme.TextRole questionRole = UiTheme.Body.WithWeight(UiFontWeight.Bold);
            float fieldY;
            float fieldWidth;
            if (portraitLayout)
            {
                Text promise = UiKit.Label(content, "Promise", promiseText,
                    UiTheme.DisplayTitle.WithSize(PortraitPromiseSize), UiTheme.TextPrimary);
                float y = StackLabel(promise, Margin, PortraitTop, ContentWidth);

                Text question = UiKit.Label(content, "Question", OnboardingCopy.NameQuestion, questionRole,
                    UiTheme.TextSecondary);
                y = StackLabel(question, Margin, y + UiTheme.Space40, ContentWidth);
                fieldY = y + 24f;
                fieldWidth = ContentWidth;
            }
            else
            {
                Text promise = UiKit.Label(content, "Promise", promiseText, UiTheme.DisplayTitle, UiTheme.TextPrimary);
                promise.horizontalOverflow = HorizontalWrapMode.Overflow;
                SetTopLeft(promise.rectTransform, Margin, 80f, TextWidth, 158f);

                Text question = UiKit.Label(content, "Question", OnboardingCopy.NameQuestion, questionRole,
                    UiTheme.TextSecondary);
                SetTopLeft(question.rectTransform, Margin, 268f, TextWidth, 52f);
                fieldY = 330f;
                fieldWidth = NameFieldWidth;
            }

            nameInput = BuildNameField(content, Margin, fieldY, fieldWidth);
            nameInput.text = draftName;
            nameInput.onValueChanged.AddListener(_ => UpdateNext());

            // Next right under (portrait) or beside (landscape) the field rather than at the sheet's foot, where
            // the keyboard would cover it.
            var nextRect = (RectTransform)nextButton.transform;
            if (portraitLayout)
            {
                SetTopLeft(nextRect, Margin, fieldY + NameFieldHeight + NameFieldGap, ContentWidth, UiTheme.TargetMin);
                keyboardTarget = nextRect;
            }
            else
            {
                SetTopLeft(nextRect, Margin + fieldWidth + NameFieldGap, fieldY, NavButtonWidth, UiTheme.TargetMin);
                keyboardTarget = (RectTransform)nameInput.transform;
            }

            UiTween.Delay(this, 0.05f, () =>
            {
                if (nameInput != null)
                {
                    nameInput.Select();
                    nameInput.ActivateInputField();
                }
            });
        }

        InputField BuildNameField(RectTransform parent, float x, float y, float width)
        {
            Image border = UiKit.Panel(parent, "Name field", UiTheme.BorderControl, UiTheme.RadiusButton, false);
            border.raycastTarget = true;
            SetTopLeft(border.rectTransform, x, y, width, NameFieldHeight);

            Image fill = UiKit.Panel(border.rectTransform, "Fill", UiTheme.Sunken, UiTheme.RadiusButton - UiTheme.Stroke, false);
            fill.rectTransform.offsetMin = new Vector2(UiTheme.Stroke, UiTheme.Stroke);
            fill.rectTransform.offsetMax = new Vector2(-UiTheme.Stroke, -UiTheme.Stroke);
            fill.raycastTarget = false;

            UiTheme.TextRole role = UiTheme.Body.WithSize(46);
            Text text = UiKit.Label(border.rectTransform, "Text", "", role, UiTheme.TextPrimary, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.rectTransform.offsetMin = new Vector2(32f, 0f);
            text.rectTransform.offsetMax = new Vector2(-32f, 0f);

            Text placeholder = UiKit.Label(border.rectTransform, "Placeholder", OnboardingCopy.NamePlaceholder, role,
                UiTheme.TextMuted, TextAnchor.MiddleLeft);
            placeholder.rectTransform.offsetMin = new Vector2(32f, 0f);
            placeholder.rectTransform.offsetMax = new Vector2(-32f, 0f);

            var input = border.gameObject.AddComponent<InputField>();
            input.targetGraphic = fill;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = OnboardingCopy.NameMaxLength;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = InputField.ContentType.Name;
            input.caretColor = UiTheme.TextPrimary;
            input.customCaretColor = true;
            input.selectionColor = UiTheme.WithAlpha(UiTheme.AccentPrimary, 0.3f);
            var nav = input.navigation;
            nav.mode = Navigation.Mode.None;
            input.navigation = nav;
            return input;
        }

        // ================================================================ screen 2: look

        void BuildLookScreen()
        {
            float titleBottom = AddTitle(OnboardingCopy.LookTitle);

            if (!catalogLoaded)
            {
                catalogLoaded = true;
                try
                {
                    catalog = KenneyRuntimeCatalogFactory.LoadCatalog();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[CharacterCreation] No character catalog for the look preview: " + ex.Message);
                    catalog = null;
                }
            }

            lookPreview = LookPreview.Create(content, catalog, AppearanceFor(lookIndex));
            const float cardH = 160f;
            float cardW;
            float gridX;
            float gridY;
            if (portraitLayout)
            {
                // One column: the turning model above the six looks, both centred. The model shrinks (to half
                // size at most) if a short screen cannot fit it above the cards.
                cardW = Mathf.Floor((ContentWidth - 2f * CardGap) / 3f);
                float gridW = 3f * cardW + 2f * CardGap;
                float gridH = 2f * cardH + CardGap;
                float y = titleBottom + 32f;
                if (lookPreview != null)
                {
                    float room = PortraitContentBottom - y - UiTheme.Space40 - gridH;
                    float size = Mathf.Clamp(Mathf.Min(room, ContentWidth), LookPreview.ImageSize * 0.5f,
                        LookPreview.ImageSize);
                    SetTopLeft(lookPreview.Rect, (sheetWidth - size) * 0.5f, y, size, size);
                    y += size + UiTheme.Space40;
                }

                gridX = (sheetWidth - gridW) * 0.5f;
                gridY = y;
            }
            else
            {
                cardW = 246f;
                float gridW = 3f * cardW + 2f * CardGap;
                float gridH = 2f * cardH + CardGap;
                if (lookPreview != null)
                {
                    SetTopLeft(lookPreview.Rect, Margin, 128f, LookPreview.ImageSize, LookPreview.ImageSize);
                    gridX = SheetWidth - Margin - gridW;
                }
                else
                {
                    gridX = (SheetWidth - gridW) * 0.5f;
                }

                gridY = 128f + (LookPreview.ImageSize - gridH) * 0.5f;
            }

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                float x = gridX + (i % 3) * (cardW + CardGap);
                float y = gridY + (i / 3) * (cardH + CardGap);
                CardView card = CardView.Create(content, "Look " + (i + 1), x, y, cardW, cardH, () => PickLook(index));

                Image swatch = UiKit.SpriteImage(card.Body, "Swatch", UiKit.Circle, 48f, ToneSwatches[i % 3]);
                RectTransform swatchRect = swatch.rectTransform;
                swatchRect.anchorMin = swatchRect.anchorMax = swatchRect.pivot = new Vector2(0.5f, 1f);
                swatchRect.anchoredPosition = new Vector2(0f, -28f);

                Text label = UiKit.Label(card.Body, "Label", OnboardingCopy.StyleLabel(i + 1), UiTheme.Label,
                    UiTheme.TextPrimary, TextAnchor.MiddleCenter);
                SetBottomStretch(label.rectTransform, 24f, 44f);
                lookCards.Add(card);
            }

            RefreshCards(lookCards, lookIndex);
        }

        void PickLook(int index)
        {
            lookIndex = Mathf.Clamp(index, 0, 5);
            RefreshCards(lookCards, lookIndex);
            draft.appearance = AppearanceFor(lookIndex);
            if (lookPreview != null)
            {
                lookPreview.SetAppearance(draft.appearance);
            }
        }

        // ================================================================ screen 3: the two profile taps

        void BuildProfileScreen()
        {
            UiTheme.TextRole questionRole = UiTheme.Body.WithWeight(UiFontWeight.Bold);
            Text q1 = UiKit.Label(content, "Spend question", OnboardingCopy.SpendQuestion, questionRole, UiTheme.TextPrimary);
            Text q2 = UiKit.Label(content, "Travel question", OnboardingCopy.TravelQuestion, questionRole, UiTheme.TextPrimary);
            RectTransform summary = UiKit.Rect(content, "Summary");
            Text built = UiKit.Label(summary, "Week built", OnboardingCopy.WeekBuiltLine, UiTheme.Body, UiTheme.TextPrimary);
            summaryPlaces = UiKit.Label(summary, "Places", "", UiTheme.Label, UiTheme.TextSecondary);

            if (portraitLayout)
            {
                // One column: each question over a 2 x 2 grid of its four answers, then the summary.
                float cardW = Mathf.Floor((ContentWidth - CardGap) * 0.5f);
                const float cardH = 128f;
                float y = StackLabel(q1, Margin, PortraitTop, ContentWidth) + 24f;
                y = AddOptionGrid(SpendingFocus.All, "Focus ", focusCards, PickFocus, y, cardW, cardH, 2);
                y = StackLabel(q2, Margin, y + 48f, ContentWidth) + 24f;
                y = AddOptionGrid(TravelMode.All, "Travel ", travelCards, PickTravel, y, cardW, cardH, 2);

                float builtHeight = StackLabel(built, 0f, 0f, ContentWidth);
                float placesHeight = Mathf.Ceil(2f * LineHeight(summaryPlaces));
                SetTopLeft(summaryPlaces.rectTransform, 0f, builtHeight + 8f, ContentWidth, placesHeight);
                SetTopLeft(summary, Margin, y + 48f, ContentWidth, builtHeight + 8f + placesHeight);
            }
            else
            {
                SetTopLeft(q1.rectTransform, Margin, 80f, TextWidth, 52f);
                AddOptionGrid(SpendingFocus.All, "Focus ", focusCards, PickFocus, 144f, 330f, 144f, 4);
                SetTopLeft(q2.rectTransform, Margin, 318f, TextWidth, 52f);
                AddOptionGrid(TravelMode.All, "Travel ", travelCards, PickTravel, 382f, 330f, 144f, 4);

                SetTopLeft(summary, Margin, 550f, TextWidth, 96f);
                SetTopLeft(built.rectTransform, 0f, 0f, TextWidth, 52f);
                SetTopLeft(summaryPlaces.rectTransform, 0f, 52f, TextWidth, 44f);
            }

            summaryGroup = summary.gameObject.AddComponent<CanvasGroup>();
            summaryGroup.alpha = 0f;
            summaryGroup.blocksRaycasts = false;

            RefreshProfile(false);
        }

        /// <summary>Lays <paramref name="options"/> out as cards in <paramref name="columns"/> columns from
        /// (Margin, <paramref name="y"/>) and returns the y under the last row.</summary>
        float AddOptionGrid(ProfileOption[] options, string namePrefix, List<CardView> cards, Action<string> pick,
            float y, float cardW, float cardH, int columns)
        {
            for (int i = 0; i < options.Length; i++)
            {
                ProfileOption option = options[i];
                float x = Margin + (i % columns) * (cardW + CardGap);
                float cardY = y + (i / columns) * (cardH + CardGap);
                CardView card = CardView.Create(content, namePrefix + option.id, x, cardY, cardW, cardH,
                    () => pick(option.id));
                AddCardLabel(card, option.cardLabel, cardW - 80f);
                cards.Add(card);
            }

            int rows = (options.Length + columns - 1) / columns;
            return y + rows * cardH + Mathf.Max(0, rows - 1) * CardGap;
        }

        void AddCardLabel(CardView card, string text, float width)
        {
            Text label = UiKit.Label(card.Body, "Label", text, UiTheme.Label, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 100f);
            rect.anchoredPosition = Vector2.zero;
        }

        void PickFocus(string id)
        {
            focusPick = id;
            RefreshProfile(true);
        }

        void PickTravel(string id)
        {
            travelPick = id;
            RefreshProfile(true);
        }

        void RefreshProfile(bool animate)
        {
            RefreshCards(focusCards, IndexOf(SpendingFocus.All, focusPick));
            RefreshCards(travelCards, IndexOf(TravelMode.All, travelPick));

            bool both = focusPick != null && travelPick != null;
            if (summaryGroup != null && both)
            {
                summaryPlaces.text = string.Join(OnboardingCopy.WeekPlacesSeparator,
                    ChapterSchedule.WeekPlaces(focusPick, travelPick));
                if (!summaryShown)
                {
                    summaryShown = true;
                    if (animate)
                    {
                        UiTween.Fade(summaryGroup, 1f);
                    }
                    else
                    {
                        summaryGroup.alpha = 1f;
                    }
                }
            }

            UpdateNext();
        }

        static int IndexOf(ProfileOption[] options, string id)
        {
            if (id == null)
            {
                return -1;
            }

            for (int i = 0; i < options.Length; i++)
            {
                if (options[i].id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        // ================================================================ screen 4: goal

        void BuildGoalScreen()
        {
            float titleBottom = AddTitle(OnboardingCopy.GoalTitle);
            float cardW;
            float cardH;
            float x0;
            float y0;
            int columns;
            if (portraitLayout)
            {
                // One column of wide cards: title and caption on the left, the target on the right.
                cardW = ContentWidth;
                cardH = 160f;
                x0 = Margin;
                y0 = titleBottom + UiTheme.Space40;
                columns = 1;
            }
            else
            {
                cardW = 560f;
                cardH = 220f;
                x0 = (SheetWidth - (2f * cardW + CardGap)) * 0.5f;
                y0 = 150f;
                columns = 2;
            }

            for (int i = 0; i < GoalPresets.All.Length; i++)
            {
                GoalPreset preset = GoalPresets.All[i];
                CardView card = CardView.Create(content, "Goal " + preset.id, x0 + (i % columns) * (cardW + CardGap),
                    y0 + (i / columns) * (cardH + CardGap), cardW, cardH, () => PickGoal(preset.id));

                Text title = UiKit.Label(card.Body, "Title", preset.title, UiTheme.Label, UiTheme.TextPrimary);
                Text caption = UiKit.Label(card.Body, "Caption", OnboardingCopy.GoalSavedCaption, UiTheme.Caption,
                    UiTheme.TextMuted);
                if (portraitLayout)
                {
                    const float targetWidth = 280f;
                    const float rightPad = 40f;
                    float leftWidth = cardW - 36f - targetWidth - rightPad - CardGap;
                    SetTopLeft(title.rectTransform, 36f, 34f, leftWidth, 44f);
                    SetTopLeft(caption.rectTransform, 36f, 90f, leftWidth, 40f);
                    // Vertically centred, below the selected check in the top-right corner.
                    Text target = UiKit.Label(card.Body, "Target", MoneyFormat.Rand(preset.target), UiTheme.HudValue,
                        UiTheme.TextPrimary, TextAnchor.MiddleRight);
                    SetTopLeft(target.rectTransform, cardW - rightPad - targetWidth, (cardH - 52f) * 0.5f + 8f,
                        targetWidth, 52f);
                }
                else
                {
                    SetTopLeft(title.rectTransform, 36f, 30f, cardW - 72f, 44f);
                    Text target = UiKit.Label(card.Body, "Target", MoneyFormat.Rand(preset.target), UiTheme.HudValue,
                        UiTheme.TextPrimary);
                    SetTopLeft(target.rectTransform, 36f, 86f, cardW - 72f, 52f);
                    SetTopLeft(caption.rectTransform, 36f, 150f, cardW - 72f, 40f);
                }

                goalCards.Add(card);
            }

            RefreshGoals();
        }

        void PickGoal(string id)
        {
            goalPick = id;
            RefreshGoals();
            UpdateNext();
        }

        void RefreshGoals()
        {
            int selected = -1;
            for (int i = 0; i < GoalPresets.All.Length; i++)
            {
                if (GoalPresets.All[i].id == goalPick)
                {
                    selected = i;
                }
            }

            RefreshCards(goalCards, selected);
        }

        // ================================================================ screen 5: Mali

        void BuildMaliScreen()
        {
            // The whole content area advances the text (a swipe that starts and ends on it counts as a tap); the
            // buttons sit above it.
            Image tapZone = UiKit.Panel(content, "Tap zone", Color.clear, 1f, false);
            tapZone.sprite = UiKit.White;
            tapZone.type = Image.Type.Simple;
            tapZone.raycastTarget = true;
            var tap = tapZone.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            var nav = tap.navigation;
            nav.mode = Navigation.Mode.None;
            tap.navigation = nav;
            tap.onClick.AddListener(OnMaliTap);

            float nextSize;
            float textX;
            float textY;
            float nextGap;
            if (portraitLayout)
            {
                // One column: Mali, her text, the continue button; the group sits a little above the middle of
                // the room over the bottom row.
                const float size = MaliPortraitSizeTall;
                nextSize = MaliNextSizeTall;
                nextGap = UiTheme.Space40;
                maliTextWidth = ContentWidth;
                float groupHeight = size + UiTheme.Space40 + MaliTextHeight + nextGap + nextSize;
                float top = PortraitTop + Mathf.Max(0f, (PortraitContentBottom - PortraitTop - groupHeight) * 0.4f);

                Image portrait = UiKit.SpriteImage(content, "Mali", UiKit.MaliPortrait, size, Color.white);
                SetTopLeft(portrait.rectTransform, (sheetWidth - size) * 0.5f, top, size, size);
                textX = Margin;
                textY = top + size + UiTheme.Space40;
            }
            else
            {
                nextSize = MaliNextSize;
                nextGap = 24f;
                maliTextWidth = MaliTextWidth;
                Image portrait = UiKit.SpriteImage(content, "Mali", UiKit.MaliPortrait, MaliPortraitSize, Color.white);
                SetTopLeft(portrait.rectTransform, Margin, 110f, MaliPortraitSize, MaliPortraitSize);
                textX = Margin + MaliPortraitSize + 40f;
                textY = 250f;
            }

            maliText = UiKit.Label(content, "Mali text", "", UiTheme.Dialogue, UiTheme.TextPrimary);
            maliText.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(maliText.rectTransform, textX, textY, maliTextWidth, MaliTextHeight);

            // The continue button (tester feedback): a big round "down" centred under the text, so a new player
            // knows a tap shows the next page. It bobs once a page is typed; the last page shows Let's go instead.
            maliNext = BuildMaliNext(nextSize);
            var nextRect = (RectTransform)maliNext.transform;
            SetTopLeft(nextRect, textX + (maliTextWidth - nextSize) * 0.5f, textY + MaliTextHeight + nextGap,
                nextSize, nextSize);
            maliNextRest = nextRect.anchoredPosition;

            foreach (string template in new[] { MaliParagraph1, MaliParagraph2, MaliParagraph3 })
            {
                string filled = MaliText.Fill(template, draft);
                maliPages.AddRange(UiTextLayout.Paginate(maliText, filled, maliTextWidth, MaliLinesPerPage));
            }

            // After a relayout, back to the page the player was on, already typed.
            int resume = maliResumePage;
            maliResumePage = -1;
            if (resume > 0 && maliPages.Count > 0)
            {
                StartMaliPage(Mathf.Min(resume, maliPages.Count - 1));
                CompleteMaliPage();
            }
            else
            {
                StartMaliPage(0);
            }
        }

        /// <summary>A round <see cref="UiTheme.AccentPrimary"/> button <paramref name="size"/> u across with a white
        /// down arrow (the kit's <c>arrowDown</c> icon, else <c>down</c>), card shadow, press feedback.</summary>
        Button BuildMaliNext(float size)
        {
            Button button = UiKit.PrimaryButton(content, string.Empty, OnMaliTap, size, size);
            button.gameObject.name = "Continue";
            var background = (Image)button.targetGraphic;
            UiKit.SetRadius(background, size * 0.5f);
            UiKit.AddShadow(background, UiTheme.ShadowCard);

            Image arrow = UiKit.IconImage((RectTransform)button.transform, "Arrow", "arrowDown", size * 0.5f,
                UiTheme.TextOnInverse);
            if (arrow.sprite == null)
            {
                arrow.sprite = UiKit.Icon("down");
                arrow.color = arrow.sprite != null ? UiTheme.TextOnInverse : Color.clear;
            }

            return button;
        }

        void StartMaliPage(int page)
        {
            maliPage = page;
            typed = 0f;
            pauseLeft = 0f;
            pageDone = false;
            maliText.text = "";
            RefreshMaliNext();
            GameEvents.RaiseMaliSpoke();
            if (!MaliGoFeatures.Typewriter || GameSettings.InstantText)
            {
                CompleteMaliPage();
            }
        }

        void CompleteMaliPage()
        {
            if (maliText == null || maliPage >= maliPages.Count)
            {
                return;
            }

            maliText.text = maliPages[maliPage];
            pageDone = true;
            RefreshMaliNext();
            if (maliPage == maliPages.Count - 1 && !letsGoButton.gameObject.activeSelf)
            {
                letsGoButton.gameObject.SetActive(true);
                var group = letsGoButton.GetComponent<CanvasGroup>();
                if (group == null)
                {
                    group = letsGoButton.gameObject.AddComponent<CanvasGroup>();
                }

                group.alpha = 0f;
                UiTween.Fade(group, 1f);
            }
        }

        /// <summary>The continue button shows while there is more to read (every page but the last, and the last
        /// one until it is typed: a tap finishes it), and bobs once the page is typed (still with reduce motion,
        /// <see cref="UiTween.Bob"/> does nothing).</summary>
        void RefreshMaliNext()
        {
            if (maliNext == null)
            {
                return;
            }

            var rect = (RectTransform)maliNext.transform;
            UiTween.Stop(rect);
            rect.anchoredPosition = maliNextRest;
            bool lastDone = pageDone && maliPage >= maliPages.Count - 1;
            maliNext.gameObject.SetActive(!lastDone);
            if (!lastDone && pageDone)
            {
                UiTween.Bob(rect, MaliNextBob);
            }
        }

        void OnMaliTap()
        {
            if (maliText == null)
            {
                return;
            }

            if (!pageDone)
            {
                CompleteMaliPage();
            }
            else if (maliPage < maliPages.Count - 1)
            {
                StartMaliPage(maliPage + 1);
            }
        }

        void UpdateMaliTyping()
        {
            if (maliText == null || pageDone || maliPage >= maliPages.Count)
            {
                return;
            }

            string page = maliPages[maliPage];
            float dt = Time.unscaledDeltaTime;
            if (pauseLeft > 0f)
            {
                pauseLeft -= dt;
                return;
            }

            int before = Mathf.FloorToInt(typed);
            typed += dt * Mathf.Max(1, GameSettings.CharsPerSecond);
            int count = Mathf.Min(page.Length, Mathf.FloorToInt(typed));
            if (count > before && count > 0)
            {
                char last = page[count - 1];
                if (last == '.' || last == '!' || last == '?')
                {
                    pauseLeft = GameSettings.PauseAfterSentence;
                }
                else if (last == ',')
                {
                    pauseLeft = GameSettings.PauseAfterComma;
                }
            }

            maliText.text = page.Substring(0, count);
            if (count >= page.Length)
            {
                CompleteMaliPage();
            }
        }

        // ================================================================ helpers

        /// <summary>Adds the screen title and returns the y under it (wrapping in portrait).</summary>
        float AddTitle(string title)
        {
            Text label = UiKit.Label(content, "Title", title, UiTheme.Title, UiTheme.TextPrimary);
            if (portraitLayout)
            {
                return StackLabel(label, Margin, PortraitTop, ContentWidth);
            }

            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(label.rectTransform, Margin, 60f, TextWidth, 64f);
            return 124f;
        }

        /// <summary>Places a wrapping label at (<paramref name="x"/>, <paramref name="y"/>) from the top-left, as
        /// tall as its lines, and returns the y under it. The text is pre-broken a little narrower than the rect,
        /// so Unity's own wrap never adds a line the height did not count.</summary>
        static float StackLabel(Text label, float x, float y, float width)
        {
            float wrapWidth = Mathf.Max(1f, width - 8f);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.text = UiTextLayout.Wrap(label, label.text, wrapWidth);
            int lines = Mathf.Max(1, UiTextLayout.CountLines(label, label.text, wrapWidth));
            float height = Mathf.Ceil(lines * LineHeight(label));
            SetTopLeft(label.rectTransform, x, y, width, height);
            return y + height;
        }

        /// <summary>One line of <paramref name="label"/> in u (a role's size x its line spacing).</summary>
        static float LineHeight(Text label)
        {
            return label.fontSize * label.lineSpacing * UiTheme.FontLineHeightEm;
        }

        static void RefreshCards(List<CardView> cards, int selected)
        {
            for (int i = 0; i < cards.Count; i++)
            {
                cards[i].SetSelected(i == selected);
            }
        }

        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, -y);
        }

        static void SetBottomStretch(RectTransform rect, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, bottom);
        }

        /// <summary>A selectable card (Card fill, radius 36, card shadow); selected = Tint fill + 6 u AccentPrimary
        /// ring + a checkmark in the top-right corner (the ring alone in Coin was too faint on Paper).</summary>
        sealed class CardView
        {
            const float CheckSize = 36f;
            const float CheckInset = 10f;

            public RectTransform Body;
            Image ring;
            Image fill;
            Image check;

            public static CardView Create(RectTransform parent, string name, float x, float y, float w, float h, Action onTap)
            {
                RectTransform root = UiKit.Rect(parent, name);
                SetTopLeft(root, x, y, w, h);

                var view = new CardView();
                view.ring = UiKit.Panel(root, "Ring", UiTheme.AccentPrimary, UiTheme.RadiusCard + RingWidth, false);
                view.ring.rectTransform.offsetMin = new Vector2(-RingWidth, -RingWidth);
                view.ring.rectTransform.offsetMax = new Vector2(RingWidth, RingWidth);
                view.ring.raycastTarget = false;
                view.ring.enabled = false;

                view.fill = UiKit.Panel(root, "Card", UiTheme.Card, UiTheme.RadiusCard, UiTheme.ShadowCard);
                view.fill.raycastTarget = true;
                view.Body = view.fill.rectTransform;

                var button = view.fill.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                var nav = button.navigation;
                nav.mode = Navigation.Mode.None;
                button.navigation = nav;
                button.onClick.AddListener(() =>
                {
                    UiKit.NotifyButtonClicked();
                    onTap?.Invoke();
                });

                // A sibling after the fill, so it draws above everything placed in Body; the card layouts above
                // keep the top-right corner clear.
                view.check = UiKit.IconImage(root, "Check", "checkmark", CheckSize, UiTheme.AccentPrimary);
                RectTransform checkRect = view.check.rectTransform;
                checkRect.anchorMin = checkRect.anchorMax = checkRect.pivot = new Vector2(1f, 1f);
                checkRect.anchoredPosition = new Vector2(-CheckInset, -CheckInset);
                view.check.raycastTarget = false;
                view.check.enabled = false;
                return view;
            }

            public void SetSelected(bool selected)
            {
                fill.color = selected ? UiTheme.Tint : UiTheme.Card;
                ring.enabled = selected;
                check.enabled = selected && check.sprite != null;
            }
        }
    }
}
