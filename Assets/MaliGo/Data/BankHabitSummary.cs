using System;

namespace MaliGo.Data
{
    /// <summary>
    /// The only bank-feed data the game keeps (docs/BANK_FEED.md): spending categories and habit flags worked out
    /// on the phone from the player's transactions. No transaction, merchant name, account id or amount of a single
    /// payment is ever stored here; only shares, rates and typical (median) amounts per category.
    ///
    /// Version-safe: a save without this field loads it as a new, empty summary (JsonUtility runs the field
    /// initialiser), so old saves keep loading and the save version does not change. Empty = <see cref="HasData"/>
    /// false. "Forget my bank data" replaces it with a new, empty one.
    /// </summary>
    [Serializable]
    public class BankHabitSummary
    {
        public const int CurrentVersion = 1;

        /// <summary>Shape version; a reader that meets a higher one keeps the fields it knows.</summary>
        public int version = CurrentVersion;

        /// <summary>A BankHabitSource value: "" (none), "sample" or "live".</summary>
        public string source = "";

        /// <summary>The sample persona's id when <see cref="source"/> is "sample"; "" otherwise.</summary>
        public string samplePersonaId = "";

        /// <summary>When the summary was made (0 = never).</summary>
        public long createdUnixSeconds;

        /// <summary>How many days of transactions the summary covers.</summary>
        public int windowDays;

        /// <summary>Debits that counted as spending (a count only).</summary>
        public int spendCount;

        /// <summary>Spending categories, largest share of spend first.</summary>
        public CategoryHabit[] categories = Array.Empty<CategoryHabit>();

        /// <summary>Day of the month the main income usually arrives; 0 = not detectable.</summary>
        public int paydayDayOfMonth;

        /// <summary>An IncomeKind value: "" (none seen), "salary", "allowance", "bursary", "grant", "irregular".</summary>
        public string incomeKind = "";

        public float cashWithdrawalsPerWeek;
        public float typicalCashWithdrawal;

        public bool eatsOutOften;
        public bool taxiCommuter;
        public bool dataHeavy;
        public bool cashUser;
        public bool sendsMoneyHome;
        public bool hasLoanRepayments;

        /// <summary>The SpendingFocus / TravelMode ids the habits point to (what the two taps are pre-filled with).</summary>
        public string suggestedFocus = "";
        public string suggestedTravel = "";

        public bool HasData => !string.IsNullOrEmpty(source) && categories != null && categories.Length > 0;

        public bool IsSample => source == BankHabitSource.Sample;

        /// <summary>The habit of a category, or null.</summary>
        public CategoryHabit Get(string category)
        {
            if (categories == null)
            {
                return null;
            }

            foreach (CategoryHabit habit in categories)
            {
                if (habit != null && habit.category == category)
                {
                    return habit;
                }
            }

            return null;
        }
    }

    /// <summary>One spending category in a <see cref="BankHabitSummary"/>.</summary>
    [Serializable]
    public class CategoryHabit
    {
        /// <summary>A MaliGo.BankFeed.SpendCategory id, e.g. "eating_out".</summary>
        public string category = "";

        /// <summary>Share of all spending, 0..1.</summary>
        public float share;

        /// <summary>Payments per week.</summary>
        public float perWeek;

        /// <summary>Typical (median) payment in Rand, rounded.</summary>
        public float typicalAmount;
    }

    public static class BankHabitSource
    {
        public const string None = "", Sample = "sample", Live = "live";
    }

    public static class BankHabits
    {
        /// <summary>"Forget my bank data": a new, empty summary. The spending profile (the player's taps) stays.</summary>
        public static void Forget(PlayerData data)
        {
            if (data != null)
            {
                data.bankHabits = new BankHabitSummary();
            }
        }

        public static bool Has(PlayerData data) => data?.bankHabits != null && data.bankHabits.HasData;
    }
}
