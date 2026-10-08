using System;
using System.Collections.Generic;
using System.Linq;
using MaliGo.Data;
using MaliGo.Scenarios;

namespace MaliGo.BankFeed
{
    /// <summary>
    /// From habits to gameplay (docs/BANK_FEED.md), through the inputs the game already has:
    /// (a) the same SpendingFocus / TravelMode ids the two onboarding taps produce, which pick the week's schedule
    ///     (ChapterSchedule), the scenario variants (ScenarioLibrary) and the place names; and
    /// (b) a weight per scenario spot, for ordering what the player is told about and for later schedule tuning.
    /// Everything here reads only the saved summary, so it can be recomputed after a load. Pure.
    /// </summary>
    public static class BankFeedMapping
    {
        // Categories that say where day-to-day money goes (fixed commitments like rent, loans and savings are left
        // out of the focus choice: everyone has them, they are not a habit the week can be built around).
        static readonly string[] Discretionary =
        {
            SpendCategory.EatingOut, SpendCategory.Groceries, SpendCategory.Transport, SpendCategory.EHailing,
            SpendCategory.Fuel, SpendCategory.DataAirtime, SpendCategory.Clothing, SpendCategory.Entertainment,
            SpendCategory.Family
        };

        /// <summary>Below this many trips a week (and almost no fuel) the player gets around on foot.</summary>
        public const float WalkTripsPerWeek = 1.5f;

        public const float CarFuelPerWeek = 0.5f;

        /// <summary>One fuel stop stands for several trips when comparing with fares.</summary>
        const float TripsPerFuelStop = 4f;

        // ------------------------------------------------------------------ (a) profile

        /// <summary>Fills <paramref name="focus"/> and <paramref name="travel"/> from the summary; false (food/taxi) when it has no data.</summary>
        public static bool SuggestProfile(BankHabitSummary summary, out string focus, out string travel)
        {
            if (summary == null || !summary.HasData)
            {
                focus = SpendingFocus.Food;
                travel = TravelMode.Taxi;
                return false;
            }

            focus = FocusFor(summary);
            travel = TravelFor(summary);
            return true;
        }

        /// <summary>
        /// The SpendingFocus the habits point to: each category's score is half its share of day-to-day spend by
        /// amount and half by number of payments; food = eating out; transport = fares, rides and fuel; data_social =
        /// data/airtime, entertainment and half of clothing; home_family = money sent home and half of groceries.
        /// Ties go to the earlier of food, transport, data_social, home_family. No data: food.
        /// </summary>
        public static string FocusFor(BankHabitSummary summary)
        {
            Dictionary<string, float> s = DiscretionaryScores(summary);
            if (s.Count == 0)
            {
                return SpendingFocus.Food;
            }

            var foci = new[]
            {
                new KeyValuePair<string, float>(SpendingFocus.Food, S(s, SpendCategory.EatingOut)),
                new KeyValuePair<string, float>(SpendingFocus.Transport,
                    S(s, SpendCategory.Transport) + S(s, SpendCategory.EHailing) + S(s, SpendCategory.Fuel)),
                new KeyValuePair<string, float>(SpendingFocus.DataSocial,
                    S(s, SpendCategory.DataAirtime) + S(s, SpendCategory.Entertainment) +
                    0.5f * S(s, SpendCategory.Clothing)),
                new KeyValuePair<string, float>(SpendingFocus.HomeFamily,
                    S(s, SpendCategory.Family) + 0.5f * S(s, SpendCategory.Groceries)),
            };

            KeyValuePair<string, float> best = foci[0];
            foreach (KeyValuePair<string, float> f in foci)
            {
                if (f.Value > best.Value + 1e-4f)
                {
                    best = f;
                }
            }

            return best.Key;
        }

        /// <summary>
        /// The TravelMode: walk when there are fewer than 1.5 fares/rides a week and under one fuel stop every two
        /// weeks; otherwise the largest of minibus/bus fares, e-hailing rides and fuel stops (x4, a tank is many
        /// trips). Ties: taxi, then e-hailing, then car. No data: taxi.
        /// </summary>
        public static string TravelFor(BankHabitSummary summary)
        {
            if (!HasCategories(summary))
            {
                return TravelMode.Taxi;
            }

            float taxi = HabitSummaryBuilder.PerWeek(summary, SpendCategory.Transport);
            float rides = HabitSummaryBuilder.PerWeek(summary, SpendCategory.EHailing);
            float fuel = HabitSummaryBuilder.PerWeek(summary, SpendCategory.Fuel);
            if (taxi + rides < WalkTripsPerWeek && fuel < CarFuelPerWeek)
            {
                return TravelMode.Walk;
            }

            float car = fuel >= CarFuelPerWeek ? fuel * TripsPerFuelStop : 0f;
            if (taxi >= rides && taxi >= car)
            {
                return TravelMode.Taxi;
            }

            return rides >= car ? TravelMode.EHailing : TravelMode.Car;
        }

        // ------------------------------------------------------------------ (b) spots

        /// <summary>
        /// A weight per ChapterSchedule spot (all six, summing to 1; equal when there is no data). Each category's
        /// score is half its share of all spend and half its share of payments:
        /// CORNER (kota/corner/spaza shop) = eating out + half data/airtime + half groceries;
        /// TAXI (rank, pick-up point or petrol station) = fares + rides + fuel;
        /// HUB (shops by the bank) = savings/stokvel + half loan repayments + half cash withdrawals;
        /// SHOPFRONT = clothing + half entertainment;
        /// GATE (home) = money sent home + half rent/utilities + half groceries;
        /// EAST (down the road: the lender, the neighbour) = half loan repayments + half cash + half entertainment.
        /// </summary>
        public static Dictionary<string, float> SpotWeights(BankHabitSummary summary)
        {
            var weights = new Dictionary<string, float>(StringComparer.Ordinal);
            Dictionary<string, float> s = AllScores(summary);
            if (s.Count > 0)
            {
                weights[ChapterSchedule.SpotCorner] = S(s, SpendCategory.EatingOut) + 0.5f * S(s, SpendCategory.DataAirtime)
                                                      + 0.5f * S(s, SpendCategory.Groceries);
                weights[ChapterSchedule.SpotTaxi] = S(s, SpendCategory.Transport) + S(s, SpendCategory.EHailing)
                                                    + S(s, SpendCategory.Fuel);
                weights[ChapterSchedule.SpotHub] = S(s, SpendCategory.Savings) + 0.5f * S(s, SpendCategory.LoanRepayment)
                                                   + 0.5f * S(s, SpendCategory.Cash);
                weights[ChapterSchedule.SpotShopfront] = S(s, SpendCategory.Clothing)
                                                         + 0.5f * S(s, SpendCategory.Entertainment);
                weights[ChapterSchedule.SpotGate] = S(s, SpendCategory.Family) + 0.5f * S(s, SpendCategory.Housing)
                                                    + 0.5f * S(s, SpendCategory.Groceries);
                weights[ChapterSchedule.SpotEast] = 0.5f * S(s, SpendCategory.LoanRepayment)
                                                    + 0.5f * S(s, SpendCategory.Cash)
                                                    + 0.5f * S(s, SpendCategory.Entertainment);
            }

            float total = weights.Values.Sum();
            foreach (string spot in ChapterSchedule.AllSpots)
            {
                weights.TryGetValue(spot, out float w);
                weights[spot] = total > 0f ? w / total : 1f / ChapterSchedule.AllSpots.Length;
            }

            return weights;
        }

        /// <summary>The spots by weight, heaviest first (ties keep ChapterSchedule.AllSpots order).</summary>
        public static string[] SpotsByWeight(BankHabitSummary summary)
        {
            Dictionary<string, float> weights = SpotWeights(summary);
            return ChapterSchedule.AllSpots
                .Select((spot, index) => new { spot, index, w = weights[spot] })
                .OrderByDescending(x => x.w)
                .ThenBy(x => x.index)
                .Select(x => x.spot)
                .ToArray();
        }

        // ------------------------------------------------------------------ scores

        /// <summary>Categories present (the builder maps before the source is filled in, so not HasData).</summary>
        static bool HasCategories(BankHabitSummary summary)
        {
            return summary?.categories != null && summary.categories.Length > 0;
        }

        static float S(Dictionary<string, float> scores, string category)
        {
            return scores.TryGetValue(category, out float v) ? v : 0f;
        }

        static Dictionary<string, float> DiscretionaryScores(BankHabitSummary summary)
        {
            return Scores(summary, Discretionary);
        }

        static Dictionary<string, float> AllScores(BankHabitSummary summary)
        {
            return Scores(summary, null);
        }

        /// <summary>Per category: 0.5 x share of the set's spend + 0.5 x share of the set's payments.</summary>
        static Dictionary<string, float> Scores(BankHabitSummary summary, string[] only)
        {
            var result = new Dictionary<string, float>(StringComparer.Ordinal);
            if (!HasCategories(summary))
            {
                return result;
            }

            var set = summary.categories
                .Where(c => c != null && (only == null || Array.IndexOf(only, c.category) >= 0))
                .ToList();
            float shareSum = set.Sum(c => c.share);
            float countSum = set.Sum(c => c.perWeek);
            foreach (CategoryHabit c in set)
            {
                float amount = shareSum > 0f ? c.share / shareSum : 0f;
                float count = countSum > 0f ? c.perWeek / countSum : 0f;
                result[c.category] = 0.5f * amount + 0.5f * count;
            }

            return result;
        }
    }
}
