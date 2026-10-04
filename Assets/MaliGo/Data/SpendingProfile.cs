using System;

namespace MaliGo.Data
{
    /// <summary>
    /// Where the player's money mostly goes and how they get around (design spec 2.7). In the beta it
    /// comes from two taps in character creation; a later source can fill the same object. Game code
    /// reads only focus and travel (always through SpendingFocus.Normalize / TravelMode.Normalize);
    /// the other fields are reserved.
    /// </summary>
    [Serializable]
    public class SpendingProfile
    {
        public const int CurrentVersion = 1;

        /// <summary>
        /// Shape version. A reader that meets a higher version keeps the fields it knows and ignores
        /// the rest (JsonUtility does this).
        /// </summary>
        public int version = CurrentVersion;

        /// <summary>A SpendingProfileSource value: "default" | "onboarding".</summary>
        public string source = SpendingProfileSource.Default;

        /// <summary>A SpendingFocus id: where most of the money goes.</summary>
        public string focus = SpendingFocus.Food;

        /// <summary>A TravelMode id: how the player usually gets around.</summary>
        public string travel = TravelMode.Taxi;

        /// <summary>0 = never set by the player.</summary>
        public long updatedUnixSeconds;

        // Reserved for a summarised bank-data source. Empty in the beta; no game code reads them.
        public CategoryShare[] categoryShares = Array.Empty<CategoryShare>();
        public RecurringDebit[] recurringDebits = Array.Empty<RecurringDebit>();

        /// <summary>0 = unknown.</summary>
        public int paydayDayOfMonth;
    }

    /// <summary>Reserved (bank-data source). Not read by the beta.</summary>
    [Serializable]
    public class CategoryShare
    {
        public string category = "";
        public float share;
        public float typicalMonthlyAmount;
    }

    /// <summary>Reserved (bank-data source). Not read by the beta.</summary>
    [Serializable]
    public class RecurringDebit
    {
        public string label = "";
        public string category = "";
        public float amount;
        public int dayOfMonth;
    }

    /// <summary>One card on the profile screen: its id, card label and the phrase Mali uses.</summary>
    public struct ProfileOption
    {
        public string id;
        public string cardLabel;
        public string maliPhrase;
    }

    /// <summary>Where most of the player's money goes.</summary>
    public static class SpendingFocus
    {
        public const string Food = "food", Transport = "transport", DataSocial = "data_social", HomeFamily = "home_family";

        public static readonly ProfileOption[] All =
        {
            new ProfileOption { id = Food, cardLabel = "Food and takeaways", maliPhrase = "food and takeaways" },
            new ProfileOption { id = Transport, cardLabel = "Getting around", maliPhrase = "getting around" },
            new ProfileOption { id = DataSocial, cardLabel = "Data, airtime and going out", maliPhrase = "data, airtime and going out" },
            new ProfileOption { id = HomeFamily, cardLabel = "Home and family", maliPhrase = "home and family" }
        };

        /// <summary>One of the four ids, exactly; anything else (null, empty, other case) gives "food".</summary>
        public static string Normalize(string id)
        {
            foreach (ProfileOption option in All)
            {
                if (string.Equals(option.id, id, StringComparison.Ordinal))
                {
                    return option.id;
                }
            }

            return Food;
        }

        /// <summary>The option for an id (normalised first).</summary>
        public static ProfileOption Get(string id)
        {
            string normalized = Normalize(id);
            foreach (ProfileOption option in All)
            {
                if (option.id == normalized)
                {
                    return option;
                }
            }

            return All[0];
        }

        /// <summary>
        /// The money categories that make up this focus (for Mali's chapter-end note): food -> Food;
        /// transport -> Transport; data_social -> Phone &amp; data, Friends; home_family -> Home, Family.
        /// Values are MaliGo.Economy.MoneyCategory strings. A new array on every call.
        /// </summary>
        public static string[] Categories(string id)
        {
            switch (Normalize(id))
            {
                case Transport:
                    return new[] { "Transport" };
                case DataSocial:
                    return new[] { "Phone & data", "Friends" };
                case HomeFamily:
                    return new[] { "Home", "Family" };
                default:
                    return new[] { "Food" };
            }
        }
    }

    /// <summary>How the player usually gets around.</summary>
    public static class TravelMode
    {
        public const string Taxi = "taxi", EHailing = "ehailing", Walk = "walk", Car = "car";

        public static readonly ProfileOption[] All =
        {
            new ProfileOption { id = Taxi, cardLabel = "Minibus taxi", maliPhrase = "minibus taxi" },
            new ProfileOption { id = EHailing, cardLabel = "E-hailing rides", maliPhrase = "e-hailing rides" },
            new ProfileOption { id = Walk, cardLabel = "Mostly on foot", maliPhrase = "mostly on foot" },
            new ProfileOption { id = Car, cardLabel = "Own or shared car", maliPhrase = "own or shared car" }
        };

        /// <summary>One of the four ids, exactly; anything else (null, empty, other case) gives "taxi".</summary>
        public static string Normalize(string id)
        {
            foreach (ProfileOption option in All)
            {
                if (string.Equals(option.id, id, StringComparison.Ordinal))
                {
                    return option.id;
                }
            }

            return Taxi;
        }

        /// <summary>The option for an id (normalised first).</summary>
        public static ProfileOption Get(string id)
        {
            string normalized = Normalize(id);
            foreach (ProfileOption option in All)
            {
                if (option.id == normalized)
                {
                    return option;
                }
            }

            return All[0];
        }
    }

    public static class SpendingProfileSource
    {
        public const string Default = "default", Onboarding = "onboarding";
    }

    public static class SpendingProfiles
    {
        /// <summary>
        /// The two taps: normalises both ids, sets source "onboarding" and updatedUnixSeconds. Pure
        /// (the caller passes the time). A null profile is ignored.
        /// </summary>
        public static void SetFromOnboarding(SpendingProfile p, string focus, string travel, long nowUnixSeconds)
        {
            if (p == null)
            {
                return;
            }

            p.focus = SpendingFocus.Normalize(focus);
            p.travel = TravelMode.Normalize(travel);
            p.source = SpendingProfileSource.Onboarding;
            p.updatedUnixSeconds = nowUnixSeconds;
            if (p.version < 1)
            {
                p.version = SpendingProfile.CurrentVersion;
            }
        }
    }
}
