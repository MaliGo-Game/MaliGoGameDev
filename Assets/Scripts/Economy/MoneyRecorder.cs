using System;
using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.Economy
{
    /// <summary>
    /// The only writer of cash and savings (design spec 2.4). Every money change in the game goes
    /// through Apply, which records it in the open day, so the end-of-day reveal can always show
    /// exactly what moved the money.
    /// </summary>
    public static class MoneyRecorder
    {
        /// <summary>Tolerance for float comparisons of Rand amounts.</summary>
        public const float Tolerance = 0.005f;

        /// <summary>True if applying the deltas leaves both pools &gt;= 0 (tolerance 0.005).</summary>
        public static bool CanAfford(PlayerData data, float cashDelta, float savingsDelta)
        {
            if (data?.financialStats == null)
            {
                return false;
            }

            return data.financialStats.cash + cashDelta >= -Tolerance
                && data.financialStats.savings + savingsDelta >= -Tolerance;
        }

        /// <summary>
        /// Applies the deltas to data.financialStats, appends a MoneyEvent to data.today.events,
        /// queues GameEvents.MoneyChanged, returns the event. Never clamps: if the result would make a
        /// pool negative it logs an error, changes nothing and returns null. Zero deltas -> null.
        /// Precondition: data.today.day == data.currentDay &amp;&amp; !data.today.closed. Apply NEVER opens a
        /// day itself: if the precondition fails it logs an error, changes nothing and returns null.
        /// Days are opened only by ChapterFlow.StartChapter and DayCycle.EndDay.
        /// </summary>
        public static MoneyEvent Apply(PlayerData data, float cashDelta, float savingsDelta,
                                       string label, string category, string sourceId)
        {
            if (data?.financialStats == null)
            {
                Debug.LogError("[MoneyRecorder] Apply called without player data.");
                return null;
            }

            if (Math.Abs(cashDelta) < Tolerance && Math.Abs(savingsDelta) < Tolerance)
            {
                return null;
            }

            if (data.today == null || data.today.day != data.currentDay || data.today.closed)
            {
                Debug.LogError($"[MoneyRecorder] No open day for Day {data.currentDay} " +
                               $"(today = {(data.today == null ? "none" : data.today.day + (data.today.closed ? ", closed" : ""))}); " +
                               $"'{label}' ({sourceId}) not applied.");
                return null;
            }

            if (!CanAfford(data, cashDelta, savingsDelta))
            {
                Debug.LogError($"[MoneyRecorder] '{label}' ({sourceId}) would leave cash {data.financialStats.cash + cashDelta} / " +
                               $"savings {data.financialStats.savings + savingsDelta}; not applied.");
                return null;
            }

            float total = cashDelta + savingsDelta;
            var moneyEvent = new MoneyEvent
            {
                label = label ?? "",
                category = category ?? "",
                cashDelta = cashDelta,
                savingsDelta = savingsDelta,
                kind = total > Tolerance ? MoneyEventKind.In
                     : total < -Tolerance ? MoneyEventKind.Out
                     : MoneyEventKind.Transfer,
                sourceId = sourceId ?? ""
            };

            data.financialStats.cash += cashDelta;
            data.financialStats.savings += savingsDelta;

            var events = new List<MoneyEvent>(data.today.events ?? Array.Empty<MoneyEvent>()) { moneyEvent };
            data.today.events = events.ToArray();

            GameEvents.QueueMoneyChanged(moneyEvent);
            return moneyEvent;
        }

        /// <summary>
        /// Opens the record for data.currentDay if today.day != currentDay: startCash/startSavings =
        /// current stats, events empty, closed false.
        /// </summary>
        public static void OpenDay(PlayerData data)
        {
            if (data?.financialStats == null)
            {
                return;
            }

            if (data.today != null && data.today.day == data.currentDay)
            {
                return;
            }

            data.today = new DayRecord
            {
                day = data.currentDay,
                startCash = data.financialStats.cash,
                startSavings = data.financialStats.savings,
                events = Array.Empty<MoneyEvent>(),
                closed = false
            };
        }

        /// <summary>
        /// Sets endCash/endSavings from current stats, closed = true, appends a copy to
        /// data.chapter.days and returns that copy. No-op (returns null) if already closed or no day is
        /// open.
        /// </summary>
        public static DayRecord CloseDay(PlayerData data)
        {
            if (data?.financialStats == null || data.today == null || data.today.day == 0 || data.today.closed)
            {
                return null;
            }

            data.today.endCash = data.financialStats.cash;
            data.today.endSavings = data.financialStats.savings;
            data.today.closed = true;

            data.chapter ??= new ChapterRecord();
            DayRecord copy = data.today.Clone();
            var days = new List<DayRecord>(data.chapter.days ?? Array.Empty<DayRecord>()) { copy };
            data.chapter.days = days.ToArray();
            return copy;
        }

        /// <summary>
        /// The invariant (2.5). For every closed day and each pool: start + sum of deltas == end, days
        /// continue from one another and from the chapter start. For today: start + sum == the stats
        /// (or, once closed, end == the stats). Returns null if it holds, else the first violation.
        /// </summary>
        public static string CheckInvariant(PlayerData data)
        {
            if (data?.financialStats == null)
            {
                return "no player data";
            }

            ChapterRecord chapter = data.chapter ?? new ChapterRecord();
            DayRecord[] days = chapter.days ?? Array.Empty<DayRecord>();
            float prevCash = chapter.startCash;
            float prevSavings = chapter.startSavings;
            string prevName = "chapter start";

            for (int i = 0; i < days.Length; i++)
            {
                DayRecord record = days[i];
                if (record == null)
                {
                    return $"chapter.days[{i}] is null";
                }

                string name = $"Day {record.day}";
                if (!record.closed)
                {
                    return $"{name} in chapter.days is not closed";
                }

                if (!Near(record.startCash, prevCash) || !Near(record.startSavings, prevSavings))
                {
                    return $"{name} starts at cash {record.startCash} / savings {record.startSavings}, " +
                           $"but {prevName} ended at cash {prevCash} / savings {prevSavings}";
                }

                Sum(record, out float cashSum, out float savingsSum);
                if (!Near(record.startCash + cashSum, record.endCash))
                {
                    return $"{name}: cash {record.startCash} + events {cashSum} != end {record.endCash}";
                }

                if (!Near(record.startSavings + savingsSum, record.endSavings))
                {
                    return $"{name}: savings {record.startSavings} + events {savingsSum} != end {record.endSavings}";
                }

                prevCash = record.endCash;
                prevSavings = record.endSavings;
                prevName = name;
            }

            DayRecord today = data.today;
            if (today == null || today.day == 0)
            {
                return null;
            }

            FinancialStats stats = data.financialStats;
            DayRecord last = days.Length > 0 ? days[days.Length - 1] : null;
            bool todayIsLast = last != null && last.day == today.day;

            if (today.closed)
            {
                if (!Near(today.endCash, stats.cash) || !Near(today.endSavings, stats.savings))
                {
                    return $"Day {today.day} is closed at cash {today.endCash} / savings {today.endSavings}, " +
                           $"but the stats are cash {stats.cash} / savings {stats.savings}";
                }

                return null;
            }

            if (!todayIsLast && (!Near(today.startCash, prevCash) || !Near(today.startSavings, prevSavings)))
            {
                return $"Day {today.day} (open) starts at cash {today.startCash} / savings {today.startSavings}, " +
                       $"but {prevName} ended at cash {prevCash} / savings {prevSavings}";
            }

            Sum(today, out float todayCash, out float todaySavings);
            if (!Near(today.startCash + todayCash, stats.cash))
            {
                return $"Day {today.day} (open): cash {today.startCash} + events {todayCash} != stats {stats.cash}";
            }

            if (!Near(today.startSavings + todaySavings, stats.savings))
            {
                return $"Day {today.day} (open): savings {today.startSavings} + events {todaySavings} != stats {stats.savings}";
            }

            return null;
        }

        static void Sum(DayRecord record, out float cash, out float savings)
        {
            double cashSum = 0d;
            double savingsSum = 0d;
            if (record.events != null)
            {
                foreach (MoneyEvent e in record.events)
                {
                    if (e == null)
                    {
                        continue;
                    }

                    cashSum += e.cashDelta;
                    savingsSum += e.savingsDelta;
                }
            }

            cash = (float)cashSum;
            savings = (float)savingsSum;
        }

        static bool Near(float a, float b) => Math.Abs(a - b) <= Tolerance;
    }
}
