using System;
using MaliGo.Data;

namespace MaliGo.Economy
{
    /// <summary>Starting and restarting a run of the chapter (design spec 7.3).</summary>
    public static class ChapterFlow
    {
        /// <summary>
        /// New run: cash 600, savings 400, energy 100, stress 25, behaviour scores 0.5, obligations =
        /// defaults (airtime Day 2, rent Day 3), completedScenarioIds empty, followUps empty, currentDay 1,
        /// lastWorkedDay 0, revealPendingForDay 0, morningLineDay 0, chapter = { chapterNumber 1,
        /// runNumber, startCash 600, startSavings 400 }, today opened, saveVersion = 2. Keeps name,
        /// appearance, goals, paydayPlanId/Text, hasMetMali and spendingProfile.
        /// This (with FinancialStats.CreateDefaults) is the only place besides MoneyRecorder that sets
        /// cash and savings: the start of a run.
        /// </summary>
        public static void StartChapter(PlayerData data, int runNumber)
        {
            if (data == null)
            {
                return;
            }

            data.financialStats ??= new FinancialStats();
            FinancialStats stats = data.financialStats;
            stats.cash = ChapterConfig.StartCash;
            stats.savings = ChapterConfig.StartSavings;
            stats.energy = ChapterConfig.DailyEnergy;
            stats.financialStress = ChapterConfig.StartStress;
            stats.spendingBehaviourScore = 0.5f;
            stats.savingBehaviourScore = 0.5f;

            data.obligations = Array.Empty<Obligation>();
            data.baseObligationsAdded = false;
            ObligationDefaults.AddBaseObligations(data);

            data.completedScenarioIds = Array.Empty<string>();
            data.followUps = Array.Empty<string>();
            data.currentDay = 1;
            data.lastWorkedDay = 0;
            data.revealPendingForDay = 0;
            data.morningLineDay = 0;

            data.chapter = new ChapterRecord
            {
                chapterNumber = 1,
                runNumber = Math.Max(1, runNumber),
                startCash = ChapterConfig.StartCash,
                startSavings = ChapterConfig.StartSavings
            };

            data.today = new DayRecord();
            MoneyRecorder.OpenDay(data);

            data.saveVersion = PlayerData.CurrentSaveVersion;

            // Kept as they are, but never left null.
            data.spendingProfile ??= new SpendingProfile();
            data.paydayPlanId ??= "";
            data.paydayPlanText ??= "";
            data.goals ??= Array.Empty<FinancialGoal>();
        }

        /// <summary>"Live the week again": StartChapter(data, chapter.runNumber + 1).</summary>
        public static void RestartChapter(PlayerData data)
        {
            if (data == null)
            {
                return;
            }

            int run = data.chapter != null ? data.chapter.runNumber : 0;
            StartChapter(data, run + 1);
        }
    }
}
