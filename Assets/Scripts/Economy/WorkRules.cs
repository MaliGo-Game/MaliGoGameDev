using System;
using MaliGo.Data;

namespace MaliGo.Economy
{
    /// <summary>Whether a shift can be taken now (design spec 7.3, A1).</summary>
    public enum ShiftState
    {
        Open,
        NotYet,
        Tired,
        Done
    }

    /// <summary>The shift at Work: +R150 cash, needs and uses 60 energy, once per day, after the day's gate.</summary>
    public static class WorkRules
    {
        public static bool HasWorkedToday(PlayerData d)
        {
            return d != null && d.lastWorkedDay >= d.currentDay;
        }

        /// <summary>Energy &gt;= 60.</summary>
        public static bool HasEnergy(PlayerData d)
        {
            return d?.financialStats != null && d.financialStats.energy >= ChapterConfig.ShiftEnergyCost - 0.005f;
        }

        /// <summary>
        /// gateScenarioId = today's first scheduled scenario (ChapterSchedule.GateScenario, passed in by the
        /// caller), or null/empty when there is no gate (schedule flag off). First that applies:
        /// Done (worked today) &gt; NotYet (gate not in completedScenarioIds) &gt; Tired (energy &lt; 60) &gt; Open.
        /// </summary>
        public static ShiftState State(PlayerData d, string gateScenarioId)
        {
            if (d == null)
            {
                return ShiftState.NotYet;
            }

            if (HasWorkedToday(d))
            {
                return ShiftState.Done;
            }

            if (!string.IsNullOrEmpty(gateScenarioId) && !d.IsScenarioCompleted(gateScenarioId))
            {
                return ShiftState.NotYet;
            }

            if (!HasEnergy(d))
            {
                return ShiftState.Tired;
            }

            return ShiftState.Open;
        }

        /// <summary>
        /// Null (and no change) unless State == Open. Otherwise +R150 through the recorder ("Shift at work",
        /// category Work, source "work:&lt;day&gt;"), energy -60, XP +5, lastWorkedDay = today,
        /// chapter.shiftsWorked + 1.
        /// </summary>
        public static MoneyEvent DoShift(PlayerData d, string gateScenarioId)
        {
            if (State(d, gateScenarioId) != ShiftState.Open)
            {
                return null;
            }

            MoneyEvent pay = MoneyRecorder.Apply(d, ChapterConfig.ShiftPay, 0f, "Shift at work", MoneyCategory.Work,
                                                 "work:" + d.currentDay);
            if (pay == null)
            {
                return null;
            }

            FinancialStats stats = d.financialStats;
            stats.energy = Math.Max(0f, Math.Min(100f, stats.energy - ChapterConfig.ShiftEnergyCost));
            stats.financialXP += 5f;
            d.lastWorkedDay = d.currentDay;
            d.chapter ??= new ChapterRecord();
            d.chapter.shiftsWorked++;
            return pay;
        }
    }
}
