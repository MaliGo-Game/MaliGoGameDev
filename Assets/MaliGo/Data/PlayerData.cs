using System;

namespace MaliGo.Data
{
    [Serializable]
    public class PlayerData
    {
        /// <summary>Saves with a lower saveVersion are discarded on load (a fresh start, nothing kept).</summary>
        public const int CurrentSaveVersion = 2;

        /// <summary>
        /// NO initialiser on purpose: JsonUtility runs field initialisers, so an old save without this
        /// field must read as 0. Set by CreateNew / ChapterFlow.StartChapter.
        /// </summary>
        public int saveVersion;

        public string characterName = "";
        public AppearanceData appearance = new AppearanceData();
        public FinancialProfile financialProfile = new FinancialProfile();
        public LifeChapter currentLifeChapter = LifeChapter.YOUNG_PROFESSIONAL;
        public FinancialStats financialStats = new FinancialStats();
        public FinancialGoal[] goals = Array.Empty<FinancialGoal>();
        public ProgressionData progression = new ProgressionData();
        public bool isCharacterCreated;
        public string[] completedScenarioIds = Array.Empty<string>();
        public bool hasMetMali;

        /// <summary>In-game day, starting at 1. Advanced by sleeping at Home.</summary>
        public int currentDay = 1;

        /// <summary>Last in-game day the player took a shift at Work (one per day).</summary>
        public int lastWorkedDay;

        public Obligation[] obligations = Array.Empty<Obligation>();

        /// <summary>
        /// True once the default bills (rent, airtime) have been added. Lets saves made
        /// before bills existed pick them up on load without adding them twice.
        /// </summary>
        public bool baseObligationsAdded;

        /// <summary>The open day; today.day == currentDay while playing.</summary>
        public DayRecord today = new DayRecord();

        /// <summary>The current run of the chapter.</summary>
        public ChapterRecord chapter = new ChapterRecord();

        /// <summary>PaydayPlans id; survives "Live the week again".</summary>
        public string paydayPlanId = "";

        /// <summary>Snapshot of the plan sentence.</summary>
        public string paydayPlanText = "";

        /// <summary>&gt; 0: the reveal for that day has not been dismissed.</summary>
        public int revealPendingForDay;

        /// <summary>The day whose morning line (and DayStarted) already ran.</summary>
        public int morningLineDay;

        /// <summary>Kept by ChapterFlow.StartChapter, wiped only by Start over (a new save).</summary>
        public SpendingProfile spendingProfile = new SpendingProfile();

        /// <summary>
        /// Follow-up scenarios set off by a choice, as keys "scenarioId@day" (ChapterSchedule.FollowUpKey).
        /// Appended by ScenarioOutcome.Apply, emptied by ChapterFlow.StartChapter.
        /// </summary>
        public string[] followUps = Array.Empty<string>();

        /// <summary>True when a save of this version must be discarded on load.</summary>
        public static bool ShouldReset(int saveVersion) => saveVersion < CurrentSaveVersion;

        /// <summary>
        /// A new save: the object as before, then ChapterFlow.StartChapter(data, 1), so every new save is a
        /// valid Day-1 state (cash 600, savings 400, today opened, saveVersion = CurrentSaveVersion,
        /// default spending profile).
        /// </summary>
        public static PlayerData CreateNew()
        {
            var data = new PlayerData
            {
                characterName = "",
                appearance = new AppearanceData(),
                financialProfile = new FinancialProfile(),
                currentLifeChapter = LifeChapter.YOUNG_PROFESSIONAL,
                financialStats = FinancialStats.CreateDefaults(LifeChapter.YOUNG_PROFESSIONAL),
                goals = new[] { new FinancialGoal() },
                progression = new ProgressionData(),
                isCharacterCreated = false,
                spendingProfile = new SpendingProfile(),
                followUps = Array.Empty<string>()
            };
            MaliGo.Economy.ChapterFlow.StartChapter(data, 1);
            return data;
        }

        public bool IsScenarioCompleted(string scenarioId)
        {
            return completedScenarioIds != null && Array.IndexOf(completedScenarioIds, scenarioId) >= 0;
        }

        public FinancialGoal GetPrimaryGoal()
        {
            if (goals == null || goals.Length == 0)
            {
                return new FinancialGoal();
            }

            return goals[0];
        }
    }
}
