using System;
using MaliGo.Data;

namespace MaliGo.Economy
{
    /// <summary>What one night did. Built fresh by DayCycle.EndDay, or from the save by DayCycle.Rebuild.</summary>
    public class NightResult
    {
        public int endedDay;

        /// <summary>The closed day.</summary>
        public DayRecord record;

        /// <summary>What the night charged; NULL when rebuilt from the save, so nothing may depend on it alone.</summary>
        public ObligationSettlement settlement;

        public bool chapterComplete;

        /// <summary>== endedDay when chapterComplete.</summary>
        public int newDay;
    }

    /// <summary>The night: bills, closing the day, and opening the next one (design spec 7.3).</summary>
    public static class DayCycle
    {
        /// <summary>
        /// Null (and no change) if today is already closed, the chapter is complete, or
        /// today.day != currentDay. Otherwise, in this order:
        /// 1. settlement = ObligationLedger.SettleDue(data, data.currentDay) (due Day N -> night of Day N)
        /// 2. record = MoneyRecorder.CloseDay(data); owedAtClose = ObligationLedger.TotalArrears(data)
        ///    (on both data.today and the copy appended to chapter.days)
        /// 3. data.revealPendingForDay = endedDay
        /// 4. if currentDay &gt;= ChapterConfig.ChapterLength: chapter.complete = true (currentDay stays 7)
        ///    else: currentDay += 1; energy = 100; MoneyRecorder.OpenDay(data)
        /// </summary>
        public static NightResult EndDay(PlayerData data)
        {
            if (data?.financialStats == null || data.today == null)
            {
                return null;
            }

            data.chapter ??= new ChapterRecord();
            if (data.today.closed || data.chapter.complete || data.today.day != data.currentDay)
            {
                return null;
            }

            int endedDay = data.currentDay;

            ObligationSettlement settlement = ObligationLedger.SettleDue(data, endedDay);

            DayRecord record = MoneyRecorder.CloseDay(data);
            if (record == null)
            {
                return null;
            }

            float owed = ObligationLedger.TotalArrears(data);
            record.owedAtClose = owed;
            data.today.owedAtClose = owed;

            data.revealPendingForDay = endedDay;

            bool complete = endedDay >= ChapterConfig.ChapterLength;
            if (complete)
            {
                data.chapter.complete = true;
            }
            else
            {
                data.currentDay = endedDay + 1;
                data.financialStats.energy = ChapterConfig.DailyEnergy;
                MoneyRecorder.OpenDay(data);
            }

            return new NightResult
            {
                endedDay = endedDay,
                record = record,
                settlement = settlement,
                chapterComplete = complete,
                newDay = complete ? endedDay : endedDay + 1
            };
        }

        /// <summary>
        /// Rebuilds the result of an earlier night from the save alone: record = the last chapter.days
        /// entry with day == endedDay, settlement = null, chapterComplete = data.chapter.complete &amp;&amp;
        /// endedDay &gt;= ChapterConfig.ChapterLength, newDay = chapterComplete ? endedDay : endedDay + 1.
        /// Null if no record. Changes nothing.
        /// </summary>
        public static NightResult Rebuild(PlayerData data, int endedDay)
        {
            DayRecord[] days = data?.chapter?.days;
            if (days == null || endedDay <= 0)
            {
                return null;
            }

            DayRecord record = null;
            for (int i = days.Length - 1; i >= 0; i--)
            {
                if (days[i] != null && days[i].day == endedDay)
                {
                    record = days[i];
                    break;
                }
            }

            if (record == null)
            {
                return null;
            }

            bool complete = data.chapter.complete && endedDay >= ChapterConfig.ChapterLength;
            return new NightResult
            {
                endedDay = endedDay,
                record = record,
                settlement = null,
                chapterComplete = complete,
                newDay = complete ? endedDay : endedDay + 1
            };
        }
    }
}
