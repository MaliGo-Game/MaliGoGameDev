using System;
using System.Collections.Generic;
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
    /// 2 "Pick your look": six looks (two builds x light/medium/deep) beside a turning <see cref="LookPreview"/>;
    /// 3 the two profile taps, then "We've built your week around where your money goes." and the first three
    ///   places from <c>ChapterSchedule.WeekPlaces</c>; Next needs a pick in both rows and runs
    ///   <c>SpendingProfiles.SetFromOnboarding</c> (skipped when <c>MaliGoFeatures.ProfileTaps</c> is off: the
    ///   default profile is kept);
    /// 4 the savings goal (<c>GoalPresets</c>);
    /// 5 Mali's three paragraphs, typed and paginated at 3 lines; Let's go sets <c>isCharacterCreated</c> and
    ///   <c>hasMetMali</c>, runs <c>ChapterFlow.StartChapter(data, 1)</c>, saves and loads the world.
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

        const float SheetWidth = 1500f;
        const float SheetHeight = 880f;
        const float Margin = 60f;
        const float TextWidth = 1380f;
        const float ButtonMargin = 40f;
        const float NavButtonWidth = 320f;
        const float LetsGoWidth = 400f;
        const float RingWidth = 6f;
        const float MaliTextWidth = 820f;
        const int MaliLinesPerPage = 3;
        const float MaliCueSize = 48f;

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

        // Screen 5 typewriter.
        readonly List<string> maliPages = new List<string>();
        int maliPage;
        Text maliText;
        Image maliCue;
        float typed;
        float pauseLeft;
        bool pageDone;

        void Awake()
        {
            GameFlowController.EnsurePlayerDataManager();
            draft = PlayerData.CreateNew();

            screens.Add(ScreenName);
            screens.Add(ScreenLook);
            if (MaliGoFeatures.ProfileTaps)
            {
                screens.Add(ScreenProfile);
            }

            screens.Add(ScreenGoal);
            screens.Add(ScreenMali);

            BuildShell();
            ShowScreen(0);
            // Android back (Escape) goes to the previous screen; no modal is ever open here.
            UiModal.BackWithNoModal += OnBack;
        }

        void Start()
        {
            if (PlayerDataManager.WasResetForUpdate)
            {
                NoticeBanner.Show(OnboardingCopy.UpdatedNotice);
                PlayerDataManager.WasResetForUpdate = false;
            }
        }

        void OnDestroy()
        {
            UiModal.BackWithNoModal -= OnBack;
            UiTween.Stop(this);
            if (maliCue != null)
            {
                UiTween.Stop(maliCue.rectTransform);
            }

            if (canvas != null)
            {
                Destroy(canvas.gameObject);
            }
        }

        int CurrentScreen => screens[screenIndex];

        // ================================================================ shell

        void BuildShell()
        {
            canvas = UiCanvasFactory.Create("CharacterCreation_Canvas", UiTheme.Sort.CharacterCreation, transform,
                out RectTransform safeRoot);
            UiCanvasFactory.FullBleed(canvas, "Backdrop", UiTheme.Inverse);

            Image sheetImage = UiKit.Panel(safeRoot, "Sheet", UiTheme.Paper, UiTheme.RadiusSheet, true);
            sheetImage.raycastTarget = true;
            sheet = sheetImage.rectTransform;
            sheet.anchorMin = sheet.anchorMax = sheet.pivot = new Vector2(0.5f, 0.5f);
            sheet.sizeDelta = new Vector2(SheetWidth, SheetHeight);

            content = UiKit.Rect(sheet, "Content");
            dots = UiKit.Rect(sheet, "Dots");

            backButton = UiKit.SecondaryButton(sheet, OnboardingCopy.BackButton, OnBack, NavButtonWidth);
            var backRect = (RectTransform)backButton.transform;
            backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(0f, 0f);
            backRect.anchoredPosition = new Vector2(ButtonMargin, ButtonMargin);

            nextButton = UiKit.PrimaryButton(sheet, OnboardingCopy.NextButton, OnNext, NavButtonWidth);
            var nextRect = (RectTransform)nextButton.transform;
            nextRect.anchorMin = nextRect.anchorMax = nextRect.pivot = new Vector2(1f, 0f);
            nextRect.anchoredPosition = new Vector2(-ButtonMargin, ButtonMargin);

            letsGoButton = UiKit.PrimaryButton(sheet, OnboardingCopy.LetsGoButton, Complete, LetsGoWidth);
            var goRect = (RectTransform)letsGoButton.transform;
            goRect.anchorMin = goRect.anchorMax = goRect.pivot = new Vector2(1f, 0f);
            goRect.anchoredPosition = new Vector2(-ButtonMargin, ButtonMargin);
            letsGoButton.gameObject.SetActive(false);
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

        void ClearContent()
        {
            lookCards.Clear();
            focusCards.Clear();
            travelCards.Clear();
            goalCards.Clear();
            nameInput = null;
            lookPreview = null;
            summaryGroup = null;
            summaryPlaces = null;
            summaryShown = false;
            maliText = null;
            if (maliCue != null)
            {
                UiTween.Stop(maliCue.rectTransform);
                maliCue = null;
            }

            maliPages.Clear();

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

            GameFlowController.LoadWorldScene();
        }

        // ================================================================ screen 1: promise + name

        void BuildNameScreen()
        {
            Text promise = UiKit.Label(content, "Promise", OnboardingCopy.PromiseLine1 + "\n" + OnboardingCopy.PromiseLine2,
                UiTheme.DisplayTitle, UiTheme.TextPrimary);
            promise.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(promise.rectTransform, Margin, 90f, TextWidth, 158f);

            Text question = UiKit.Label(content, "Question", OnboardingCopy.NameQuestion,
                UiTheme.Body.WithWeight(UiFontWeight.Bold), UiTheme.TextSecondary);
            SetTopLeft(question.rectTransform, Margin, 288f, TextWidth, 52f);

            nameInput = BuildNameField(content, Margin, 352f);
            nameInput.text = draftName;
            nameInput.onValueChanged.AddListener(_ => UpdateNext());
            UiTween.Delay(this, 0.05f, () =>
            {
                if (nameInput != null)
                {
                    nameInput.Select();
                    nameInput.ActivateInputField();
                }
            });
        }

        InputField BuildNameField(RectTransform parent, float x, float y)
        {
            Image border = UiKit.Panel(parent, "Name field", UiTheme.BorderControl, UiTheme.RadiusButton, false);
            border.raycastTarget = true;
            SetTopLeft(border.rectTransform, x, y, 900f, 144f);

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
            AddTitle(OnboardingCopy.LookTitle);

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
            const float cardW = 246f;
            const float cardH = 160f;
            const float gap = 20f;
            float gridW = 3f * cardW + 2f * gap;
            float gridH = 2f * cardH + gap;
            float gridX;
            float gridY;
            if (lookPreview != null)
            {
                SetTopLeft(lookPreview.Rect, Margin, 128f, LookPreview.ImageSize, LookPreview.ImageSize);
                gridX = SheetWidth - Margin - gridW;
                gridY = 128f + (LookPreview.ImageSize - gridH) * 0.5f;
            }
            else
            {
                gridX = (SheetWidth - gridW) * 0.5f;
                gridY = 128f + (LookPreview.ImageSize - gridH) * 0.5f;
            }

            for (int i = 0; i < 6; i++)
            {
                int index = i;
                float x = gridX + (i % 3) * (cardW + gap);
                float y = gridY + (i / 3) * (cardH + gap);
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
            const float cardW = 330f;
            const float cardH = 144f;
            const float gap = 20f;

            Text q1 = UiKit.Label(content, "Spend question", OnboardingCopy.SpendQuestion, questionRole, UiTheme.TextPrimary);
            SetTopLeft(q1.rectTransform, Margin, 80f, TextWidth, 52f);
            for (int i = 0; i < SpendingFocus.All.Length; i++)
            {
                ProfileOption option = SpendingFocus.All[i];
                CardView card = CardView.Create(content, "Focus " + option.id, Margin + i * (cardW + gap), 144f, cardW, cardH,
                    () => PickFocus(option.id));
                AddCardLabel(card, option.cardLabel);
                focusCards.Add(card);
            }

            Text q2 = UiKit.Label(content, "Travel question", OnboardingCopy.TravelQuestion, questionRole, UiTheme.TextPrimary);
            SetTopLeft(q2.rectTransform, Margin, 318f, TextWidth, 52f);
            for (int i = 0; i < TravelMode.All.Length; i++)
            {
                ProfileOption option = TravelMode.All[i];
                CardView card = CardView.Create(content, "Travel " + option.id, Margin + i * (cardW + gap), 382f, cardW, cardH,
                    () => PickTravel(option.id));
                AddCardLabel(card, option.cardLabel);
                travelCards.Add(card);
            }

            RectTransform summary = UiKit.Rect(content, "Summary");
            SetTopLeft(summary, Margin, 550f, TextWidth, 96f);
            summaryGroup = summary.gameObject.AddComponent<CanvasGroup>();
            summaryGroup.alpha = 0f;
            summaryGroup.blocksRaycasts = false;

            Text built = UiKit.Label(summary, "Week built", OnboardingCopy.WeekBuiltLine, UiTheme.Body, UiTheme.TextPrimary);
            SetTopLeft(built.rectTransform, 0f, 0f, TextWidth, 52f);
            summaryPlaces = UiKit.Label(summary, "Places", "", UiTheme.Label, UiTheme.TextSecondary);
            SetTopLeft(summaryPlaces.rectTransform, 0f, 52f, TextWidth, 44f);

            RefreshProfile(false);
        }

        void AddCardLabel(CardView card, string text)
        {
            Text label = UiKit.Label(card.Body, "Label", text, UiTheme.Label, UiTheme.TextPrimary, TextAnchor.MiddleCenter);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(250f, 100f);
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
            AddTitle(OnboardingCopy.GoalTitle);
            const float cardW = 560f;
            const float cardH = 220f;
            const float gap = 20f;
            float x0 = (SheetWidth - (2f * cardW + gap)) * 0.5f;
            const float y0 = 150f;

            for (int i = 0; i < GoalPresets.All.Length; i++)
            {
                GoalPreset preset = GoalPresets.All[i];
                CardView card = CardView.Create(content, "Goal " + preset.id, x0 + (i % 2) * (cardW + gap),
                    y0 + (i / 2) * (cardH + gap), cardW, cardH, () => PickGoal(preset.id));

                Text title = UiKit.Label(card.Body, "Title", preset.title, UiTheme.Label, UiTheme.TextPrimary);
                SetTopLeft(title.rectTransform, 36f, 30f, cardW - 72f, 44f);
                Text target = UiKit.Label(card.Body, "Target", MoneyFormat.Rand(preset.target), UiTheme.HudValue,
                    UiTheme.TextPrimary);
                SetTopLeft(target.rectTransform, 36f, 86f, cardW - 72f, 52f);
                Text caption = UiKit.Label(card.Body, "Caption", OnboardingCopy.GoalSavedCaption, UiTheme.Caption,
                    UiTheme.TextMuted);
                SetTopLeft(caption.rectTransform, 36f, 150f, cardW - 72f, 40f);
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
            // The whole content area advances the text; the buttons sit above it.
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

            Image portrait = UiKit.SpriteImage(content, "Mali", UiKit.MaliPortrait, 520f, Color.white);
            SetTopLeft(portrait.rectTransform, Margin, 110f, 520f, 520f);

            maliText = UiKit.Label(content, "Mali text", "", UiTheme.Dialogue, UiTheme.TextPrimary);
            maliText.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(maliText.rectTransform, Margin + 520f + 40f, 250f, MaliTextWidth, 220f);

            // The continue cue, as in Mali's dialogue box: a bobbing "down" under the text's right edge once a page
            // is typed, so a new player knows a tap shows the next page. The last page shows Let's go instead.
            maliCue = UiKit.IconImage(content, "Cue", "down", MaliCueSize, UiTheme.AccentPrimary);
            maliCue.raycastTarget = false;
            SetTopLeft(maliCue.rectTransform, Margin + 520f + 40f + MaliTextWidth - MaliCueSize, 250f + 220f + 16f,
                MaliCueSize, MaliCueSize);
            maliCue.gameObject.SetActive(false);

            foreach (string template in new[] { MaliParagraph1, MaliParagraph2, MaliParagraph3 })
            {
                string filled = MaliText.Fill(template, draft);
                maliPages.AddRange(UiTextLayout.Paginate(maliText, filled, MaliTextWidth, MaliLinesPerPage));
            }

            StartMaliPage(0);
        }

        void StartMaliPage(int page)
        {
            maliPage = page;
            typed = 0f;
            pauseLeft = 0f;
            pageDone = false;
            maliText.text = "";
            SetMaliCue(false);
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
            SetMaliCue(maliPage < maliPages.Count - 1);
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

        void SetMaliCue(bool visible)
        {
            if (maliCue == null)
            {
                return;
            }

            RectTransform rect = maliCue.rectTransform;
            UiTween.Stop(rect);
            rect.anchoredPosition = new Vector2(Margin + 520f + 40f + MaliTextWidth - MaliCueSize, -(250f + 220f + 16f));
            bool show = visible && maliCue.sprite != null;
            maliCue.gameObject.SetActive(show);
            if (show)
            {
                UiTween.Bob(rect);
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

        void Update()
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

        void AddTitle(string title)
        {
            Text label = UiKit.Label(content, "Title", title, UiTheme.Title, UiTheme.TextPrimary);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            SetTopLeft(label.rectTransform, Margin, 60f, TextWidth, 64f);
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
