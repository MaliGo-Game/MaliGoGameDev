using System.Globalization;
using MaliGo.Data;
using MaliGo.Economy;

namespace MaliGo.Copy
{
    /// <summary>
    /// Every string the HUD shows (DESIGN_SPEC §5.4.1, §7.4, §4.9). Pure: built only from the save and the shared
    /// bill helpers (<see cref="ObligationLedger"/>), so the fit of every string can be tested outside Unity.
    /// Never shows a name, level, XP, stress or score.
    /// </summary>
    public static class HudCopy
    {
        public const string NoBillsLine1 = "No bills";
        public const string NoBillsLine2 = "till payday";

        /// <summary>"Day 3 of 7" / "Payday in 5 days" (or "Payday tomorrow" on Day 7).</summary>
        public static (string line1, string line2) DayPill(PlayerData data)
        {
            int day = data != null ? data.currentDay : 1;
            if (day < 1)
            {
                day = 1;
            }

            int toPayday = ChapterConfig.DaysToPayday(day);
            string line2 = toPayday <= 1
                ? "Payday tomorrow"
                : "Payday in " + toPayday.ToString(CultureInfo.InvariantCulture) + " days";
            return ("Day " + day.ToString(CultureInfo.InvariantCulture) + " of "
                    + ChapterConfig.ChapterLength.ToString(CultureInfo.InvariantCulture), line2);
        }

        /// <summary>
        /// The Today pill (A4): line 1 "Today from R{start}" (start = <c>today.StartTotal</c>, cash + savings when
        /// the day opened) and the value after the arrow, "R{now}" (cash + savings now).
        /// </summary>
        public static (string line1, string nowText) TodayPill(PlayerData data)
        {
            return ("Today from " + MoneyFormat.Rand(TodayStart(data)), MoneyFormat.Rand(TodayNow(data)));
        }

        /// <summary>Cash + savings when today opened (the number the reveal starts from).</summary>
        public static float TodayStart(PlayerData data)
        {
            if (data?.today != null && data.today.day > 0)
            {
                return data.today.StartTotal;
            }

            return TodayNow(data);
        }

        /// <summary>Cash + savings now.</summary>
        public static float TodayNow(PlayerData data)
        {
            FinancialStats stats = data?.financialStats;
            return stats != null ? stats.cash + stats.savings : 0f;
        }

        /// <summary>
        /// The bill pill: the largest arrears ("{shortLabel} owed" / "R{owed}", attention = true), else the next
        /// payment ("{shortLabel}" / "R{amt} · {when}", when = tonight / tomorrow / Day {n} / payday), else
        /// "No bills" / "till payday".
        /// </summary>
        public static (string line1, string line2, bool attention) BillPill(PlayerData data)
        {
            Obligation owed = ObligationLedger.LargestArrears(data);
            if (owed != null)
            {
                return (ShortLabel(owed.shortLabel, owed.label) + " owed", MoneyFormat.Rand(owed.arrears), true);
            }

            DueItem? next = ObligationLedger.NextDue(data);
            if (next.HasValue)
            {
                DueItem item = next.Value;
                int day = data != null ? data.currentDay : 1;
                return (ShortLabel(item.shortLabel, item.label), MoneyFormat.Rand(item.amount) + " · " + When(item.day, day), false);
            }

            return (NoBillsLine1, NoBillsLine2, false);
        }

        /// <summary>"tonight" / "tomorrow" / "Day {n}" / "payday" for a due day seen from <paramref name="currentDay"/>.</summary>
        public static string When(int dueDay, int currentDay)
        {
            if (dueDay >= ChapterConfig.PaydayDay)
            {
                return "payday";
            }

            if (dueDay <= currentDay)
            {
                return "tonight";
            }

            if (dueDay == currentDay + 1)
            {
                return "tomorrow";
            }

            return "Day " + dueDay.ToString(CultureInfo.InvariantCulture);
        }

        static string ShortLabel(string shortLabel, string label)
        {
            return string.IsNullOrEmpty(shortLabel) ? (label ?? "") : shortLabel;
        }
    }
}
