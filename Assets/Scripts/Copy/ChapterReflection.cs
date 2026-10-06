using System;
using System.Collections.Generic;
using System.Globalization;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;

namespace MaliGo.Copy
{
    /// <summary>The chapter end's numbers (DESIGN_SPEC §5.4.9 screen A, §4.4). Never compared with an earlier run.</summary>
    public class ChapterSummary
    {
        public float startTotal;
        public float endTotal;
        public float endCash;
        public float endSavings;

        /// <summary>Arrears going into payday (never part of the total).</summary>
        public float stillOwed;

        /// <summary>"{shortLabel} R{amt}" per promised payment (ObligationLedger.Promised), in creation order.</summary>
        public string[] promised = Array.Empty<string>();

        /// <summary>Sum of everything already promised for payday.</summary>
        public float promisedTotal;

        /// <summary>The top three scenario events (transfers excluded) by |TotalDelta|, ties to the earlier.</summary>
        public MoneyEvent[] top = Array.Empty<MoneyEvent>();

        /// <summary>The day of each event in <see cref="top"/>.</summary>
        public int[] topDays = Array.Empty<int>();

        /// <summary>Sum of every bill: event (negative, or 0).</summary>
        public float billsTotal;
    }

    /// <summary>
    /// The chapter end's words (DESIGN_SPEC §4.4, §4.8, §5.4.9): the summary of the week, "What moved it most" and
    /// "What Mali noticed". Pure; reads only <c>chapter.days</c> (incl. <c>owedAtClose</c>), <c>chapter.choices</c>,
    /// obligations, stats and the spending profile. Observations of actions, never traits; no scores or ratios.
    /// </summary>
    public static class ChapterReflection
    {
        public const string Title = "Seven days to payday";
        public const string StartLabel = "On Day 1 you had";
        public const string EndLabel = "Tonight you have";
        public const string TotalCaption = "cash + savings";
        public const string MomentsTitle = "What moved it most";
        public const string NoMoments = "None of your choices moved money this week.";
        public const string NoticedTitle = "What Mali noticed";
        public const string PlanPrompt1 = "Tomorrow is payday.";
        public const string PlanPrompt2 = "If you could decide one thing about payday tonight, what would it be?";
        public const string PlanSaved = "Got it. Next time this week starts, I'll remind you what you said.";
        public const string PlanNotNow = "That's the week, {name}.";
        public const string CloseCaption = "Chapter 2 starts on payday. It's coming in a future update.";
        public const string LiveAgain = "Live the week again";

        /// <summary>N0 needs at least this much spent in the focus's categories (Revision 4).</summary>
        public const float N0Minimum = 100f;

        const float Tol = 0.005f;

        // ================================================================ summary

        public static ChapterSummary Summary(PlayerData data)
        {
            var s = new ChapterSummary();
            if (data == null)
            {
                return s;
            }

            ChapterRecord chapter = data.chapter ?? new ChapterRecord();
            s.startTotal = chapter.startCash + chapter.startSavings;

            DayRecord last = LastDay(chapter);
            if (last != null)
            {
                s.endCash = last.endCash;
                s.endSavings = last.endSavings;
            }
            else if (data.financialStats != null)
            {
                s.endCash = data.financialStats.cash;
                s.endSavings = data.financialStats.savings;
            }

            s.endTotal = s.endCash + s.endSavings;
            s.stillOwed = ObligationLedger.TotalArrears(data);

            var promised = new List<string>();
            foreach (DueItem item in ObligationLedger.Promised(data))
            {
                promised.Add((string.IsNullOrEmpty(item.shortLabel) ? item.label : item.shortLabel) + " " + MoneyFormat.Rand(item.Total));
                s.promisedTotal += item.Total;
            }

            s.promised = promised.ToArray();

            // Top three scenario events; a stable ranking keeps the earlier event on a tie.
            var ranked = new List<(MoneyEvent e, int day, int order)>();
            int order = 0;
            foreach (DayRecord day in chapter.days ?? Array.Empty<DayRecord>())
            {
                if (day == null)
                {
                    continue;
                }

                foreach (MoneyEvent e in day.events ?? Array.Empty<MoneyEvent>())
                {
                    if (e == null)
                    {
                        continue;
                    }

                    string source = e.sourceId ?? "";
                    if (source.StartsWith("bill:", StringComparison.Ordinal))
                    {
                        s.billsTotal += e.TotalDelta;
                    }

                    if (source.StartsWith("scenario:", StringComparison.Ordinal) && e.kind != MoneyEventKind.Transfer)
                    {
                        ranked.Add((e, day.day, order++));
                    }
                }
            }

            ranked.Sort((a, b) =>
            {
                int byAmount = Math.Abs(b.e.TotalDelta).CompareTo(Math.Abs(a.e.TotalDelta));
                if (Math.Abs(Math.Abs(b.e.TotalDelta) - Math.Abs(a.e.TotalDelta)) <= Tol)
                {
                    byAmount = 0;
                }

                return byAmount != 0 ? byAmount : a.order.CompareTo(b.order);
            });

            int n = Math.Min(3, ranked.Count);
            s.top = new MoneyEvent[n];
            s.topDays = new int[n];
            for (int i = 0; i < n; i++)
            {
                s.top[i] = ranked[i].e;
                s.topDays[i] = ranked[i].day;
            }

            return s;
        }

        static DayRecord LastDay(ChapterRecord chapter)
        {
            DayRecord[] days = chapter?.days;
            if (days == null)
            {
                return null;
            }

            for (int i = days.Length - 1; i >= 0; i--)
            {
                if (days[i] != null && days[i].day > 0)
                {
                    return days[i];
                }
            }

            return null;
        }

        // ---------------------------------------------------------------- screen A lines

        /// <summary>"+R440 this week" / "−R455 this week".</summary>
        public static string WeekPill(ChapterSummary s) => MoneyFormat.Signed(s.endTotal - s.startTotal) + " this week";

        public static string SplitLine(ChapterSummary s) =>
            "Cash " + MoneyFormat.Rand(s.endCash) + " · Savings " + MoneyFormat.Rand(s.endSavings);

        /// <summary>"Still owed going into payday: R{owed}"; null when nothing is owed.</summary>
        public static string StillOwedLine(ChapterSummary s) =>
            s.stillOwed > Tol ? "Still owed going into payday: " + MoneyFormat.Rand(s.stillOwed) : null;

        /// <summary>"Already promised for payday: R{sum}"; null when nothing is promised.</summary>
        public static string PromisedLine(ChapterSummary s) =>
            s.promised.Length > 0 ? "Already promised for payday: " + MoneyFormat.Rand(s.promisedTotal) : null;

        /// <summary>"Stokvel R200 · Gym R199"; null when nothing is promised.</summary>
        public static string PromisedList(ChapterSummary s) => s.promised.Length > 0 ? string.Join(" · ", s.promised) : null;

        /// <summary>"Bills and repayments: −R{amt}"; null when no bill was charged.</summary>
        public static string BillsLine(ChapterSummary s) =>
            Math.Abs(s.billsTotal) > Tol ? "Bills and repayments: " + MoneyFormat.Signed(s.billsTotal) : null;

        /// <summary>The bills line for an amount (used by fit tests).</summary>
        public static string BillsLine(float billsTotal) => "Bills and repayments: " + MoneyFormat.Signed(billsTotal);

        /// <summary>"Day {d}" caption of a moment card.</summary>
        public static string MomentDay(int day) => "Day " + day.ToString(CultureInfo.InvariantCulture);

        // ================================================================ What Mali noticed (§4.4)

        /// <summary>Two or three lines: the first three rules that match (N0–N7b), topped up with F1 then F2.</summary>
        public static string[] Noticed(PlayerData data)
        {
            var lines = new List<string>(3);
            if (data == null)
            {
                lines.Add("You lived the whole week, one choice at a time.");
                lines.Add("You didn't take a shift this week.");
                return lines.ToArray();
            }

            ChapterRecord chapter = data.chapter ?? new ChapterRecord();
            void Add(string line)
            {
                if (line != null && lines.Count < 3)
                {
                    lines.Add(line);
                }
            }

            // N0: only from R100 (Revision 4), so a small week never reads as contradicting what the player said.
            SpendingProfile profile = data.spendingProfile;
            if (profile != null && profile.source == SpendingProfileSource.Onboarding)
            {
                float amt = Outflow(chapter, SpendingFocus.Categories(profile.focus));
                if (amt >= N0Minimum - Tol)
                {
                    Add("You said most of your money goes on " + SpendingFocus.Get(profile.focus).maliPhrase
                        + ". This week that came to R" + MoneyFormat.Digits(amt) + ".");
                }
            }

            // N1 / N1b / N1c
            if (Chose(chapter, "emergency_expense", "from_savings"))
            {
                Add("When the geyser broke, you reached for savings first.");
            }
            else if (Chose(chapter, "emergency_expense", "from_cash"))
            {
                Add("When the geyser broke, you paid for it from cash the same day.");
            }
            else if (Chose(chapter, "emergency_expense", "cold_showers"))
            {
                Add("When the geyser broke, you waited it out with cold water.");
            }

            // N2
            float support = Outflow(chapter, new[] { MoneyCategory.Family, MoneyCategory.Friends });
            if (support >= 50f - Tol)
            {
                Add("You made room for other people this week: R" + MoneyFormat.Digits(support) + " to family and friends.");
            }

            // N3 / N4 / N5
            int deferred = TagCount(chapter, "Deferred");
            if (deferred >= 2)
            {
                Add(CountWord(deferred) + " you moved a cost to later.");
            }

            int frugal = TagCount(chapter, "Frugal");
            if (frugal >= 3)
            {
                Add(CountWord(frugal) + " you held on to your money rather than spend it.");
            }

            int discretionary = TagCount(chapter, "Discretionary");
            if (discretionary >= 2)
            {
                Add(CountWord(discretionary) + " you chose the option that made the day easier.");
            }

            // N6: the net movement of savings over the week (money taken out counts against money put in).
            float saved = 0f;
            foreach (MoneyEvent e in AllEvents(chapter))
            {
                saved += e.savingsDelta;
            }

            if (saved >= 100f - Tol)
            {
                Add("R" + MoneyFormat.Digits(saved) + " went into savings over the week.");
            }

            // N7 / N7b (from owedAtClose only)
            bool anyNightOwed = false;
            foreach (DayRecord day in chapter.days ?? Array.Empty<DayRecord>())
            {
                if (day != null && day.owedAtClose > Tol)
                {
                    anyNightOwed = true;
                    break;
                }
            }

            if (anyNightOwed)
            {
                float owed = ObligationLedger.TotalArrears(data);
                Add(owed > Tol
                    ? "Some nights there wasn't enough cash for everything due. R" + MoneyFormat.Digits(owed) + " is still owed going into payday."
                    : "Some nights there wasn't enough cash for everything due. It carried over and got paid.");
            }

            if (lines.Count < 2)
            {
                int shifts = chapter.shiftsWorked;
                string f1 = shifts >= 2
                    ? "You took a shift on " + shifts.ToString(CultureInfo.InvariantCulture) + " days this week."
                    : shifts == 1
                        ? "You took a shift on one day this week."
                        : "You didn't take a shift this week.";
                lines.Add(f1);
            }

            if (lines.Count < 2)
            {
                lines.Add("You lived the whole week, one choice at a time.");
            }

            return lines.ToArray();
        }

        /// <summary>2 → "Twice", 3 → "Three times", 4 → "Four times", n ≥ 5 → "{n} times".</summary>
        public static string CountWord(int n)
        {
            switch (n)
            {
                case 1: return "Once";
                case 2: return "Twice";
                case 3: return "Three times";
                case 4: return "Four times";
                default: return n.ToString(CultureInfo.InvariantCulture) + " times";
            }
        }

        static IEnumerable<MoneyEvent> AllEvents(ChapterRecord chapter)
        {
            foreach (DayRecord day in chapter.days ?? Array.Empty<DayRecord>())
            {
                if (day?.events == null)
                {
                    continue;
                }

                foreach (MoneyEvent e in day.events)
                {
                    if (e != null)
                    {
                        yield return e;
                    }
                }
            }
        }

        /// <summary>Money out (bills included) in these categories, as a positive amount.</summary>
        static float Outflow(ChapterRecord chapter, string[] categories)
        {
            float total = 0f;
            foreach (MoneyEvent e in AllEvents(chapter))
            {
                if (e.kind == MoneyEventKind.Out && Array.IndexOf(categories, e.category) >= 0)
                {
                    total -= e.TotalDelta;
                }
            }

            return total;
        }

        static bool Chose(ChapterRecord chapter, string scenarioId, string choiceId)
        {
            foreach (ChoiceRecord c in chapter.choices ?? Array.Empty<ChoiceRecord>())
            {
                if (c != null && c.scenarioId == scenarioId && c.choiceId == choiceId)
                {
                    return true;
                }
            }

            return false;
        }

        static int TagCount(ChapterRecord chapter, string tag)
        {
            int n = 0;
            foreach (ChoiceRecord c in chapter.choices ?? Array.Empty<ChoiceRecord>())
            {
                if (c != null && c.tag == tag)
                {
                    n++;
                }
            }

            return n;
        }

        /// <summary>The line Mali says after the plan screen ("Got it…" or "That's the week, {name}."), filled.</summary>
        public static string CloseLine(PlayerData data, bool planSaved)
        {
            return planSaved ? PlanSaved : MaliText.Fill(PlanNotNow, data);
        }
    }
}
