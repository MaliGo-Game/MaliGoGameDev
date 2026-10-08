using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MaliGo.Data;
using MaliGo.Economy;

namespace MaliGo.BankFeed
{
    /// <summary>
    /// Words for the optional "Connect your bank" step and Pause (docs/BANK_FEED.md). Plain language, warm, no
    /// judgement, generic (no bank or brand names). Pure, so the logic tests check glyphs and brands.
    /// </summary>
    public static class BankFeedCopy
    {
        // Intro card.
        public const string IntroTitle = "Connect your bank?";
        public const string IntroBody =
            "Optional. Your week can follow how you really spend: the places you go and the things you buy. You can skip this.";
        public const string ConnectButton = "Connect your bank";
        public const string SkipButton = "Skip for now";

        // Consent.
        public const string ConsentTitle = "Before you connect";

        public static readonly string[] ConsentPoints =
        {
            "We read your accounts and the last 60 days of money in and out.",
            "Your phone sorts them into habits, like food or getting around.",
            "Only those habits stay, on this phone. The payments are never saved.",
            "Nothing is sent anywhere.",
            "You can skip this, and forget it any time in Pause.",
        };

        public const string NoLiveLine =
            "A live bank connection isn't available yet. You can see how it works with a sample person instead.";
        public const string SampleButton = "Try with a sample person";
        public const string AgreeButton = "I agree, connect";
        public const string WaitingLine = "Waiting for your bank...";
        public const string ErrorLine = "That didn't work. You can try again, or skip.";

        // Summary.
        public const string SummaryTitle = "What your money says";
        public const string SampleBadge = "Sample data, not yours";
        public const string SamplePersonPrefix = "Sample person: ";
        public const string WeekLine = "We'll start your week with:";
        public const string ChangeLine = "You can change these on the next screen.";
        public const string AnotherSampleButton = "Another sample";
        public const string NoHabitsLine = "Nothing stood out. You can pick for yourself next.";

        // Pause.
        public const string ForgetButton = "Forget my bank data";
        public const string ForgottenNotice = "Your bank data is forgotten.";

        /// <summary>"Sample person: Student living in res".</summary>
        public static string SamplePersonLine(string personaId)
        {
            return SamplePersonPrefix + SampleTransactionSource.Persona(personaId).label;
        }

        /// <summary>"Food and takeaways · Minibus taxi".</summary>
        public static string WeekPicks(string focus, string travel)
        {
            return SpendingFocus.Get(focus).cardLabel + OnboardingCopy.WeekPlacesSeparator + TravelMode.Get(travel).cardLabel;
        }

        /// <summary>
        /// Up to <paramref name="count"/> short lines about the player's biggest habits, most telling first. A habit
        /// flag (eats out often, taxi commuter...) outranks a plain category; within each group, a bigger share of
        /// spend plus more payments ranks higher. Empty for a summary with no data.
        /// </summary>
        public static List<string> TopHabitLines(BankHabitSummary s, int count)
        {
            var candidates = new List<KeyValuePair<float, string>>();
            if (s == null || !s.HasData)
            {
                return new List<string>();
            }

            CategoryHabit eat = s.Get(SpendCategory.EatingOut);
            CategoryHabit taxi = s.Get(SpendCategory.Transport);
            CategoryHabit rides = s.Get(SpendCategory.EHailing);
            CategoryHabit fuel = s.Get(SpendCategory.Fuel);
            CategoryHabit data = s.Get(SpendCategory.DataAirtime);
            CategoryHabit groceries = s.Get(SpendCategory.Groceries);
            CategoryHabit savings = s.Get(SpendCategory.Savings);
            CategoryHabit fun = s.Get(SpendCategory.Entertainment);
            CategoryHabit family = s.Get(SpendCategory.Family);

            if (s.eatsOutOften && eat != null)
            {
                Add(candidates, eat, true,
                    "You grab food out most days, " + Times(eat.perWeek) + " a week, usually around " + Rand(eat.typicalAmount) + ".");
            }

            if (s.taxiCommuter && taxi != null)
            {
                Add(candidates, taxi, true,
                    "Taxis get you where you need to be: " + Trips(taxi.perWeek) + " a week, around " + Rand(taxi.typicalAmount) + " a trip.");
            }

            if (s.dataHeavy && data != null)
            {
                Add(candidates, data, true,
                    "Staying connected matters to you: you buy data or airtime " + Times(data.perWeek) + " a week.");
            }

            if (s.sendsMoneyHome && family != null)
            {
                Add(candidates, family, true, "You look after the people at home and send money regularly.");
            }

            if (s.cashUser)
            {
                Add(candidates, s.Get(SpendCategory.Cash), true,
                    "You like cash in hand, drawing about " + Rand(s.typicalCashWithdrawal) + " at a time.");
            }

            if (s.hasLoanRepayments)
            {
                Add(candidates, s.Get(SpendCategory.LoanRepayment), false, "You're steadily paying off an account every month.");
            }

            if (!s.eatsOutOften && eat != null && eat.perWeek >= 1f)
            {
                Add(candidates, eat, false, "You buy food out " + Times(eat.perWeek) + " a week.");
            }

            if (!s.taxiCommuter && taxi != null && taxi.perWeek >= 1f)
            {
                Add(candidates, taxi, false, "You take a taxi or bus " + Times(taxi.perWeek) + " a week.");
            }

            if (rides != null && rides.perWeek >= 1f)
            {
                Add(candidates, rides, false, "Ride apps get you around, " + Trips(rides.perWeek) + " a week.");
            }

            if (fuel != null && fuel.perWeek >= 0.5f)
            {
                Add(candidates, fuel, false, "You fill up the car " + Times(fuel.perWeek) + " a week.");
            }

            if (!s.dataHeavy && data != null && data.perWeek >= 0.5f)
            {
                Add(candidates, data, false, "You top up data about " + Times(data.perWeek) + " a week.");
            }

            if (groceries != null && groceries.perWeek >= 1f)
            {
                Add(candidates, groceries, false, "You shop for groceries " + Times(groceries.perWeek) + " a week.");
            }

            if (savings != null)
            {
                Add(candidates, savings, false, "You put money into a stokvel or savings every month. Nice.");
            }

            if (fun != null && fun.perWeek >= 1f)
            {
                Add(candidates, fun, false, "You make time for fun, " + Times(fun.perWeek) + " a week.");
            }

            if (s.paydayDayOfMonth > 0)
            {
                candidates.Add(new KeyValuePair<float, string>(0.05f,
                    "Your money comes in around the " + Ordinal(s.paydayDayOfMonth) + " of the month."));
            }

            return candidates
                .Select((c, index) => new { c, index })
                .OrderByDescending(x => x.c.Key)
                .ThenBy(x => x.index)
                .Take(Math.Max(0, count))
                .Select(x => x.c.Value)
                .ToList();
        }

        static void Add(List<KeyValuePair<float, string>> list, CategoryHabit habit, bool flag, string line)
        {
            float score = (habit != null ? habit.share + Math.Min(habit.perWeek, 20f) / 20f : 0f) + (flag ? 2f : 0f);
            list.Add(new KeyValuePair<float, string>(score, line));
        }

        /// <summary>"once", "twice", "about 5 times".</summary>
        public static string Times(float perWeek)
        {
            int n = Math.Max(1, (int)Math.Round(perWeek, MidpointRounding.AwayFromZero));
            switch (n)
            {
                case 1: return "about once";
                case 2: return "about twice";
                default: return "about " + n.ToString(CultureInfo.InvariantCulture) + " times";
            }
        }

        static string Trips(float perWeek)
        {
            int n = Math.Max(1, (int)Math.Round(perWeek, MidpointRounding.AwayFromZero));
            return "about " + n.ToString(CultureInfo.InvariantCulture) + (n == 1 ? " trip" : " trips");
        }

        static string Rand(float amount) => MoneyFormat.Rand(amount);

        /// <summary>1st, 2nd, 3rd, 4th ... 11th, 12th, 13th ... 21st, 22nd, 23rd ... 31st.</summary>
        public static string Ordinal(int day)
        {
            string suffix = "th";
            if (day % 100 < 11 || day % 100 > 13)
            {
                switch (day % 10)
                {
                    case 1: suffix = "st"; break;
                    case 2: suffix = "nd"; break;
                    case 3: suffix = "rd"; break;
                }
            }

            return day.ToString(CultureInfo.InvariantCulture) + suffix;
        }
    }
}
