using System;

namespace MaliGo.Data
{
    /// <summary>One scenario choice the player made during the chapter.</summary>
    [Serializable]
    public class ChoiceRecord
    {
        public int day;
        public string scenarioId = "";
        public string choiceId = "";

        /// <summary>ScenarioBehaviourTag name: "Neutral" | "Frugal" | "Discretionary" | "Deferred".</summary>
        public string tag = "";
    }

    /// <summary>The current run of a chapter: its closed days, choices and shifts.</summary>
    [Serializable]
    public class ChapterRecord
    {
        public int chapterNumber = 1;

        /// <summary>1 on first play, +1 per "Live the week again".</summary>
        public int runNumber = 1;

        public float startCash;
        public float startSavings;

        /// <summary>Closed days, in order (at most 7).</summary>
        public DayRecord[] days = Array.Empty<DayRecord>();

        public ChoiceRecord[] choices = Array.Empty<ChoiceRecord>();
        public int shiftsWorked;

        /// <summary>
        /// Set on the night of Day 7. While true the world always shows the chapter end until
        /// "Live the week again".
        /// </summary>
        public bool complete;

        public float StartTotal => startCash + startSavings;
    }
}
