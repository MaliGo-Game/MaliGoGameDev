using System;
using System.Collections.Generic;
using System.Linq;
using MaliGo.Data;

namespace MaliGo.BankFeed
{
    /// <summary>
    /// Reduces transactions to a <see cref="BankHabitSummary"/> on the phone (docs/BANK_FEED.md). Only booked ZAR
    /// transactions count (pending ones may be booked again later). The result holds per-category shares, rates and
    /// median amounts, the income rhythm and habit flags, and nothing that identifies a single payment. Pure.
    /// </summary>
    public static class HabitSummaryBuilder
    {
        // Habit flag thresholds (per week unless noted).
        public const float EatsOutPerWeek = 3f;
        public const float TaxiTripsPerWeek = 4f;
        public const float DataTopUpsPerWeek = 2f;
        public const float DataHeavyShare = 0.12f;
        public const float CashWithdrawalsPerWeek = 1f;
        public const float CashUserShare = 0.2f;

        /// <summary>Income credits at least this share of the largest one are "the main income".</summary>
        const decimal MainIncomeShare = 0.5m;

        /// <summary>Main income credits within this many days of the month of each other set a payday.</summary>
        const int PaydaySpreadDays = 3;

        const int MinWindowDays = 7;

        /// <summary>
        /// The summary for these transactions. <paramref name="windowDays"/> &lt;= 0 takes the span of the booking dates
        /// (at least 7 days). Source and persona are filled by the caller. Never null; with no spending it has no
        /// categories (HasData false).
        /// </summary>
        public static BankHabitSummary Build(IEnumerable<ObTransaction> transactions, int windowDays)
        {
            var summary = new BankHabitSummary();
            var spendByCategory = new Dictionary<string, List<decimal>>(StringComparer.Ordinal);
            var incomes = new List<KeyValuePair<DateTimeOffset, decimal>>();
            var incomeKinds = new List<KeyValuePair<decimal, string>>();
            DateTimeOffset? first = null;
            DateTimeOffset? last = null;
            decimal totalSpend = 0m;
            int spendCount = 0;

            if (transactions != null)
            {
                foreach (ObTransaction t in transactions)
                {
                    if (t == null || !t.IsBooked || !IsRand(t))
                    {
                        continue;
                    }

                    decimal amount = t.Amount.Value;
                    if (amount <= 0m)
                    {
                        continue;
                    }

                    bool hasTime = t.TryGetBookingTime(out DateTimeOffset when);
                    if (hasTime)
                    {
                        first = first == null || when < first ? when : first;
                        last = last == null || when > last ? when : last;
                    }

                    Categorised c = TransactionCategoriser.Categorise(t);
                    if (c.IsSpend)
                    {
                        if (!spendByCategory.TryGetValue(c.Category, out List<decimal> list))
                        {
                            list = new List<decimal>();
                            spendByCategory[c.Category] = list;
                        }

                        list.Add(amount);
                        totalSpend += amount;
                        spendCount++;
                    }
                    else if (c.IsIncome)
                    {
                        incomeKinds.Add(new KeyValuePair<decimal, string>(amount, c.IncomeKind));
                        if (hasTime)
                        {
                            incomes.Add(new KeyValuePair<DateTimeOffset, decimal>(when, amount));
                        }
                    }
                }
            }

            if (windowDays <= 0)
            {
                windowDays = first != null && last != null
                    ? (int)Math.Ceiling((last.Value - first.Value).TotalDays) + 1
                    : MinWindowDays;
            }

            windowDays = Math.Max(MinWindowDays, windowDays);
            float weeks = windowDays / 7f;
            summary.windowDays = windowDays;
            summary.spendCount = spendCount;

            var habits = new List<CategoryHabit>();
            if (totalSpend > 0m)
            {
                foreach (KeyValuePair<string, List<decimal>> pair in spendByCategory)
                {
                    decimal sum = 0m;
                    foreach (decimal a in pair.Value)
                    {
                        sum += a;
                    }

                    habits.Add(new CategoryHabit
                    {
                        category = pair.Key,
                        share = (float)Math.Round(sum / totalSpend, 4),
                        perWeek = (float)Math.Round(pair.Value.Count / weeks, 2),
                        typicalAmount = (float)Math.Round(Median(pair.Value), 0, MidpointRounding.AwayFromZero)
                    });
                }
            }

            summary.categories = habits
                .OrderByDescending(h => h.share)
                .ThenBy(h => h.category, StringComparer.Ordinal)
                .ToArray();

            ReadIncome(summary, incomes, incomeKinds);

            CategoryHabit cash = summary.Get(SpendCategory.Cash);
            summary.cashWithdrawalsPerWeek = cash?.perWeek ?? 0f;
            summary.typicalCashWithdrawal = cash?.typicalAmount ?? 0f;

            summary.eatsOutOften = PerWeek(summary, SpendCategory.EatingOut) >= EatsOutPerWeek;
            summary.taxiCommuter = PerWeek(summary, SpendCategory.Transport) >= TaxiTripsPerWeek;
            summary.dataHeavy = PerWeek(summary, SpendCategory.DataAirtime) >= DataTopUpsPerWeek
                                || Share(summary, SpendCategory.DataAirtime) >= DataHeavyShare;
            summary.cashUser = summary.cashWithdrawalsPerWeek >= CashWithdrawalsPerWeek
                               || Share(summary, SpendCategory.Cash) >= CashUserShare;
            summary.sendsMoneyHome = summary.Get(SpendCategory.Family) != null;
            summary.hasLoanRepayments = summary.Get(SpendCategory.LoanRepayment) != null;

            summary.suggestedFocus = BankFeedMapping.FocusFor(summary);
            summary.suggestedTravel = BankFeedMapping.TravelFor(summary);
            return summary;
        }

        public static float PerWeek(BankHabitSummary s, string category) => s?.Get(category)?.perWeek ?? 0f;

        public static float Share(BankHabitSummary s, string category) => s?.Get(category)?.share ?? 0f;

        static bool IsRand(ObTransaction t)
        {
            string currency = t.Amount?.Currency;
            return t.Amount != null && (string.IsNullOrEmpty(currency) ||
                                        string.Equals(currency, "ZAR", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Income kind = the kind of the largest credit. Payday = the median day of the month of the main income
        /// credits (each at least half the largest), when there are two or more and they fall within a few days of
        /// each other (month ends wrap: the 30th and the 1st are close); otherwise 0.
        /// </summary>
        static void ReadIncome(BankHabitSummary summary, List<KeyValuePair<DateTimeOffset, decimal>> incomes,
            List<KeyValuePair<decimal, string>> kinds)
        {
            if (kinds.Count == 0)
            {
                summary.incomeKind = IncomeKind.None;
                return;
            }

            summary.incomeKind = kinds.OrderByDescending(k => k.Key).First().Value;
            if (incomes.Count < 2)
            {
                return;
            }

            decimal largest = incomes.Max(p => p.Value);
            List<int> days = incomes
                .Where(p => p.Value >= largest * MainIncomeShare)
                .Select(p => p.Key.Day)
                .OrderBy(d => d)
                .ToList();
            if (days.Count < 2)
            {
                return;
            }

            for (int a = 0; a < days.Count; a++)
            {
                for (int b = a + 1; b < days.Count; b++)
                {
                    int gap = Math.Abs(days[a] - days[b]);
                    if (Math.Min(gap, 31 - gap) > PaydaySpreadDays)
                    {
                        if (summary.incomeKind == IncomeKind.Salary)
                        {
                            summary.incomeKind = IncomeKind.Irregular;
                        }

                        return;
                    }
                }
            }

            summary.paydayDayOfMonth = days[days.Count / 2];
        }

        static decimal Median(List<decimal> values)
        {
            if (values.Count == 0)
            {
                return 0m;
            }

            var sorted = new List<decimal>(values);
            sorted.Sort();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2m;
        }
    }
}
