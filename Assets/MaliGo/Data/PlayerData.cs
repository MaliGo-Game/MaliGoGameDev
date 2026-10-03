using System;

namespace MaliGo.Data
{
    [Serializable]
    public class PlayerData
    {
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
                isCharacterCreated = false
            };
            ObligationDefaults.AddBaseObligations(data);
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
