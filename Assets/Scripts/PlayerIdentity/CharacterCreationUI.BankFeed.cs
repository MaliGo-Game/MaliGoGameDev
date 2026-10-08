using System;
using System.Collections.Generic;
using MaliGo.BankFeed;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.PlayerIdentity
{
    /// <summary>
    /// The optional "Connect your bank" screen (docs/BANK_FEED.md), shown before the two profile taps only when
    /// <c>MaliGoFeatures.BankFeedOnboarding</c> is on (off for the beta). Three stages on one screen:
    /// intro card (Connect / Skip for now) -> consent (what is read, only habits stay on the phone, nothing is sent,
    /// you can skip) -> "What your money says" (top three habits, then Next pre-fills the two taps, which the player
    /// can still change). With no live provider registered (the public build) the consent stage offers only "Try
    /// with a sample person", and everything after it is labelled as sample data; nothing pretends to connect.
    /// Skip clears any summary and goes to the taps exactly as before. Raw transactions never reach this class:
    /// <see cref="BankFeedSession"/> hands back only the summary.
    /// </summary>
    public partial class CharacterCreationUI
    {
        const int ScreenBank = 6;

        const int BankIntro = 0;
        const int BankConsent = 1;
        const int BankSummary = 2;

        // Widths fit the labels in Button 44 Bold with room to spare (BankFeedTests.TestButtonLabelsFit).
        const float BankPrimaryWidth = 600f;
        const float BankSecondaryWidth = 440f;
        const float BankDotSize = 14f;
        const float BankPointGap = 12f;

        int bankStage = BankIntro;
        BankHabitSummary bankPending;
        string bankPersonaId;
        bool bankBusy;
        bool bankError;
        int bankRun;

        /// <summary>True when the two taps hold what the bank summary suggested (so Skip can put them back to empty).</summary>
        bool bankPrefilled;

        // ================================================================ build

        void BuildBankScreen()
        {
            nextButton.gameObject.SetActive(bankStage == BankSummary && bankPending != null);
            switch (bankStage)
            {
                case BankConsent: BuildBankConsent(); break;
                case BankSummary when bankPending != null: BuildBankSummary(); break;
                default:
                    bankStage = BankIntro;
                    BuildBankIntro();
                    break;
            }
        }

        void BuildBankIntro()
        {
            float y = AddTitle(BankFeedCopy.IntroTitle);
            Text body = UiKit.Label(content, "Intro", BankFeedCopy.IntroBody, UiTheme.Body, UiTheme.TextSecondary);
            StackLabel(body, Margin, y + UiTheme.Space30, ContentWidth);

            AddBankButtons(BankFeedCopy.ConnectButton, () => SetBankStage(BankConsent), BankFeedCopy.SkipButton, SkipBank);
        }

        void BuildBankConsent()
        {
            float y = AddTitle(BankFeedCopy.ConsentTitle) + UiTheme.Space30;
            foreach (string point in BankFeedCopy.ConsentPoints)
            {
                y = AddBankPoint(point, y, UiTheme.Body) + BankPointGap;
            }

            bool live = TransactionSources.HasLive;
            string note = bankError ? BankFeedCopy.ErrorLine : bankBusy ? BankFeedCopy.WaitingLine : live ? null : BankFeedCopy.NoLiveLine;
            if (note != null)
            {
                Text noteLabel = UiKit.Label(content, "Note", note, UiTheme.Caption,
                    bankError ? UiTheme.Attention : UiTheme.TextMuted);
                StackLabel(noteLabel, Margin, y + UiTheme.Space20, ContentWidth);
            }

            if (live)
            {
                AddBankButtons(BankFeedCopy.AgreeButton, ConnectLive, BankFeedCopy.SkipButton, SkipBank);
            }
            else
            {
                AddBankButtons(BankFeedCopy.SampleButton, () => RunSample(StartPersona()), BankFeedCopy.SkipButton, SkipBank);
            }
        }

        void BuildBankSummary()
        {
            BankHabitSummary s = bankPending;
            float y = AddTitle(BankFeedCopy.SummaryTitle) + UiTheme.Space20;

            if (s.IsSample)
            {
                // The sample label: a warm pill, then which made-up person this is.
                Text badge = UiKit.Label(content, "Sample badge", BankFeedCopy.SampleBadge, UiTheme.Caption,
                    UiTheme.Attention, TextAnchor.MiddleCenter);
                badge.horizontalOverflow = HorizontalWrapMode.Overflow;
                float badgeWidth = UiTextLayout.MeasureWidth(badge, BankFeedCopy.SampleBadge) + 2f * UiTheme.Space20;
                float badgeHeight = Mathf.Ceil(LineHeight(badge)) + UiTheme.Space10;
                Image pill = UiKit.Panel(content, "Sample pill", UiTheme.Warm, badgeHeight * 0.5f, false);
                pill.raycastTarget = false;
                SetTopLeft(pill.rectTransform, Margin, y, badgeWidth, badgeHeight);
                badge.transform.SetAsLastSibling();
                SetTopLeft(badge.rectTransform, Margin, y, badgeWidth, badgeHeight);

                Text persona = UiKit.Label(content, "Sample person", BankFeedCopy.SamplePersonLine(s.samplePersonaId),
                    UiTheme.Caption, UiTheme.TextMuted, TextAnchor.MiddleLeft);
                if (portraitLayout)
                {
                    y = StackLabel(persona, Margin, y + badgeHeight + UiTheme.Space10, ContentWidth);
                }
                else
                {
                    float x = Margin + badgeWidth + UiTheme.Space20;
                    persona.horizontalOverflow = HorizontalWrapMode.Overflow;
                    SetTopLeft(persona.rectTransform, x, y, ContentWidth - (x - Margin), badgeHeight);
                    y += badgeHeight;
                }

                y += UiTheme.Space20;
            }

            List<string> lines = BankFeedCopy.TopHabitLines(s, 3);
            if (lines.Count == 0)
            {
                lines.Add(BankFeedCopy.NoHabitsLine);
            }

            // The landscape sheet is only 880 u tall: habit and week lines a size or two down there, so three habits
            // (one line each at 1380 u) and a two-line week line still end above the button row.
            UiTheme.TextRole habitRole = portraitLayout ? UiTheme.Body : UiTheme.Caption;
            UiTheme.TextRole weekRole = portraitLayout ? UiTheme.Body.WithWeight(UiFontWeight.Bold) : UiTheme.Label;
            foreach (string line in lines)
            {
                y = AddBankPoint(line, y, habitRole) + BankPointGap;
            }

            BankFeedMapping.SuggestProfile(s, out string focus, out string travel);
            Text week = UiKit.Label(content, "Week line",
                BankFeedCopy.WeekLine + " " + BankFeedCopy.WeekPicks(focus, travel), weekRole, UiTheme.TextPrimary);
            y = StackLabel(week, Margin, y + UiTheme.Space20, ContentWidth);
            Text change = UiKit.Label(content, "Change line", BankFeedCopy.ChangeLine, UiTheme.Caption, UiTheme.TextMuted);
            StackLabel(change, Margin, y + UiTheme.Space10, ContentWidth);

            // Next (the nav button) accepts; the developer/sample cycle sits where Skip sits on the other stages.
            if (s.IsSample)
            {
                AddBankButtons(null, null, BankFeedCopy.AnotherSampleButton,
                    () => RunSample(SampleTransactionSource.NextPersonaId(s.samplePersonaId)));
            }
        }

        /// <summary>A small accent dot and a wrapping line beside it; returns the y under the line.</summary>
        float AddBankPoint(string text, float y, UiTheme.TextRole role)
        {
            Text label = UiKit.Label(content, "Point", text, role, UiTheme.TextPrimary);
            float indent = BankDotSize + UiTheme.Space20;
            float bottom = StackLabel(label, Margin + indent, y, ContentWidth - indent);
            Image dot = UiKit.SpriteImage(content, "Dot", UiKit.Circle, BankDotSize, UiTheme.AccentPrimary);
            float firstLine = LineHeight(label);
            SetTopLeft(dot.rectTransform, Margin, y + (firstLine - BankDotSize) * 0.5f, BankDotSize, BankDotSize);
            return bottom;
        }

        /// <summary>
        /// The stage's own buttons, in the nav row's place (Next is hidden on these stages). Landscape: primary
        /// bottom-right, secondary to its left. Portrait: primary fills the bottom row beside Back, secondary full
        /// width just above it. A null label leaves that button out.
        /// </summary>
        void AddBankButtons(string primaryLabel, Action primary, string secondaryLabel, Action secondary)
        {
            float rowAbove = ButtonMargin + UiTheme.TargetMin + UiTheme.TappableGap;
            if (primaryLabel != null)
            {
                Button p = UiKit.PrimaryButton(content, primaryLabel, () => { if (!bankBusy) primary?.Invoke(); },
                    BankPrimaryWidth);
                p.interactable = !bankBusy;
                var rect = (RectTransform)p.transform;
                if (portraitLayout)
                {
                    PlaceBottomRow(rect, Margin + PortraitBackWidth + UiTheme.TappableGap);
                }
                else
                {
                    PlaceCorner(rect, new Vector2(1f, 0f), new Vector2(-ButtonMargin, ButtonMargin), BankPrimaryWidth);
                }
            }

            if (secondaryLabel != null)
            {
                Button s = UiKit.SecondaryButton(content, secondaryLabel, () => { if (!bankBusy) secondary?.Invoke(); },
                    BankSecondaryWidth);
                s.interactable = !bankBusy;
                var rect = (RectTransform)s.transform;
                if (portraitLayout)
                {
                    PlaceBottomRow(rect, Margin);
                    rect.anchoredPosition = new Vector2(0f, rowAbove);
                }
                else
                {
                    // Left of the primary (or of Next, on the summary stage).
                    float right = ButtonMargin + (primaryLabel != null ? BankPrimaryWidth : NavButtonWidth) + UiTheme.TappableGap;
                    PlaceCorner(rect, new Vector2(1f, 0f), new Vector2(-right, ButtonMargin), BankSecondaryWidth);
                }
            }
        }

        // ================================================================ actions

        void SetBankStage(int stage)
        {
            bankStage = stage;
            bankError = false;
            ShowScreen(screenIndex);
        }

        /// <summary>Back inside the screen: summary -> consent -> intro. False on the intro (leave the screen).</summary>
        bool BankBack()
        {
            if (CurrentScreen != ScreenBank || bankStage == BankIntro)
            {
                return false;
            }

            bankRun++;
            bankBusy = false;
            SetBankStage(bankStage == BankSummary ? BankConsent : BankIntro);
            return true;
        }

        /// <summary>Skip: no summary is kept, and the taps are as they would have been without this screen.</summary>
        void SkipBank()
        {
            bankRun++;
            bankBusy = false;
            bankPending = null;
            bankStage = BankIntro;
            draft.bankHabits = new BankHabitSummary();
            if (bankPrefilled)
            {
                focusPick = null;
                travelPick = null;
                bankPrefilled = false;
            }

            ShowScreen(screenIndex + 1);
        }

        /// <summary>Next on the summary stage: keep the summary in the draft and pre-fill the two taps.</summary>
        void AcceptBankSummary()
        {
            if (bankPending == null)
            {
                return;
            }

            draft.bankHabits = bankPending;
            BankFeedMapping.SuggestProfile(bankPending, out string focus, out string travel);
            focusPick = focus;
            travelPick = travel;
            bankPrefilled = true;
        }

        string StartPersona()
        {
            if (!string.IsNullOrEmpty(bankPersonaId))
            {
                return bankPersonaId;
            }

            return SampleTransactionSource.Persona(MaliGoFeatures.BankFeedSamplePersona).id;
        }

        void RunSample(string personaId)
        {
            bankPersonaId = SampleTransactionSource.Persona(personaId).id;
            int run = ++bankRun;
            BankFeedSession.RunSample(bankPersonaId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), o => OnBankOutcome(run, o));
        }

        /// <summary>A registered live adapter runs its own consent and calls back (now or later, on the main thread).</summary>
        void ConnectLive()
        {
            ITransactionSource source = TransactionSources.CreateLive();
            if (source == null)
            {
                bankError = true;
                ShowScreen(screenIndex);
                return;
            }

            int run = ++bankRun;
            bankBusy = true;
            bankError = false;
            ShowScreen(screenIndex);
            BankFeedSession.Run(source, DateTimeOffset.Now, BankFeedSession.DefaultWindowDays,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "", o => OnBankOutcome(run, o));
        }

        void OnBankOutcome(int run, BankFeedOutcome outcome)
        {
            // A late answer from a flow the player has left (Back, Skip, another sample) is dropped.
            if (this == null || run != bankRun || completing)
            {
                return;
            }

            bankBusy = false;
            if (outcome == null || !outcome.Ok || outcome.Summary == null)
            {
                bankError = true;
                bankStage = BankConsent;
            }
            else
            {
                bankError = false;
                bankPending = outcome.Summary;
                bankStage = BankSummary;
            }

            if (CurrentScreen == ScreenBank)
            {
                ShowScreen(screenIndex);
            }
        }
    }
}
