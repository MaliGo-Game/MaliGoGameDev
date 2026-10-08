using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MaliGo.BankFeed;
using MaliGo.Data;
using MaliGo.Scenarios;

/// <summary>The bank feed pipeline (docs/BANK_FEED.md): OB parser, categoriser, habit summary, profile/spot mapping.</summary>
public static class BankFeedTests
{
    const string SampleJson = @"{
  ""Data"": {
    ""Transaction"": [
      {
        ""AccountId"": ""acc-1"",
        ""TransactionId"": ""t-1"",
        ""Amount"": { ""Amount"": ""45.50"", ""Currency"": ""ZAR"" },
        ""CreditDebitIndicator"": ""Debit"",
        ""Status"": ""Booked"",
        ""BookingDateTime"": ""2026-09-14T12:31:00+02:00"",
        ""TransactionInformation"": ""Kota and chips"",
        ""MerchantDetails"": { ""MerchantName"": ""Corner Kota"", ""MerchantCategoryCode"": ""5814"" },
        ""BankTransactionCode"": { ""Code"": ""ReceivedCreditTransfer"", ""SubCode"": ""DomesticCreditTransfer"" }
      },
      {
        ""AccountId"": ""acc-1"",
        ""TransactionId"": ""t-2"",
        ""Amount"": { ""Amount"": ""5800.00"", ""Currency"": ""ZAR"" },
        ""CreditDebitIndicator"": ""Credit"",
        ""Status"": ""Booked"",
        ""BookingDateTime"": ""2026-09-25T08:00:00+02:00"",
        ""TransactionInformation"": ""Salary & bonus \""Sept\"""",
        ""Unknown"": [1, 2, { ""nested"": true }, null]
      },
      {
        ""AccountId"": ""acc-1"",
        ""TransactionId"": ""t-3"",
        ""Amount"": { ""Amount"": 18, ""Currency"": ""ZAR"" },
        ""CreditDebitIndicator"": ""Debit"",
        ""Status"": ""Pending"",
        ""BookingDateTime"": ""2026-09-26T06:10:00+02:00"",
        ""MerchantDetails"": { ""MerchantName"": ""Taxi rank fare"" }
      }
    ]
  },
  ""Links"": { ""Self"": ""/accounts/acc-1/transactions"" },
  ""Meta"": { ""TotalPages"": 1 }
}";

    static ObTransaction Tx(string merchant, string mcc, string amount, bool credit = false, string info = "",
        string when = "2026-09-14T12:00:00+02:00", string status = "Booked")
    {
        return new ObTransaction
        {
            TransactionId = Guid.NewGuid().ToString("N"),
            AccountId = "acc",
            Amount = new ObAmount { Amount = amount, Currency = "ZAR" },
            CreditDebitIndicator = credit ? "Credit" : "Debit",
            Status = status,
            BookingDateTime = when,
            TransactionInformation = info,
            MerchantDetails = new ObMerchantDetails { MerchantName = merchant, MerchantCategoryCode = mcc }
        };
    }

    static string Cat(string merchant, string mcc, string info = "") =>
        TransactionCategoriser.Categorise(Tx(merchant, mcc, "10.00", false, info)).Category;

    static BankHabitSummary Persona(string id)
    {
        BankFeedOutcome outcome = null;
        BankFeedSession.RunSample(id, 1700000000L, o => outcome = o);
        Assert.True(outcome != null && outcome.Ok, "sample run for " + id + " finished ok");
        return outcome.Summary;
    }

    // ------------------------------------------------------------------ parser

    public static void TestParserReadsObEnvelope()
    {
        Assert.True(ObJsonParser.TryParseTransactions(SampleJson, out List<ObTransaction> list), "parses");
        Assert.True(list.Count == 3, "3 transactions, got " + list.Count);

        ObTransaction kota = list[0];
        Assert.Equal("t-1", kota.TransactionId, "id");
        Assert.Equal("acc-1", kota.AccountId, "account");
        Assert.Equal("45.50", kota.Amount.Amount, "amount string kept");
        Assert.True(kota.Amount.Value == 45.50m, "amount as decimal");
        Assert.Equal("ZAR", kota.Amount.Currency, "currency");
        Assert.True(kota.IsDebit && !kota.IsCredit && kota.IsBooked, "debit, booked");
        Assert.Equal("Corner Kota", kota.MerchantDetails.MerchantName, "merchant");
        Assert.Equal("5814", kota.MerchantDetails.MerchantCategoryCode, "mcc");
        Assert.True(kota.BankTransactionCode != null && kota.BankTransactionCode.Code == "ReceivedCreditTransfer", "BTC");
        Assert.True(kota.TryGetBookingTime(out DateTimeOffset when) && when.Day == 14 && when.Offset == TimeSpan.FromHours(2),
            "booking time");

        ObTransaction salary = list[1];
        Assert.True(salary.IsCredit, "credit");
        Assert.Equal("Salary & bonus \"Sept\"", salary.TransactionInformation, "escapes");
        Assert.True(salary.BankTransactionCode == null, "BTC optional");
        Assert.Equal("", salary.MerchantDetails.MerchantName, "missing merchant -> default");

        ObTransaction taxi = list[2];
        Assert.True(!taxi.IsBooked, "pending");
        Assert.True(taxi.Amount.Value == 18m, "bare number amount accepted");
    }

    public static void TestParserRejectsMalformedAndAcceptsEmpty()
    {
        Assert.True(!ObJsonParser.TryParseTransactions("", out var a) && a.Count == 0, "empty string");
        Assert.True(!ObJsonParser.TryParseTransactions("{\"Data\": {\"Transaction\": [ {\"a\": 1}, ]}}", out _), "trailing comma");
        Assert.True(!ObJsonParser.TryParseTransactions("{\"Data\": {\"Transaction\": 5}}", out _), "not an array");
        Assert.True(!ObJsonParser.TryParseTransactions("[1,2]", out _), "no envelope");
        Assert.True(!ObJsonParser.TryParseTransactions("{\"Data\":{}} extra", out _), "trailing text");
        Assert.True(ObJsonParser.TryParseTransactions("{\"Data\":{}}", out var none) && none.Count == 0, "empty page");
        Assert.True(ObJsonParser.TryParseAccounts(
            "{\"Data\":{\"Account\":[{\"AccountId\":\"a1\",\"Currency\":\"ZAR\",\"Nickname\":\"Everyday\"}]}}",
            out List<ObAccount> accounts) && accounts.Count == 1 && accounts[0].AccountId == "a1", "accounts");
    }

    // ------------------------------------------------------------------ categoriser

    public static void TestCategoriserMccFirst()
    {
        Assert.Equal(SpendCategory.EatingOut, Cat("Anything", "5812"), "5812");
        Assert.Equal(SpendCategory.EatingOut, Cat("Anything", "5814"), "5814");
        Assert.Equal(SpendCategory.Transport, Cat("Anything", "4111"), "4111");
        Assert.Equal(SpendCategory.Transport, Cat("Anything", "4121"), "4121");
        Assert.Equal(SpendCategory.Transport, Cat("Anything", "4131"), "4131");
        Assert.Equal(SpendCategory.EHailing, Cat("Ride app trip", "4121"), "4121 ride app");
        Assert.Equal(SpendCategory.DataAirtime, Cat("Anything", "4814"), "4814");
        Assert.Equal(SpendCategory.DataAirtime, Cat("Anything", "4816"), "4816");
        Assert.Equal(SpendCategory.Groceries, Cat("Anything", "5411"), "5411");
        Assert.Equal(SpendCategory.Groceries, Cat("Anything", "5499"), "5499");
        Assert.Equal(SpendCategory.Fuel, Cat("Anything", "5541"), "5541");
        Assert.Equal(SpendCategory.Fuel, Cat("Anything", "5542"), "5542");
        Assert.Equal(SpendCategory.Clothing, Cat("Anything", "5651"), "5651");
        Assert.Equal(SpendCategory.Clothing, Cat("Anything", "5691"), "5691");
        Assert.Equal(SpendCategory.Entertainment, Cat("Anything", "7832"), "7832");
        Assert.Equal(SpendCategory.Entertainment, Cat("Anything", "7841"), "7841");
        Assert.Equal(SpendCategory.Entertainment, Cat("Anything", "7994"), "7994");
        Assert.Equal(SpendCategory.Cash, Cat("Anything", "6011"), "6011");
        // The code beats a misleading name.
        Assert.Equal(SpendCategory.Groceries, Cat("Kota corner store", "5411"), "mcc wins over keyword");
        Assert.Equal("mcc", TransactionCategoriser.Categorise(Tx("x", "5814", "1.00")).Rule, "rule mcc");
    }

    public static void TestCategoriserKeywordsThenOther()
    {
        Assert.Equal(SpendCategory.EatingOut, Cat("Corner Kota", ""), "kota");
        Assert.Equal(SpendCategory.Transport, Cat("Taxi rank fare", ""), "taxi");
        Assert.Equal(SpendCategory.DataAirtime, Cat("Data bundle", ""), "data");
        Assert.Equal(SpendCategory.Groceries, Cat("Spaza shop", ""), "spaza");
        Assert.Equal(SpendCategory.Fuel, Cat("Fuel station", ""), "fuel");
        Assert.Equal(SpendCategory.Housing, Cat("Landlord", "", "Backroom rent"), "rent");
        Assert.Equal(SpendCategory.Savings, Cat("Stokvel", "6012", "Stokvel contribution"), "6012 -> keyword stokvel");
        Assert.Equal(SpendCategory.LoanRepayment, Cat("Micro lender", "6012", "Loan repayment"), "6012 -> loan");
        Assert.Equal(SpendCategory.Family, Cat("Money transfer", "4829", "Send money home"), "money home");
        Assert.Equal(SpendCategory.Other, Cat("Transfer to parent", "6012", "Current account"), "no false 'rent'");
        Assert.Equal(SpendCategory.Other, Cat("Coffee cart", ""), "no false 'fee'");
        Assert.Equal(SpendCategory.Other, Cat("Something odd", "1234"), "unknown -> other");
        Assert.Equal("keyword", TransactionCategoriser.Categorise(Tx("Corner Kota", "", "1.00")).Rule, "rule keyword");
    }

    public static void TestCreditsAreIncomeNotSpend()
    {
        Categorised salary = TransactionCategoriser.Categorise(Tx("Employer", "", "5800.00", true, "Salary"));
        Assert.True(salary.IsIncome && !salary.IsSpend && salary.IncomeKind == IncomeKind.Salary, "salary");
        Categorised bursary = TransactionCategoriser.Categorise(Tx("", "", "1650.00", true, "Bursary allowance"));
        Assert.Equal(IncomeKind.Bursary, bursary.IncomeKind, "bursary");
        Categorised refund = TransactionCategoriser.Categorise(Tx("Fashion outlet", "5651", "349.00", true, "Refund"));
        Assert.True(!refund.IsIncome && !refund.IsSpend, "refund is neither");
        Categorised gig = TransactionCategoriser.Categorise(Tx("Client payment", "", "1200.00", true));
        Assert.Equal(IncomeKind.Irregular, gig.IncomeKind, "other credit -> irregular income");
    }

    // ------------------------------------------------------------------ habit summary

    public static void TestSummaryFromHandMadeTransactions()
    {
        var list = new List<ObTransaction>();
        for (int day = 1; day <= 14; day++)
        {
            string date = "2026-09-" + day.ToString("00") + "T12:00:00+02:00";
            list.Add(Tx("Corner Kota", "5814", day % 2 == 0 ? "40.00" : "50.00", when: date));
            list.Add(Tx("Taxi rank fare", "4111", "15.00", when: date));
        }

        list.Add(Tx("Data bundle", "4814", "99.00", when: "2026-09-03T09:00:00+02:00"));
        list.Add(Tx("Corner Kota", "5814", "500.00", when: "2026-09-03T09:00:00+02:00", status: "Pending"));
        list.Add(new ObTransaction
        {
            Amount = new ObAmount { Amount = "100.00", Currency = "USD" }, Status = "Booked",
            BookingDateTime = "2026-09-04T09:00:00+02:00", CreditDebitIndicator = "Debit"
        });

        BankHabitSummary s = HabitSummaryBuilder.Build(list, 14);
        Assert.True(s.windowDays == 14, "window");
        Assert.True(s.spendCount == 29, "pending and non-Rand left out: " + s.spendCount);
        CategoryHabit eat = s.Get(SpendCategory.EatingOut);
        Assert.Equal(7f, eat.perWeek, "7 kotas a week");
        Assert.Equal(45f, eat.typicalAmount, "median kota");
        Assert.Equal(630f / (630f + 210f + 99f), eat.share, "kota share", 0.001f);
        Assert.Equal(SpendCategory.EatingOut, s.categories[0].category, "largest share first");
        Assert.True(s.eatsOutOften && s.taxiCommuter, "flags");
        Assert.True(!s.cashUser && !s.sendsMoneyHome && !s.hasLoanRepayments, "no other flags");
        Assert.True(s.paydayDayOfMonth == 0 && s.incomeKind == "", "no income");
        float total = s.categories.Sum(c => c.share);
        Assert.Equal(1f, total, "shares sum to 1", 0.001f);
    }

    public static void TestPaydayDetection()
    {
        var steady = new List<ObTransaction>
        {
            Tx("Employer", "", "5800.00", true, "Salary", "2026-08-25T08:00:00+02:00"),
            Tx("Employer", "", "5800.00", true, "Salary", "2026-09-26T08:00:00+02:00"),
            Tx("Friend", "", "200.00", true, "Thanks", "2026-09-10T08:00:00+02:00"),
            Tx("Corner Kota", "5814", "40.00"),
        };
        BankHabitSummary s = HabitSummaryBuilder.Build(steady, 60);
        Assert.True(s.paydayDayOfMonth == 25 || s.paydayDayOfMonth == 26, "payday ~25th, got " + s.paydayDayOfMonth);
        Assert.Equal(IncomeKind.Salary, s.incomeKind, "salary");

        var wrap = new List<ObTransaction>
        {
            Tx("Employer", "", "9000.00", true, "Salary", "2026-08-31T08:00:00+02:00"),
            Tx("Employer", "", "9000.00", true, "Salary", "2026-10-01T08:00:00+02:00"),
            Tx("Corner Kota", "5814", "40.00"),
        };
        Assert.True(HabitSummaryBuilder.Build(wrap, 60).paydayDayOfMonth > 0, "month end wraps");

        var scattered = new List<ObTransaction>
        {
            Tx("Client", "", "2000.00", true, "", "2026-08-03T08:00:00+02:00"),
            Tx("Client", "", "1800.00", true, "", "2026-08-17T08:00:00+02:00"),
            Tx("Corner Kota", "5814", "40.00"),
        };
        Assert.True(HabitSummaryBuilder.Build(scattered, 60).paydayDayOfMonth == 0, "no payday when scattered");

        var once = new List<ObTransaction> { Tx("Employer", "", "5800.00", true, "Salary"), Tx("Corner Kota", "5814", "40.00") };
        Assert.True(HabitSummaryBuilder.Build(once, 30).paydayDayOfMonth == 0, "one credit: not detectable");
    }

    public static void TestEmptyInputGivesEmptySummary()
    {
        BankHabitSummary s = HabitSummaryBuilder.Build(new List<ObTransaction>(), 0);
        Assert.True(s.categories.Length == 0 && !s.HasData, "no data");
        Assert.True(HabitSummaryBuilder.Build(null, 0).categories.Length == 0, "null input");
        Assert.True(!BankFeedMapping.SuggestProfile(s, out string f, out string t) && f == "food" && t == "taxi",
            "no data -> default profile");
    }

    // ------------------------------------------------------------------ sample personas and mapping

    public static void TestSampleDataIsDeterministicAndRealistic()
    {
        foreach (SamplePersona p in SampleTransactionSource.Personas)
        {
            List<ObTransaction> a = SampleTransactionSource.Generate(p.id);
            List<ObTransaction> b = SampleTransactionSource.Generate(p.id);
            Assert.True(a.Count == b.Count && a.Count >= 40, p.id + ": same, plenty of transactions (" + a.Count + ")");
            for (int i = 0; i < a.Count; i++)
            {
                Assert.Equal(a[i].BookingDateTime + a[i].Amount.Amount + a[i].TransactionInformation,
                    b[i].BookingDateTime + b[i].Amount.Amount + b[i].TransactionInformation, p.id + " #" + i);
                Assert.True(a[i].Amount.Currency == "ZAR" && a[i].Amount.Value > 0m && a[i].Amount.Value < 20000m,
                    p.id + " amount " + a[i].Amount.Amount);
                Assert.True(a[i].TryGetBookingTime(out DateTimeOffset when) && when >= SampleTransactionSource.StartDate
                    && when < SampleTransactionSource.EndDate.AddDays(1), p.id + " date in window");
            }

            Assert.True(a.Any(t => t.IsCredit), p.id + " has income");
            Assert.True(a.Select(t => t.TransactionId).Distinct().Count() == a.Count, p.id + " unique ids");
        }
    }

    public static void TestPersonasMapToProfiles()
    {
        Expect(SampleTransactionSource.StudentId, SpendingFocus.Food, TravelMode.Taxi);
        Expect(SampleTransactionSource.CommuterId, SpendingFocus.Transport, TravelMode.Taxi);
        Expect(SampleTransactionSource.CreatorId, SpendingFocus.DataSocial, TravelMode.EHailing);
        Expect(SampleTransactionSource.BreadwinnerId, SpendingFocus.HomeFamily, TravelMode.Walk);
        Expect(SampleTransactionSource.DriverId, SpendingFocus.Transport, TravelMode.Car);

        BankHabitSummary student = Persona(SampleTransactionSource.StudentId);
        Assert.True(student.eatsOutOften && !student.taxiCommuter, "student eats out, walks to class");
        Assert.True(student.paydayDayOfMonth == 25 && student.incomeKind == IncomeKind.Bursary, "bursary on the 25th");

        BankHabitSummary commuter = Persona(SampleTransactionSource.CommuterId);
        Assert.True(commuter.taxiCommuter && commuter.sendsMoneyHome && commuter.Get(SpendCategory.Savings) != null,
            "commuter: taxis, money home, stokvel");
        Assert.True(commuter.paydayDayOfMonth == 25 && commuter.incomeKind == IncomeKind.Salary, "salary on the 25th");

        BankHabitSummary creator = Persona(SampleTransactionSource.CreatorId);
        Assert.True(creator.dataHeavy && creator.paydayDayOfMonth == 0 && creator.incomeKind == IncomeKind.Irregular,
            "creator: data heavy, irregular income");

        BankHabitSummary breadwinner = Persona(SampleTransactionSource.BreadwinnerId);
        Assert.True(breadwinner.cashUser && breadwinner.sendsMoneyHome && breadwinner.hasLoanRepayments,
            "breadwinner: cash, money home, loan");
        Assert.True(breadwinner.paydayDayOfMonth == 15, "payday 15th, got " + breadwinner.paydayDayOfMonth);

        BankHabitSummary driver = Persona(SampleTransactionSource.DriverId);
        Assert.True(driver.hasLoanRepayments && driver.paydayDayOfMonth == 28, "driver: store account, payday 28th");
    }

    static void Expect(string persona, string focus, string travel)
    {
        BankHabitSummary s = Persona(persona);
        Assert.True(s.HasData && s.IsSample && s.samplePersonaId == persona, persona + " sample summary");
        Assert.Equal(focus, s.suggestedFocus, persona + " focus");
        Assert.Equal(travel, s.suggestedTravel, persona + " travel");
        Assert.True(BankFeedMapping.SuggestProfile(s, out string f, out string t) && f == focus && t == travel,
            persona + " SuggestProfile");
        // The ids are ones the schedule knows (so all existing logic keeps working).
        Assert.Equal(focus, ChapterSchedule.NormalizeFocus(f), persona + " schedule focus");
        Assert.Equal(travel, ChapterSchedule.NormalizeTravel(t), persona + " schedule travel");
    }

    public static void TestSpotWeights()
    {
        Dictionary<string, float> empty = BankFeedMapping.SpotWeights(new BankHabitSummary());
        Assert.True(empty.Count == 6, "six spots");
        foreach (float w in empty.Values)
        {
            Assert.Equal(1f / 6f, w, "equal without data", 0.0001f);
        }

        foreach (SamplePersona p in SampleTransactionSource.Personas)
        {
            Dictionary<string, float> weights = BankFeedMapping.SpotWeights(Persona(p.id));
            Assert.Equal(1f, weights.Values.Sum(), p.id + " weights sum to 1", 0.001f);
            Assert.True(ChapterSchedule.AllSpots.All(weights.ContainsKey), p.id + " every spot");
        }

        Assert.Equal(ChapterSchedule.SpotCorner, BankFeedMapping.SpotsByWeight(Persona(SampleTransactionSource.StudentId))[0],
            "student: the kota shop first");
        Assert.Equal(ChapterSchedule.SpotTaxi, BankFeedMapping.SpotsByWeight(Persona(SampleTransactionSource.CommuterId))[0],
            "commuter: the taxi rank first");
        Assert.Equal(ChapterSchedule.SpotGate, BankFeedMapping.SpotsByWeight(Persona(SampleTransactionSource.BreadwinnerId))[0],
            "breadwinner: home first");
    }

    // ------------------------------------------------------------------ session, privacy, registry

    public static void TestSessionReducesAndDisconnects()
    {
        var source = new FakeSource();
        BankFeedOutcome outcome = null;
        BankFeedSession.Run(source, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)), 30, 42L, "x",
            o => outcome = o);
        Assert.True(outcome != null && outcome.Ok, "ok");
        Assert.True(source.disconnected, "source disconnected after summarising");
        Assert.True(source.accountsAsked == 1 && source.transactionCalls == 2, "consent -> accounts -> each account");
        Assert.Equal(BankHabitSource.Live, outcome.Summary.source, "a non-sample source is live");
        Assert.Equal("", outcome.Summary.samplePersonaId, "no persona for live");
        Assert.True(outcome.Summary.createdUnixSeconds == 42L, "time");
        Assert.True(source.permissions.SequenceEqual(ObPermission.Requested), "asks exactly the listed permissions");

        var declined = new FakeSource { grant = false };
        BankFeedSession.Run(declined, DateTimeOffset.UtcNow, 30, 0L, "", o => outcome = o);
        Assert.True(!outcome.Ok && outcome.Error == "declined" && declined.disconnected && declined.accountsAsked == 0,
            "declined stops before accounts");
    }

    public static void TestSavedSummaryHoldsNoTransactionData()
    {
        BankHabitSummary s = Persona(SampleTransactionSource.CommuterId);
        var data = PlayerData.CreateNew();
        data.bankHabits = s;
        // Every string the summary can carry is an id from a fixed list, never transaction text.
        var allowed = new HashSet<string>(SpendCategory.All.Concat(new[]
        {
            "", BankHabitSource.Sample, BankHabitSource.Live, IncomeKind.Salary, IncomeKind.Allowance,
            IncomeKind.Bursary, IncomeKind.Grant, IncomeKind.Irregular
        }).Concat(SpendingFocus.All.Select(o => o.id)).Concat(TravelMode.All.Select(o => o.id))
          .Concat(SampleTransactionSource.Personas.Select(p => p.id)));
        foreach (FieldInfo f in typeof(BankHabitSummary).GetFields().Where(f => f.FieldType == typeof(string)))
        {
            Assert.True(allowed.Contains((string)f.GetValue(s)), "summary." + f.Name + " is a fixed id");
        }

        foreach (CategoryHabit c in s.categories)
        {
            Assert.True(SpendCategory.All.Contains(c.category), "category id " + c.category);
        }

        Assert.True(typeof(BankHabitSummary).GetFields().All(f => f.FieldType != typeof(ObTransaction[])
            && f.FieldType != typeof(List<ObTransaction>)), "no transaction list in the save");

        BankHabits.Forget(data);
        Assert.True(!BankHabits.Has(data) && data.bankHabits != null, "forget empties the summary");
        Assert.True(data.spendingProfile != null, "forget keeps the taps");
    }

    public static void TestSaveFormatIsBackwardCompatible()
    {
        // No save-version bump: old saves (without bankHabits) are not discarded, and JsonUtility fills the missing
        // field from its initialiser: an empty summary.
        Assert.True(PlayerData.CurrentSaveVersion == 2 && !PlayerData.ShouldReset(2), "save version unchanged");
        var old = new PlayerData();
        Assert.True(old.bankHabits != null && !old.bankHabits.HasData, "missing field reads as an empty summary");
        Assert.True(old.bankHabits.version == BankHabitSummary.CurrentVersion, "summary version");

        var data = PlayerData.CreateNew();
        data.bankHabits = Persona(SampleTransactionSource.StudentId);
        MaliGo.Economy.ChapterFlow.RestartChapter(data);
        Assert.True(BankHabits.Has(data), "\"Live the week again\" keeps the summary");
        data.bankHabits = null;
        MaliGo.Economy.ChapterFlow.StartChapter(data, 1);
        Assert.True(data.bankHabits != null && !BankHabits.Has(data), "never left null");
    }

    public static void TestRegistryHasNoLiveProviderByDefault()
    {
        TransactionSources.ClearLive();
        Assert.True(!TransactionSources.HasLive && TransactionSources.CreateLive() == null, "no live provider in the public build");
        TransactionSources.RegisterLive(() => new FakeSource());
        Assert.True(TransactionSources.HasLive && TransactionSources.CreateLive() is FakeSource, "adapter registers");
        TransactionSources.ClearLive();
        Assert.True(TransactionSources.CreateSample("nope").IsSample, "sample source");
        Assert.True(!MaliGo.Core.MaliGoFeatures.BankFeedOnboarding, "flag off by default for the beta");
    }

    // ------------------------------------------------------------------ copy

    public static void TestHabitLinesAndCopyAreCleanAndGeneric()
    {
        var texts = new List<string>
        {
            BankFeedCopy.IntroTitle, BankFeedCopy.IntroBody, BankFeedCopy.ConnectButton, BankFeedCopy.SkipButton,
            BankFeedCopy.ConsentTitle, BankFeedCopy.NoLiveLine, BankFeedCopy.SampleButton, BankFeedCopy.AgreeButton,
            BankFeedCopy.ErrorLine, BankFeedCopy.WaitingLine, BankFeedCopy.SummaryTitle, BankFeedCopy.SampleBadge, BankFeedCopy.WeekLine,
            BankFeedCopy.ChangeLine, BankFeedCopy.AnotherSampleButton, BankFeedCopy.NoHabitsLine,
            BankFeedCopy.ForgetButton, BankFeedCopy.ForgottenNotice
        };
        texts.AddRange(BankFeedCopy.ConsentPoints);
        foreach (SamplePersona p in SampleTransactionSource.Personas)
        {
            BankHabitSummary s = Persona(p.id);
            List<string> lines = BankFeedCopy.TopHabitLines(s, 3);
            Assert.True(lines.Count == 3, p.id + ": three habit lines, got " + lines.Count);
            Assert.True(lines.Distinct().Count() == 3, p.id + ": distinct lines");
            texts.AddRange(lines);
            texts.Add(BankFeedCopy.SamplePersonLine(p.id));
            texts.Add(BankFeedCopy.WeekPicks(s.suggestedFocus, s.suggestedTravel));
            texts.AddRange(SampleTransactionSource.Generate(p.id)
                .SelectMany(t => new[] { t.MerchantDetails.MerchantName, t.TransactionInformation }).Distinct());
        }

        Assert.True(BankFeedCopy.TopHabitLines(new BankHabitSummary(), 3).Count == 0, "no data, no lines");
        Assert.Equal("about once", BankFeedCopy.Times(0.4f), "times 1");
        Assert.Equal("about 5 times", BankFeedCopy.Times(4.6f), "times 5");
        Assert.Equal("21st", BankFeedCopy.Ordinal(21), "21st");
        Assert.Equal("12th", BankFeedCopy.Ordinal(12), "12th");
        Assert.Equal("3rd", BankFeedCopy.Ordinal(3), "3rd");

        // The shell copy test's brand list (kept in one place).
        var brands = (string[])typeof(ShellCopyTests).GetField("Brands", BindingFlags.NonPublic | BindingFlags.Static)
            .GetValue(null);
        var patterns = brands.Select(b => new Regex(@"(?<![A-Za-z])" + Regex.Escape(b).Replace(@"\ ", @"\s+") + @"(?![A-Za-z])",
            RegexOptions.IgnoreCase)).ToArray();
        foreach (string text in texts.Where(t => !string.IsNullOrEmpty(t)))
        {
            foreach (char c in text)
            {
                Assert.True(c >= 0x20 && c <= 0x7E || "·—’".IndexOf(c) >= 0, "glyph U+" + ((int)c).ToString("X4") + " in \"" + text + "\"");
            }

            foreach (Regex p in patterns)
            {
                Assert.True(!p.IsMatch(text), "\"" + text + "\" names a brand");
            }
        }
    }

    public static void TestButtonLabelsFit()
    {
        // CharacterCreationUI.BankFeed: primary 600 u, secondary 440 u; Pause's Forget row is 660 u. 40 u padding each side.
        foreach (string label in new[] { BankFeedCopy.ConnectButton, BankFeedCopy.SampleButton, BankFeedCopy.AgreeButton })
        {
            Assert.True(AileronMetrics.Width(label, "Bold", 44f) <= 600f - 80f, "primary label fits: " + label);
        }

        foreach (string label in new[] { BankFeedCopy.SkipButton, BankFeedCopy.AnotherSampleButton })
        {
            Assert.True(AileronMetrics.Width(label, "Bold", 44f) <= 440f - 80f, "secondary label fits: " + label);
        }

        Assert.True(AileronMetrics.Width(BankFeedCopy.ForgetButton, "Bold", 44f) <= 660f - 80f, "forget label fits");
    }

    sealed class FakeSource : ITransactionSource
    {
        public bool grant = true;
        public bool disconnected;
        public int accountsAsked;
        public int transactionCalls;
        public string[] permissions = Array.Empty<string>();

        public string SourceId => "fake";
        public bool IsSample => false;

        public void RequestConsent(ObConsentRequest request, Action<ConsentResult> done)
        {
            permissions = request.Permissions;
            done(new ConsentResult { Granted = grant });
        }

        public void GetAccounts(Action<AccountsResult> done)
        {
            accountsAsked++;
            var r = new AccountsResult { Ok = true };
            r.Accounts.Add(new ObAccount { AccountId = "a" });
            r.Accounts.Add(new ObAccount { AccountId = "b" });
            done(r);
        }

        public void GetTransactions(string accountId, DateTimeOffset from, DateTimeOffset to, Action<TransactionsResult> done)
        {
            transactionCalls++;
            ObJsonParser.TryParseTransactions(SampleJson, out List<ObTransaction> list);
            done(new TransactionsResult { Ok = true, Transactions = list });
        }

        public void Disconnect()
        {
            disconnected = true;
        }
    }
}
