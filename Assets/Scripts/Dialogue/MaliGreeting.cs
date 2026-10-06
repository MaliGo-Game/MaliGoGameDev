using System.Collections.Generic;
using System.Globalization;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.Scenarios;

namespace MaliGo.Dialogue
{
    /// <summary>
    /// What Mali says when the player talks to her (design spec 4.2.1): [stretched prefix] + what is waiting
    /// today + [one suffix]. Pure. Returns the template with {places} and {count} already substituted;
    /// {owed}, {label} and {gate} come back raw in <c>extra</c> (owed as an invariant-culture float string). The
    /// caller formats owed with MoneyFormat.Digits and fills everything with MaliText.Fill (7.7).
    /// </summary>
    public static class MaliGreeting
    {
        public const string StretchedPrefix = "You seem stretched, {name}. ";
        public const string NothingWaiting = "That's everything for today, {name}. Sleep at home when you're ready.";
        public const string OneWaiting = "One thing is waiting today, {places}.";
        public const string TwoWaiting = "Two things are waiting today, {places}.";
        public const string ManyWaiting = "{count} things are waiting today, {places}.";
        public const string OwedSuffix = " R{owed} is still owed for {label}.";
        public const string NotYetSuffix = " The shift opens after {gate}.";
        public const string ShiftSuffix = " There's a shift going at the far end of the main road.";
        public const string TiredSuffix = " You're too tired for a shift today.";

        public static string Build(PlayerData data, bool useSchedule, out Dictionary<string, string> extra)
        {
            extra = new Dictionary<string, string>();
            if (data == null)
            {
                return NothingWaiting;
            }

            if (!data.hasMetMali)
            {
                MaliDialogueEntry first = MaliDialogueLibrary.FindById(MaliDialogueLibrary.FirstMeetingId);
                if (first != null && !string.IsNullOrEmpty(first.line))
                {
                    return first.line;
                }
            }

            string focus = data.spendingProfile != null ? data.spendingProfile.focus : null;
            string travel = data.spendingProfile != null ? data.spendingProfile.travel : null;

            string prefix = data.financialStats != null && data.financialStats.financialStress >= ChapterConfig.StretchedStress
                ? StretchedPrefix
                : "";

            // Spot queue heads, in the order ActiveScenarioIds gives them (today's gate first).
            var spots = new List<string>();
            foreach (string id in ChapterSchedule.ActiveScenarioIds(data, useSchedule, focus, data.followUps))
            {
                string spot = ChapterSchedule.SpotFor(id);
                if (spot != null && !spots.Contains(spot))
                {
                    spots.Add(spot);
                }
            }

            var places = new List<string>();
            foreach (string spot in spots)
            {
                places.Add(ChapterSchedule.SpotWhere(spot, focus, travel));
            }

            string waiting;
            switch (places.Count)
            {
                case 0:
                    waiting = NothingWaiting;
                    break;
                case 1:
                    waiting = OneWaiting;
                    break;
                case 2:
                    waiting = TwoWaiting;
                    break;
                default:
                    waiting = ManyWaiting.Replace("{count}", places.Count.ToString(CultureInfo.InvariantCulture));
                    break;
            }

            waiting = waiting.Replace("{places}", JoinPlaces(places));
            return prefix + waiting + Suffix(data, useSchedule, focus, travel, extra);
        }

        /// <summary>"A", "A and B", "A, B and C".</summary>
        public static string JoinPlaces(IList<string> names)
        {
            if (names == null || names.Count == 0)
            {
                return "";
            }

            if (names.Count == 1)
            {
                return names[0];
            }

            var head = new List<string>();
            for (int i = 0; i < names.Count - 1; i++)
            {
                head.Add(names[i]);
            }

            return string.Join(", ", head) + " and " + names[names.Count - 1];
        }

        static string Suffix(PlayerData data, bool useSchedule, string focus, string travel, Dictionary<string, string> extra)
        {
            Obligation owed = ObligationLedger.LargestArrears(data);
            if (owed != null && owed.arrears > MoneyRecorder.Tolerance)
            {
                extra["owed"] = owed.arrears.ToString(CultureInfo.InvariantCulture);
                extra["label"] = string.IsNullOrEmpty(owed.shortLabel) ? owed.label ?? "" : owed.shortLabel;
                return OwedSuffix;
            }

            string gateId = useSchedule ? ChapterSchedule.GateScenario(data.currentDay, focus) : null;
            if (WorkRules.State(data, gateId) == ShiftState.NotYet && !string.IsNullOrEmpty(gateId))
            {
                ScenarioDefinition gate = ScenarioLibrary.Get(gateId, focus, travel);
                extra["gate"] = gate != null ? gate.gateNoun ?? "" : "";
                return NotYetSuffix;
            }

            if (WorkRules.HasWorkedToday(data))
            {
                return "";
            }

            return WorkRules.HasEnergy(data) ? ShiftSuffix : TiredSuffix;
        }
    }
}
