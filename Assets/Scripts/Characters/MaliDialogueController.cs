using System;
using System.Collections.Generic;
using System.Globalization;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Dialogue;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.Scenarios;
using UnityEngine;

namespace MaliGo.Characters
{
    /// <summary>How a Mali line is shown (design spec 7.7): the compact box that never blocks, or the blocking box.</summary>
    public enum MaliLineMode
    {
        Passing,
        Blocking
    }

    /// <summary>
    /// Everything Mali says goes through here (design spec 7.7). Every template is filled with MaliText.Fill, so
    /// no content string ever reaches string.Format.
    ///
    /// Queue: blocking lines queue FIFO; a passing line replaces a passing line; a blocking line supersedes a
    /// showing passing line; a passing line that arrives while a blocking line is up waits (only the latest one)
    /// until the blocking queue is empty. A passing line that needs more than 2 lines is shown in the blocking box.
    /// Subscribes to GameEvents.DayStarted for the morning line (or the replay's Day-1 line, 4.2.4). Raises
    /// GameEvents.MaliSpoke on every line and puts Mali's 3D model in TALK while a line is up.
    /// </summary>
    public class MaliDialogueController : MonoBehaviour
    {
        public const string StretchedMorningPrefix = "You seem stretched. ";
        public const string ReplayWithPlan = "Same week, another go, {name}. Last time, the night before payday, you said: \"{plan}\"";
        public const string ReplayWithoutPlan = "Same week, another go, {name}. R600 in your pocket, R400 in savings, seven days to payday.";

        [SerializeField] MaliDialogueView dialogueView;
        [SerializeField] MaliNpcController maliController;

        public event Action<string> OnDialogueShown;
        public event Action OnDialogueHidden;

        sealed class Line
        {
            public string text;
            public MaliLineMode mode;
            public Action onClosed;
            public string[] chips;
        }

        readonly Queue<Line> blockingQueue = new Queue<Line>();
        Line current;
        Line pendingPassing;
        int lastMorningDay;

        public bool IsShowingDialogue { get; private set; }

        /// <summary>True while a blocking line is up.</summary>
        public bool IsBlocking => current != null && current.mode == MaliLineMode.Blocking;

        void Awake()
        {
            if (dialogueView == null)
            {
                dialogueView = GetComponent<MaliDialogueView>();
            }

            if (dialogueView == null)
            {
                dialogueView = gameObject.AddComponent<MaliDialogueView>();
            }

            if (maliController == null)
            {
                maliController = GetComponent<MaliNpcController>();
            }

            dialogueView.HideImmediate();
            GameEvents.DayStarted += HandleDayStarted;
        }

        void OnDestroy()
        {
            GameEvents.DayStarted -= HandleDayStarted;
        }

        // ================================================================ lines

        /// <summary>
        /// Shows a line (already filled). <paramref name="onClosed"/> runs when the line is closed, or when it is
        /// dropped by <see cref="Hide"/>. <paramref name="chips"/> (blocking only) sit in the name-tag row.
        /// </summary>
        public void Say(string text, MaliLineMode mode, Action onClosed = null, string[] chips = null)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                onClosed?.Invoke();
                return;
            }

            if (mode == MaliLineMode.Passing && !dialogueView.FitsPassing(text))
            {
                mode = MaliLineMode.Blocking;
            }

            var line = new Line { text = text, mode = mode, onClosed = onClosed, chips = chips };

            if (mode == MaliLineMode.Blocking)
            {
                if (current == null)
                {
                    Present(line);
                }
                else if (current.mode == MaliLineMode.Passing)
                {
                    Line superseded = current;
                    current = null;
                    superseded.onClosed?.Invoke();
                    Present(line);
                }
                else
                {
                    blockingQueue.Enqueue(line);
                }

                return;
            }

            if (current == null)
            {
                Present(line);
            }
            else if (current.mode == MaliLineMode.Passing)
            {
                Line replaced = current;
                current = null;
                replaced.onClosed?.Invoke();
                Present(line);
            }
            else
            {
                Line dropped = pendingPassing;
                pendingPassing = line;
                dropped?.onClosed?.Invoke();
            }
        }

        /// <summary>A passing line (Say(line, Passing)).</summary>
        public void ShowLine(string line)
        {
            Say(line, MaliLineMode.Passing);
        }

        /// <summary>
        /// Fills the template with MaliText.Fill ({name}, legacy {0} and every other 4.1 token) and shows it as a
        /// passing line. Without player data, args[0] stands in for {name}/{0}.
        /// </summary>
        public void ShowFormatted(string template, params object[] args)
        {
            ShowLine(Fill(template, args));
        }

        /// <summary>Talk to Mali: MaliGreeting.Build, {owed} formatted with MoneyFormat.Digits, then MaliText.Fill (blocking).</summary>
        public void ShowGreeting()
        {
            PlayerData player = PlayerDataAccess.GetCurrentPlayer();
            bool firstMeeting = player != null && !player.hasMetMali;
            string template = MaliGreeting.Build(player, MaliGoFeatures.ChapterSchedule, out Dictionary<string, string> extra);
            if (extra.TryGetValue("owed", out string owedRaw)
                && float.TryParse(owedRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out float owed))
            {
                extra["owed"] = MoneyFormat.Digits(owed);
            }

            Say(MaliText.Fill(template, player, extra), MaliLineMode.Blocking);

            if (firstMeeting)
            {
                MarkMetMali();
            }
        }

        static void MarkMetMali()
        {
            if (PlayerDataManager.Instance == null)
            {
                return;
            }

            PlayerDataManager.Instance.UpdatePlayerData(data => data.hasMetMali = true, saveImmediately: true);
        }

        /// <summary>Closes the line that is up and drops every queued one (their close callbacks still run).</summary>
        public void Hide()
        {
            var dropped = new List<Line>(blockingQueue);
            blockingQueue.Clear();
            if (pendingPassing != null)
            {
                dropped.Add(pendingPassing);
                pendingPassing = null;
            }

            Line closing = current;
            current = null;
            dialogueView.HideImmediate();

            closing?.onClosed?.Invoke();
            foreach (Line line in dropped)
            {
                line.onClosed?.Invoke();
            }

            Finish();
        }

        void Present(Line line)
        {
            current = line;
            IsShowingDialogue = true;
            maliController?.SetBehaviourState(MaliBehaviourState.TALK);

            if (line.mode == MaliLineMode.Blocking)
            {
                dialogueView.ShowBlocking(line.text, line.chips, () => HandleViewClosed(line));
            }
            else
            {
                dialogueView.ShowPassing(line.text, () => HandleViewClosed(line));
            }

            OnDialogueShown?.Invoke(line.text);
            GameEvents.RaiseMaliSpoke();
        }

        void HandleViewClosed(Line line)
        {
            if (!ReferenceEquals(current, line))
            {
                return;
            }

            current = null;
            line.onClosed?.Invoke();

            if (current != null)
            {
                return; // the callback said something new
            }

            if (blockingQueue.Count > 0)
            {
                Present(blockingQueue.Dequeue());
                return;
            }

            if (pendingPassing != null)
            {
                Line next = pendingPassing;
                pendingPassing = null;
                Present(next);
                return;
            }

            Finish();
        }

        void Finish()
        {
            if (current != null)
            {
                return;
            }

            bool wasShowing = IsShowingDialogue;
            IsShowingDialogue = false;

            if (maliController != null && maliController.CurrentState == MaliBehaviourState.TALK)
            {
                MaliBehaviourState resumeState = maliController.FollowPlayerEnabled
                    ? MaliBehaviourState.FOLLOW
                    : MaliBehaviourState.IDLE;
                maliController.SetBehaviourState(resumeState);
            }

            if (wasShowing)
            {
                OnDialogueHidden?.Invoke();
            }
        }

        // ================================================================ morning line

        void HandleDayStarted(int day)
        {
            PlayerData player = PlayerDataAccess.GetCurrentPlayer();
            if (player == null || day == lastMorningDay)
            {
                return;
            }

            lastMorningDay = day;

            if (day == 1 && player.chapter != null && player.chapter.runNumber > 1)
            {
                string replay = string.IsNullOrWhiteSpace(player.paydayPlanText) ? ReplayWithoutPlan : ReplayWithPlan;
                Say(MaliText.Fill(replay, player), MaliLineMode.Blocking);
                return;
            }

            string focus = player.spendingProfile != null ? player.spendingProfile.focus : null;
            string morning = ChapterSchedule.MorningLine(day, focus);
            if (string.IsNullOrEmpty(morning))
            {
                return;
            }

            if (player.financialStats != null && player.financialStats.financialStress >= ChapterConfig.StretchedStress)
            {
                morning = StretchedMorningPrefix + morning;
            }

            Say(MaliText.Fill(morning, player), MaliLineMode.Passing);
        }

        static string Fill(string template, object[] args)
        {
            PlayerData player = PlayerDataAccess.GetCurrentPlayer();
            Dictionary<string, string> extra = null;
            if (player == null && args != null && args.Length > 0 && args[0] != null)
            {
                string name = Convert.ToString(args[0], CultureInfo.InvariantCulture);
                extra = new Dictionary<string, string> { { "name", name }, { "0", name } };
            }

            return MaliText.Fill(template ?? "", player, extra);
        }

        // --- Future trigger entry points (stub implementations) ---

        public void TriggerFromArea(string areaId)
        {
            Debug.Log($"[MaliDialogue] Area trigger reserved for future use: {areaId}");
        }

        public void TriggerFromInteraction(string interactionId)
        {
            Debug.Log($"[MaliDialogue] Interaction trigger reserved for future use: {interactionId}");
        }

        public void TriggerFromFinancialEvent(string eventId)
        {
            Debug.Log($"[MaliDialogue] Financial event trigger reserved for future use: {eventId}");
        }

        public void TriggerFromQuest(string questId)
        {
            Debug.Log($"[MaliDialogue] Quest trigger reserved for future use: {questId}");
        }

        public void TriggerFromLifeChapterTransition(string chapterId)
        {
            Debug.Log($"[MaliDialogue] Life chapter trigger reserved for future use: {chapterId}");
        }

        public void TriggerFromFinancialBehaviour(string behaviourId)
        {
            Debug.Log($"[MaliDialogue] Financial behaviour trigger reserved for future use: {behaviourId}");
        }
    }
}
