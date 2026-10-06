using System.Collections.Generic;
using MaliGo.Characters;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.Scenarios;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Work (DESIGN_SPEC §7.1, §4.2.2, §4.8): one shift a day through <see cref="WorkRules"/>, opening only after
    /// the day's gate scenario (A1). The prompt is visible in every state so the player can always find Work and
    /// read why it is shut: Open = "Take a shift (+R150)"; NotYet = "Shift opens after {gate}"; Done = "Shift done
    /// for today"; Tired = "Too tired for a shift". A tap on a shut Work calls <see cref="OnDisabledTap"/>, which
    /// has Mali say why (Passing).
    /// </summary>
    public class WorkInteraction : ProximityInteraction
    {
        const string OpenPrompt = "Take a shift (+R150)";
        const string NotYetPrompt = "Shift opens after {gate}";
        const string DonePrompt = "Shift done for today";
        const string TiredPrompt = "Too tired for a shift";

        const string ShiftDoneLine = "R150 for the shift, {name}.";
        const string AlreadyWorkedLine = "That's today's shift done. There's another one tomorrow.";
        const string TiredLine = "A shift needs 60 energy, and you've got {energy}. Sleep brings it back to 100.";
        const string NotYetLine = "The shift opens after {gate}. A shift needs 60 energy.";

        int stateFrame = -1;
        ShiftState state = ShiftState.NotYet;
        string gateScenarioId;
        string gateNoun = "";
        bool hasData;

        protected override void OnAwake()
        {
            SetPrompt(OpenPrompt);
            SetVerbAndIcon("Work", "wrench");
        }

        public override bool IsEnabled
        {
            get
            {
                Refresh();
                return hasData && state == ShiftState.Open;
            }
        }

        public override string PromptText
        {
            get
            {
                Refresh();
                if (!hasData)
                {
                    return OpenPrompt;
                }

                switch (state)
                {
                    case ShiftState.Open: return OpenPrompt;
                    case ShiftState.Done: return DonePrompt;
                    case ShiftState.Tired: return TiredPrompt;
                    default: return NotYetPrompt.Replace("{gate}", gateNoun);
                }
            }
        }

        protected override void OnInteract()
        {
            if (PlayerDataManager.Instance == null)
            {
                return;
            }

            stateFrame = -1;
            Refresh();
            if (!hasData || state != ShiftState.Open)
            {
                OnDisabledTap();
                return;
            }

            string gate = gateScenarioId;
            MoneyEvent pay = null;
            PlayerDataManager.Instance.UpdatePlayerData(data => pay = WorkRules.DoShift(data, gate), saveImmediately: true);
            stateFrame = -1;

            if (pay != null)
            {
                Say(ShiftDoneLine, null);
            }
        }

        public override void OnDisabledTap()
        {
            stateFrame = -1;
            Refresh();
            if (!hasData)
            {
                return;
            }

            switch (state)
            {
                case ShiftState.Done:
                    Say(AlreadyWorkedLine, null);
                    break;
                case ShiftState.Tired:
                    Say(TiredLine, null);
                    break;
                case ShiftState.NotYet:
                    Say(NotYetLine, new Dictionary<string, string> { { "gate", gateNoun } });
                    break;
            }
        }

        /// <summary>Reads <see cref="WorkRules.State"/> at most once per frame.</summary>
        void Refresh()
        {
            if (stateFrame == Time.frameCount)
            {
                return;
            }

            stateFrame = Time.frameCount;
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            hasData = data != null;
            if (!hasData)
            {
                gateScenarioId = null;
                gateNoun = "";
                state = ShiftState.NotYet;
                return;
            }

            string focus = data.spendingProfile?.focus;
            string gate = MaliGoFeatures.ChapterSchedule
                ? ChapterSchedule.GateScenario(data.currentDay, data.spendingProfile?.focus)
                : null;
            if (gate != gateScenarioId)
            {
                gateScenarioId = gate;
                ScenarioDefinition definition = string.IsNullOrEmpty(gate)
                    ? null
                    : ScenarioLibrary.Get(gate, focus, data.spendingProfile?.travel);
                gateNoun = definition != null ? definition.gateNoun ?? "" : "";
            }

            state = WorkRules.State(data, gateScenarioId);
        }

        static void Say(string template, IDictionary<string, string> extra)
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            MaliDialogueController dialogue = FindMaliDialogue();
            if (dialogue == null)
            {
                return;
            }

            // Filled here so the line is final whichever dialogue version is in the build; ShowLine = Passing.
            dialogue.ShowLine(MaliText.Fill(template, data, extra));
        }

        static MaliDialogueController FindMaliDialogue()
        {
            GameObject maliObject = GameObject.Find("Mali");
            MaliDialogueController dialogue = maliObject != null ? maliObject.GetComponent<MaliDialogueController>() : null;
            return dialogue != null ? dialogue : FindFirstObjectByType<MaliDialogueController>();
        }
    }
}
