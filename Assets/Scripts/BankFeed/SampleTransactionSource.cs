using System;
using System.Collections.Generic;
using System.Globalization;

namespace MaliGo.BankFeed
{
    /// <summary>One made-up person the sample source can play.</summary>
    public struct SamplePersona
    {
        public string id;

        /// <summary>Shown on the summary screen, always with the "sample" label.</summary>
        public string label;
    }

    /// <summary>
    /// Made-up transactions for trying the bank feed without a live connection (docs/BANK_FEED.md). Five personas,
    /// 60 days each, with Rand amounts and South African spending patterns; merchant names are generic. Generated in
    /// code from a fixed seed and a fixed end date, so the same persona always gives the same transactions. Calls
    /// back at once. IsSample is true: the UI labels everything from it as sample data.
    /// </summary>
    public sealed class SampleTransactionSource : ITransactionSource
    {
        public const string StudentId = "student_res";
        public const string CommuterId = "shop_commuter";
        public const string CreatorId = "content_creator";
        public const string BreadwinnerId = "family_breadwinner";
        public const string DriverId = "office_driver";

        public const int WindowDays = 60;

        public static readonly SamplePersona[] Personas =
        {
            new SamplePersona { id = StudentId, label = "Student living in res" },
            new SamplePersona { id = CommuterId, label = "Shop assistant who commutes" },
            new SamplePersona { id = CreatorId, label = "Freelance content creator" },
            new SamplePersona { id = BreadwinnerId, label = "Care worker supporting family" },
            new SamplePersona { id = DriverId, label = "Office worker with a car" },
        };

        /// <summary>The last day of every persona's window (fixed, so the data never changes).</summary>
        public static readonly DateTimeOffset EndDate = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.FromHours(2));

        public static DateTimeOffset StartDate => EndDate.AddDays(-(WindowDays - 1));

        readonly string personaId;
        bool consented;

        public SampleTransactionSource(string personaId)
        {
            this.personaId = Persona(personaId).id;
        }

        public string SourceId => "sample";

        public bool IsSample => true;

        public string PersonaId => personaId;

        /// <summary>The persona for an id; the first one for an unknown id.</summary>
        public static SamplePersona Persona(string id)
        {
            foreach (SamplePersona p in Personas)
            {
                if (p.id == id)
                {
                    return p;
                }
            }

            return Personas[0];
        }

        /// <summary>The persona after <paramref name="id"/> (wrapping), for "try another sample person".</summary>
        public static string NextPersonaId(string id)
        {
            for (int i = 0; i < Personas.Length; i++)
            {
                if (Personas[i].id == id)
                {
                    return Personas[(i + 1) % Personas.Length].id;
                }
            }

            return Personas[0].id;
        }

        public void RequestConsent(ObConsentRequest request, Action<ConsentResult> done)
        {
            // Nothing to ask a real bank: the player already agreed on the consent screen.
            consented = true;
            done?.Invoke(new ConsentResult { Granted = true });
        }

        public void GetAccounts(Action<AccountsResult> done)
        {
            var result = new AccountsResult { Ok = consented };
            if (consented)
            {
                result.Accounts.Add(new ObAccount
                {
                    AccountId = AccountIdFor(personaId),
                    Currency = "ZAR",
                    AccountType = "Personal",
                    AccountSubType = "CurrentAccount",
                    Nickname = "Everyday account"
                });
            }
            else
            {
                result.Error = "no consent";
            }

            done?.Invoke(result);
        }

        public void GetTransactions(string accountId, DateTimeOffset from, DateTimeOffset to, Action<TransactionsResult> done)
        {
            var result = new TransactionsResult { Ok = consented && accountId == AccountIdFor(personaId) };
            if (result.Ok)
            {
                foreach (ObTransaction t in Generate(personaId))
                {
                    if (t.TryGetBookingTime(out DateTimeOffset when) && when >= from && when <= to)
                    {
                        result.Transactions.Add(t);
                    }
                }
            }
            else
            {
                result.Error = consented ? "unknown account" : "no consent";
            }

            done?.Invoke(result);
        }

        public void Disconnect()
        {
            consented = false;
        }

        static string AccountIdFor(string id) => "sample-" + id + "-01";

        // ================================================================ generation

        /// <summary>All of a persona's transactions over the window, oldest first.</summary>
        public static List<ObTransaction> Generate(string personaId)
        {
            string id = Persona(personaId).id;
            var g = new Gen(id);
            switch (id)
            {
                case CommuterId: Commuter(g); break;
                case CreatorId: Creator(g); break;
                case BreadwinnerId: Breadwinner(g); break;
                case DriverId: Driver(g); break;
                default: Student(g); break;
            }

            g.list.Sort((a, b) => string.CompareOrdinal(a.BookingDateTime, b.BookingDateTime));
            return g.list;
        }

        // Kota most weekdays, walks to class, a taxi to town on Saturdays, small data bundles, a monthly allowance.
        static void Student(Gen g)
        {
            g.Monthly(25, "Bursary allowance", "", "Bursary allowance", 1650m, credit: true);
            g.Monthly(1, "Video streaming", "4899", "Streaming subscription", 59m);
            g.Days(d => Weekday(d), 0.8f, "Corner Kota", "5814", "Kota and chips", 38, 55, 12);
            g.Days(d => !Weekday(d), 0.5f, "Corner Kota", "5814", "Kota", 35, 50, 14);
            g.Days(d => d.DayOfWeek == DayOfWeek.Saturday, 0.9f, "Taxi rank fare", "4111", "Taxi fare to town", 15, 15, 10);
            g.Days(d => d.DayOfWeek == DayOfWeek.Saturday, 0.9f, "Taxi rank fare", "4111", "Taxi fare back", 15, 15, 17);
            g.Days(d => true, 0.25f, "Data bundle", "4814", "Data bundle", 29, 99, 20);
            g.Days(d => d.DayOfWeek == DayOfWeek.Sunday, 0.9f, "Spaza shop", "5499", "Bread and milk", 45, 120, 11);
            g.Once(19, "Fashion outlet", "5651", "Sneakers", 349m);
            g.Once(41, "Cinema", "7832", "Movie ticket", 85m);
        }

        // Two taxi fares six days a week, salary on the 25th, groceries on payday, money home, stokvel, backroom rent.
        static void Commuter(Gen g)
        {
            g.Monthly(25, "Employer", "", "Salary", 5800m, credit: true);
            g.Monthly(1, "Landlord", "", "Backroom rent", 1500m);
            g.Monthly(26, "Supermarket", "5411", "Month-end groceries", 950m);
            g.Monthly(26, "Money transfer", "4829", "Send money home", 500m);
            g.Monthly(27, "Stokvel", "6012", "Stokvel contribution", 300m);
            g.Days(d => d.DayOfWeek != DayOfWeek.Sunday, 0.95f, "Taxi rank fare", "4111", "Taxi fare to work", 18, 22, 6);
            g.Days(d => d.DayOfWeek != DayOfWeek.Sunday, 0.95f, "Taxi rank fare", "4111", "Taxi fare home", 18, 22, 18);
            g.Days(d => Weekday(d), 0.3f, "Corner Kota", "5814", "Kota", 35, 50, 13);
            g.Days(d => d.DayOfWeek == DayOfWeek.Monday, 1f, "Data bundle", "4814", "Weekly data", 49, 49, 7);
            g.Days(d => d.DayOfWeek == DayOfWeek.Sunday, 0.8f, "Spaza shop", "5499", "Bread, eggs, airtime", 60, 140, 10);
        }

        // Irregular client payments, lots of data, streaming, weekend outings, ride app trips.
        static void Creator(Gen g)
        {
            g.Once(3, "Client payment", "", "Invoice paid", 2400m, credit: true);
            g.Once(14, "Client payment", "", "Invoice paid", 1250m, credit: true);
            g.Once(29, "Client payment", "", "Invoice paid", 3500m, credit: true);
            g.Once(47, "Client payment", "", "Invoice paid", 1800m, credit: true);
            g.Monthly(5, "Video streaming", "4899", "Streaming subscription", 99m);
            g.Monthly(5, "Music streaming", "5815", "Music subscription", 60m);
            g.Monthly(1, "Shared flat", "", "Room rent", 2800m);
            g.Days(d => true, 0.5f, "Data bundle", "4814", "Data bundle", 49, 149, 9);
            g.Days(d => true, 0.15f, "Airtime", "4814", "Airtime top-up", 20, 50, 15);
            g.Days(d => d.DayOfWeek == DayOfWeek.Friday || d.DayOfWeek == DayOfWeek.Saturday, 0.7f, "Corner tavern",
                "5813", "Drinks with friends", 80, 250, 21);
            g.Days(d => true, 0.4f, "Ride app trip", "4121", "Ride app trip", 45, 95, 19);
            g.Days(d => true, 0.25f, "Burger spot", "5814", "Burger and chips", 60, 110, 13);
            g.Days(d => d.DayOfWeek == DayOfWeek.Sunday, 0.7f, "Supermarket", "5411", "Groceries", 180, 320, 11);
            g.Once(12, "Cinema", "7832", "Movie tickets", 170m);
            g.Once(36, "Fashion outlet", "5651", "Jacket", 450m);
            g.Once(52, "Event tickets", "7922", "Concert ticket", 250m);
        }

        // Salary on the 15th, money home twice a month, weekly groceries and cash, electricity, a loan, mostly on foot.
        static void Breadwinner(Gen g)
        {
            g.Monthly(15, "Employer", "", "Salary", 9200m, credit: true);
            g.Monthly(16, "Money transfer", "4829", "Money home to gogo", 1200m);
            g.Monthly(1, "Money transfer", "4829", "Money home to gogo", 1200m);
            g.Monthly(3, "Micro lender", "6012", "Loan repayment", 650m);
            g.Monthly(2, "Landlord", "", "Room rent", 1800m);
            g.Days(d => d.Day == 2 || d.Day == 17, 1f, "Prepaid electricity", "4900", "Prepaid electricity", 300, 300, 8);
            g.Days(d => d.DayOfWeek == DayOfWeek.Saturday, 1f, "Supermarket", "5411", "Weekly groceries", 450, 700, 10);
            g.Days(d => d.DayOfWeek == DayOfWeek.Wednesday || d.DayOfWeek == DayOfWeek.Sunday, 0.8f, "Spaza shop",
                "5499", "Bread and milk", 40, 90, 17);
            g.Days(d => d.DayOfWeek == DayOfWeek.Tuesday || d.DayOfWeek == DayOfWeek.Friday, 0.7f, "ATM", "6011",
                "Cash withdrawal", 200, 400, 17);
            g.Days(d => d.DayOfWeek == DayOfWeek.Monday, 0.9f, "Data bundle", "4814", "Weekly data", 29, 29, 7);
            g.Days(d => true, 0.05f, "Taxi rank fare", "4111", "Taxi fare", 16, 16, 9);
            g.Days(d => true, 0.04f, "Corner Kota", "5814", "Kota", 40, 50, 13);
        }

        // Salary on the 28th, weekly fuel, lunches out, a phone contract, a store account, gym, rent.
        static void Driver(Gen g)
        {
            g.Monthly(28, "Employer", "", "Salary", 14000m, credit: true);
            g.Monthly(1, "Property rentals", "6513", "Flat rent", 4200m);
            g.Monthly(1, "Phone contract", "4814", "Phone and data contract", 199m);
            g.Monthly(5, "Store account", "6012", "Store account instalment", 420m);
            g.Monthly(2, "Gym", "7997", "Gym membership", 299m);
            g.Monthly(7, "Car insurance", "6300", "Car insurance", 650m);
            g.Days(d => d.DayOfWeek == DayOfWeek.Monday || d.DayOfWeek == DayOfWeek.Thursday, 0.9f, "Fuel station",
                "5541", "Fuel", 350, 600, 7);
            g.Days(d => Weekday(d), 0.45f, "Lunch canteen", "5812", "Lunch", 65, 95, 13);
            g.Days(d => d.DayOfWeek == DayOfWeek.Saturday, 0.9f, "Supermarket", "5411", "Groceries", 250, 450, 11);
            g.Once(22, "Fashion outlet", "5691", "Work shirts", 600m);
        }

        static bool Weekday(DateTimeOffset d) => d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday;

        /// <summary>Builds one persona's list with a seeded random sequence.</summary>
        sealed class Gen
        {
            public readonly List<ObTransaction> list = new List<ObTransaction>();
            readonly string id;
            uint state;
            int next;

            public Gen(string personaId)
            {
                id = personaId;
                // FNV-1a of the id: a fixed seed per persona.
                state = 2166136261u;
                foreach (char c in personaId)
                {
                    state = (state ^ c) * 16777619u;
                }

                if (state == 0)
                {
                    state = 1;
                }
            }

            /// <summary>xorshift32 in [0, 1). Our own, so the numbers are the same on every runtime.</summary>
            float NextFloat()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state >> 8) / 16777216f;
            }

            int Range(int min, int max) => min + (int)(NextFloat() * (max - min + 1));

            /// <summary>On each day of the window that passes <paramref name="when"/>, with this chance.</summary>
            public void Days(Func<DateTimeOffset, bool> when, float chance, string merchant, string mcc, string info,
                int min, int max, int hour)
            {
                for (int i = 0; i < WindowDays; i++)
                {
                    DateTimeOffset day = StartDate.AddDays(i);
                    float roll = NextFloat();
                    int amount = Range(min, max);
                    if (when(day) && roll < chance)
                    {
                        Add(day, hour, merchant, mcc, info, amount, false);
                    }
                }
            }

            /// <summary>On this day of every month in the window.</summary>
            public void Monthly(int dayOfMonth, string merchant, string mcc, string info, decimal amount, bool credit = false)
            {
                for (int i = 0; i < WindowDays; i++)
                {
                    DateTimeOffset day = StartDate.AddDays(i);
                    if (day.Day == dayOfMonth)
                    {
                        Add(day, 9, merchant, mcc, info, amount, credit);
                    }
                }
            }

            /// <summary>Once, <paramref name="dayIndex"/> days into the window.</summary>
            public void Once(int dayIndex, string merchant, string mcc, string info, decimal amount, bool credit = false)
            {
                Add(StartDate.AddDays(Math.Max(0, Math.Min(WindowDays - 1, dayIndex))), 12, merchant, mcc, info, amount,
                    credit);
            }

            void Add(DateTimeOffset day, int hour, string merchant, string mcc, string info, decimal amount, bool credit)
            {
                DateTimeOffset when = day.AddHours(hour).AddMinutes(Range(0, 59));
                next++;
                list.Add(new ObTransaction
                {
                    TransactionId = id + "-" + next.ToString("D4", CultureInfo.InvariantCulture),
                    AccountId = AccountIdFor(id),
                    Amount = new ObAmount
                    {
                        Amount = amount.ToString("0.00", CultureInfo.InvariantCulture),
                        Currency = "ZAR"
                    },
                    CreditDebitIndicator = credit ? ObCreditDebit.Credit : ObCreditDebit.Debit,
                    Status = ObStatus.Booked,
                    BookingDateTime = when.ToString("yyyy-MM-dd'T'HH:mm:ssK", CultureInfo.InvariantCulture),
                    TransactionInformation = info,
                    MerchantDetails = new ObMerchantDetails { MerchantName = merchant, MerchantCategoryCode = mcc }
                });
            }
        }
    }
}
