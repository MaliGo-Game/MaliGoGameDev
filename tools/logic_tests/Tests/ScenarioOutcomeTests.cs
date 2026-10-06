// WP6 tests: ScenarioOutcome (design spec 7.6, 3.4, 4.8), MaliGreeting (4.2.1) and the copy-fit checks of the
// dialogue box and the choice sheet with Aileron metrics (5.4.2, 5.4.3, 5.4.5, section 8 WP6).
using System;
using System.Collections.Generic;
using System.Linq;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Dialogue;
using MaliGo.Economy;
using MaliGo.Scenarios;

public static class ScenarioOutcomeTests
{
    // ------------------------------------------------------------------ fixtures

    static readonly string[] Foci = { "food", "transport", "data_social", "home_family" };
    static readonly string[] Travels = { "taxi", "ehailing", "walk", "car" };

    /// <summary>The widest 16-character name (spec 3.1 fit rule).</summary>
    const string WideName = "Mmmmmmmmmmmmmmmm";

    /// <summary>Spec 3.4: cash, savings, energy, stress per choice (default profile; travel rows below).</summary>
    static readonly Dictionary<string, (float cash, float sav, float energy, float stress)> Deltas =
        new Dictionary<string, (float, float, float, float)>
        {
            { "food_decision/kota", (-50, 0, 0, -5) },
            { "food_decision/vetkoek", (-20, 0, 0, -2) },
            { "food_decision/skip_lunch", (0, 0, -45, 5) },
            { "transport_decision/usual", (-30, 0, -5, 0) },
            { "transport_decision/walk", (0, 0, -45, 0) },
            { "transport_decision/ride", (-90, 0, 0, -2) },
            { "data_runs_out/bundle_1gb", (-85, 0, 0, 0) },
            { "data_runs_out/day_bundle", (-15, 0, 0, 2) },
            { "data_runs_out/free_wifi", (0, 0, -45, 3) },
            { "credit_bnpl/pay_in_full", (-360, 0, 0, 0) },
            { "credit_bnpl/pay_later", (-120, 0, 0, 3) },
            { "credit_bnpl/leave_it", (0, 0, 0, 0) },
            { "impulse_purchase/buy_it", (-120, 0, 0, -2) },
            { "impulse_purchase/walk_away", (0, 0, 0, 0) },
            { "impulse_purchase/to_savings", (-120, 120, 0, 0) },
            { "taxi_fare_rise/pay_new_fare", (-34, 0, -5, 0) },
            { "taxi_fare_rise/lift_club", (-120, 0, 0, -3) },
            { "taxi_fare_rise/walk_today", (0, 0, -50, 2) },
            { "family_obligation/send_full", (-200, 0, 0, 0) },
            { "family_obligation/send_part", (-80, 0, 0, 3) },
            { "family_obligation/from_savings", (0, -200, 0, 0) },
            { "family_obligation/cant_this_week", (0, 0, 0, 8) },
            { "family_callback/send_rest", (-120, 0, 0, 0) },
            { "family_callback/from_savings", (0, -120, 0, 0) },
            { "family_callback/not_this_week", (0, 0, 0, 6) },
            { "family_callback_full/send_full", (-200, 0, 0, 0) },
            { "family_callback_full/from_savings", (0, -200, 0, 0) },
            { "family_callback_full/not_this_week", (0, 0, 0, 6) },
            { "group_chat_contribution/in_for_dinner", (-150, 0, 0, -4) },
            { "group_chat_contribution/gift_only", (-50, 0, 0, 2) },
            { "group_chat_contribution/not_this_time", (0, 0, 0, 4) },
            { "emergency_expense/from_savings", (0, -350, 0, 0) },
            { "emergency_expense/from_cash", (-350, 0, 0, 0) },
            { "emergency_expense/cold_showers", (0, 0, -10, 12) },
            { "mashonisa_offer/borrow_400", (400, 0, 0, 6) },
            { "mashonisa_offer/borrow_200", (200, 0, 0, 3) },
            { "mashonisa_offer/not_today", (0, 0, 0, 0) },
            { "stokvel_decision/join", (0, 0, 0, -2) },
            { "stokvel_decision/not_for_now", (0, 0, 0, 0) },
            { "windfall/all_to_savings", (0, 300, 0, 0) },
            { "windfall/keep_cash", (300, 0, 0, -5) },
            { "windfall/half_half", (150, 150, 0, -2) },
            { "debit_order_check/move_to_cash", (199, -199, 0, -3) },
            { "debit_order_check/cancel_gym", (0, 0, 0, 0) },
            { "debit_order_check/leave_it", (0, 0, 0, 6) },
            { "kota_run/full_kota", (-65, 0, 0, -4) },
            { "kota_run/share_kota", (-30, 0, -10, -1) },
            { "kota_run/cook_home", (0, 0, -20, 2) },
        };

    /// <summary>Spec 3.4.0: (trip, trip energy, daily, daily energy) per travel mode.</summary>
    static (float trip, float tripEnergy, float daily, float dailyEnergy) TravelAmounts(string travel)
    {
        switch (travel)
        {
            case "ehailing": return (-44, 0, -50, 0);
            case "car": return (-36, 0, -40, 0);
            default: return (-30, -5, -34, -5); // taxi, walk
        }
    }

    static (float cash, float sav, float energy, float stress) Expected(string key, string travel)
    {
        var d = Deltas[key];
        var t = TravelAmounts(travel);
        if (key == "transport_decision/usual")
        {
            return (t.trip, 0, t.tripEnergy, 0);
        }

        if (key == "taxi_fare_rise/pay_new_fare")
        {
            return (t.daily, 0, t.dailyEnergy, 0);
        }

        return d;
    }

    /// <summary>A fresh chapter state on Day <paramref name="day"/> with that day open (cash 600, savings 400).</summary>
    static PlayerData State(int day, string focus = "food", string travel = "taxi", string name = "Thandeka")
    {
        PlayerData d = PlayerData.CreateNew();
        d.characterName = name;
        d.hasMetMali = true;
        d.spendingProfile.focus = focus;
        d.spendingProfile.travel = travel;
        OpenDay(d, day);
        return d;
    }

    static void OpenDay(PlayerData d, int day)
    {
        d.currentDay = day;
        d.today = new DayRecord();
        MoneyRecorder.OpenDay(d);
    }

    static ScenarioDefinition Def(string id, string focus = "food", string travel = "taxi")
    {
        ScenarioDefinition s = ScenarioLibrary.Get(id, focus, travel);
        Assert.True(s != null, "no scenario " + id);
        return s;
    }

    static ScenarioChoice ChoiceOf(ScenarioDefinition s, string choiceId)
    {
        ScenarioChoice c = s.choices.FirstOrDefault(x => x.choiceId == choiceId);
        Assert.True(c != null, s.scenarioId + " has no choice " + choiceId);
        return c;
    }

    /// <summary>The day a scenario comes up in a focus's week (follow-ups: two days after the call).</summary>
    static int DayOf(string id, string focus)
    {
        if (ChapterSchedule.IsFollowUp(id))
        {
            return ChapterSchedule.ScheduledDay(ScenarioLibrary.FamilyObligationId, focus) + 2;
        }

        // kota_run is scheduled only in the food week (Day 5); its content is still built for every profile.
        int day = ChapterSchedule.ScheduledDay(id, focus);
        return day == 0 && id == ScenarioLibrary.KotaRunId ? 5 : day;
    }

    static Obligation Find(PlayerData d, string id) => d.obligations.FirstOrDefault(o => o.obligationId == id);

    static IEnumerable<(string focus, string travel)> Profiles()
    {
        foreach (string f in Foci)
        {
            foreach (string t in Travels)
            {
                yield return (f, t);
            }
        }
    }

    // ------------------------------------------------------------------ deltas

    public static void TestEveryChoiceGivesTheListedDeltasForEveryProfile()
    {
        foreach (var (focus, travel) in Profiles())
        {
            foreach (string id in ScenarioLibrary.AllIds)
            {
                ScenarioDefinition s = Def(id, focus, travel);
                int day = DayOf(id, focus);
                Assert.True(day >= 1 && day <= 7, id + " has no day in " + focus);

                string[] expectedIds = Deltas.Keys.Where(k => k.StartsWith(id + "/", StringComparison.Ordinal))
                    .Select(k => k.Substring(id.Length + 1)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                string[] actualIds = s.choices.Select(c => c.choiceId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
                Assert.Equal(string.Join(",", expectedIds), string.Join(",", actualIds), id + " choice ids");

                foreach (ScenarioChoice c in s.choices)
                {
                    string key = id + "/" + c.choiceId;
                    string where = focus + "/" + travel + " " + key + " on Day " + day;
                    var e = Expected(key, travel);

                    PlayerData d = State(day, focus, travel);
                    FinancialStats st = d.financialStats;
                    float cash = st.cash, sav = st.savings, energy = st.energy, stress = st.financialStress, xp = st.financialXP;
                    int events = d.today.events.Length;

                    Assert.True(ScenarioOutcome.Availability(d, c) == ChoiceAvailability.Available, where + " available");
                    Assert.True(ScenarioOutcome.Apply(d, s, c), where + " applies");

                    Assert.Equal(e.cash, st.cash - cash, where + " cash");
                    Assert.Equal(e.sav, st.savings - sav, where + " savings");
                    Assert.Equal(e.energy, st.energy - energy, where + " energy");
                    Assert.Equal(e.stress, st.financialStress - stress, where + " stress");
                    Assert.Equal(5f, st.financialXP - xp, where + " xp");
                    Assert.True(d.IsScenarioCompleted(id), where + " completed");

                    bool moves = Math.Abs(e.cash) > 0.005f || Math.Abs(e.sav) > 0.005f;
                    Assert.True(d.today.events.Length == events + (moves ? 1 : 0), where + " money events");
                    if (moves)
                    {
                        MoneyEvent ev = d.today.events[d.today.events.Length - 1];
                        Assert.Equal("scenario:" + id + "/" + c.choiceId, ev.sourceId, where + " source");
                        Assert.Equal(s.moneyCategory, ev.category, where + " category");
                        Assert.Equal(c.ledgerLabel, ev.label, where + " ledger label");
                    }

                    ChoiceRecord record = d.chapter.choices.LastOrDefault();
                    Assert.True(record != null && record.scenarioId == id && record.choiceId == c.choiceId && record.day == day
                                && record.tag == c.behaviourTag.ToString(), where + " choice record");
                    string broken = MoneyRecorder.CheckInvariant(d);
                    Assert.True(broken == null, where + " invariant: " + broken);
                }
            }
        }
    }

    public static void TestUnaffordableChoiceIsRefusedAndChangesNothing()
    {
        PlayerData d = State(1);
        d.financialStats.cash = 10f;
        OpenDay(d, 1);
        ScenarioDefinition food = Def("food_decision");
        ScenarioChoice kota = ChoiceOf(food, "kota");
        float energy = d.financialStats.energy, xp = d.financialStats.financialXP;

        Assert.True(ScenarioOutcome.Availability(d, kota) == ChoiceAvailability.NotEnoughCash, "kota needs cash");
        Assert.True(!ScenarioOutcome.Apply(d, food, kota), "kota refused");
        Assert.Equal(10f, d.financialStats.cash, "cash unchanged");
        Assert.Equal(energy, d.financialStats.energy, "energy unchanged");
        Assert.Equal(xp, d.financialStats.financialXP, "xp unchanged");
        Assert.True(!d.IsScenarioCompleted("food_decision"), "not completed");
        Assert.True(d.chapter.choices.Length == 0 && d.today.events.Length == 0, "nothing recorded");
        Assert.True(ScenarioOutcome.Availability(d, ChoiceOf(food, "skip_lunch")) == ChoiceAvailability.Available,
                    "skip lunch stays available");
        Assert.True(ScenarioOutcome.AnyAvailable(d, food), "the scenario is never fully disabled");

        PlayerData poor = State(5);
        poor.financialStats.savings = 100f;
        OpenDay(poor, 5);
        ScenarioDefinition geyser = Def("emergency_expense");
        ScenarioChoice fromSavings = ChoiceOf(geyser, "from_savings");
        Assert.True(ScenarioOutcome.Availability(poor, fromSavings) == ChoiceAvailability.NotEnoughSavings, "geyser needs savings");
        Assert.True(!ScenarioOutcome.Apply(poor, geyser, fromSavings), "from savings refused");
        Assert.Equal(100f, poor.financialStats.savings, "savings unchanged");
        Assert.True(poor.obligations.All(o => o.createdDay == 0), "no obligation added");
    }

    public static void TestCompletedScenarioIsNotAppliedTwice()
    {
        PlayerData d = State(1);
        ScenarioDefinition food = Def("food_decision");
        Assert.True(ScenarioOutcome.Apply(d, food, ChoiceOf(food, "kota")), "first time");
        float cash = d.financialStats.cash;
        Assert.True(!ScenarioOutcome.Apply(d, food, ChoiceOf(food, "vetkoek")), "second time refused");
        Assert.Equal(cash, d.financialStats.cash, "no second charge");
        Assert.True(d.chapter.choices.Length == 1, "one record");
    }

    public static void TestToSavingsIsATransfer()
    {
        PlayerData d = State(3);
        ScenarioDefinition s = Def("impulse_purchase");
        float total = d.financialStats.cash + d.financialStats.savings;
        Assert.True(ScenarioOutcome.Apply(d, s, ChoiceOf(s, "to_savings")), "applies");
        Assert.Equal(total, d.financialStats.cash + d.financialStats.savings, "total unchanged");
        Assert.True(d.today.events.Last().kind == MoneyEventKind.Transfer, "recorded as a transfer");
        Assert.True(ScenarioOutcome.IsTransfer(ChoiceOf(s, "to_savings")), "IsTransfer");
        Assert.True(!ScenarioOutcome.IsTransfer(ChoiceOf(s, "buy_it")), "buy_it is not a transfer");
    }

    // ------------------------------------------------------------------ instalments and follow-ups

    static void AssertObligation(PlayerData d, string id, float amount, int firstDue, int count, int interval, string kind,
                                 string category, int createdDay, string where)
    {
        Obligation o = Find(d, id);
        Assert.True(o != null, where + ": no obligation " + id);
        Assert.Equal(amount, o.amount, where + " amount");
        Assert.True(o.nextDueDay == firstDue, where + " first due " + o.nextDueDay + " != " + firstDue);
        Assert.True(o.paymentsRemaining == count, where + " payments " + o.paymentsRemaining + " != " + count);
        Assert.True(o.intervalDays == interval, where + " interval " + o.intervalDays);
        Assert.Equal(kind, o.kind, where + " kind");
        Assert.Equal(category, o.category, where + " category");
        Assert.True(o.createdDay == createdDay, where + " createdDay " + o.createdDay);
        Assert.True(!string.IsNullOrEmpty(o.shortLabel) && o.shortLabel.Length <= 10, where + " shortLabel");
        Assert.True(!string.IsNullOrEmpty(o.label), where + " label");
    }

    static PlayerData Choose(int day, string id, string choiceId, string focus = "food", string travel = "taxi")
    {
        PlayerData d = State(day, focus, travel);
        ScenarioDefinition s = Def(id, focus, travel);
        Assert.True(ScenarioOutcome.Apply(d, s, ChoiceOf(s, choiceId)), id + "/" + choiceId + " on Day " + day);
        return d;
    }

    public static void TestInstalmentObligationsHaveTheirDueDays()
    {
        AssertObligation(Choose(2, "credit_bnpl", "pay_later"), "credit_bnpl_pay_later", 130, 4, 2, 2, "instalment", "Pay-later", 2,
                         "pay later Day 2");
        AssertObligation(Choose(2, "data_runs_out", "day_bundle"), "data_runs_out_day_bundle", 15, 3, 2, 1, "repeat", "Phone & data", 2,
                         "day bundles Day 2");
        AssertObligation(Choose(6, "data_runs_out", "day_bundle"), "data_runs_out_day_bundle", 15, 7, 1, 1, "repeat", "Phone & data", 6,
                         "day bundles Day 6");
        AssertObligation(Choose(3, "taxi_fare_rise", "pay_new_fare"), "taxi_fare_rise_pay_new_fare", 34, 4, 4, 1, "repeat", "Transport", 3,
                         "fare Day 3");
        AssertObligation(Choose(5, "taxi_fare_rise", "pay_new_fare"), "taxi_fare_rise_pay_new_fare", 34, 6, 2, 1, "repeat", "Transport", 5,
                         "fare Day 5");
        AssertObligation(Choose(3, "taxi_fare_rise", "walk_today"), "taxi_fare_rise_walk_today", 34, 4, 4, 1, "repeat", "Transport", 3,
                         "walk today Day 3");
        AssertObligation(Choose(5, "mashonisa_offer", "borrow_400"), "mashonisa_offer_borrow_400", 600, 7, 1, 2, "loan", "Loan", 5,
                         "borrow R400 Day 5");
        AssertObligation(Choose(5, "mashonisa_offer", "borrow_200"), "mashonisa_offer_borrow_200", 300, 7, 1, 2, "loan", "Loan", 5,
                         "borrow R200 Day 5");
        AssertObligation(Choose(5, "emergency_expense", "cold_showers"), "emergency_expense_cold_showers", 350, 8, 1, 2, "commitment",
                         "Home", 5, "cold showers");
        AssertObligation(Choose(6, "stokvel_decision", "join"), "stokvel_decision_join", 200, 8, 1, 2, "commitment", "Bills", 6,
                         "stokvel");
        AssertObligation(Choose(7, "debit_order_check", "leave_it"), "debit_order_check_leave_it", 199, 8, 1, 2, "commitment", "Bills", 7,
                         "gym");

        foreach (string travel in Travels)
        {
            var t = TravelAmounts(travel);
            AssertObligation(Choose(3, "taxi_fare_rise", "pay_new_fare", "food", travel), "taxi_fare_rise_pay_new_fare", -t.daily, 4, 4, 1,
                             "repeat", "Transport", 3, "fare Day 3 " + travel);
        }

        // Cold showers: the geyser repair is promised for payday, never due inside the chapter.
        PlayerData cold = Choose(5, "emergency_expense", "cold_showers");
        Assert.True(ObligationLedger.Promised(cold).Any(p => p.obligationId == "emergency_expense_cold_showers" && p.day == 8),
                    "geyser is promised for payday");
    }

    public static void TestLaterDays()
    {
        PlayerData d2 = State(2), d3 = State(3), d5 = State(5), d6 = State(6), d7 = State(7);
        ScenarioChoice bundle = ChoiceOf(Def("data_runs_out"), "day_bundle");
        ScenarioChoice fare = ChoiceOf(Def("taxi_fare_rise"), "pay_new_fare");
        ScenarioChoice payLater = ChoiceOf(Def("credit_bnpl"), "pay_later");

        Assert.Equal("3,4", string.Join(",", ScenarioOutcome.LaterDays(d2, bundle)), "bundles Day 2");
        Assert.Equal("7", string.Join(",", ScenarioOutcome.LaterDays(d6, bundle)), "bundles Day 6");
        Assert.Equal("", string.Join(",", ScenarioOutcome.LaterDays(d7, bundle)), "bundles Day 7");
        Assert.True(ScenarioOutcome.LaterCount(d7, bundle) == 0, "bundles Day 7 count");
        Assert.Equal(bundle.maliReactionNoLater, ScenarioOutcome.ReactionFor(d7, bundle), "bundles Day 7 reaction");
        Assert.Equal(bundle.maliReactionLine, ScenarioOutcome.ReactionFor(d2, bundle), "bundles Day 2 reaction");
        Assert.True(!string.IsNullOrEmpty(bundle.maliReactionNoLater), "bundles have a no-later line");

        PlayerData bought = Choose(7, "data_runs_out", "day_bundle");
        Assert.True(Find(bought, "data_runs_out_day_bundle") == null, "no obligation for bundles on Day 7");

        Assert.Equal("4,5,6,7", string.Join(",", ScenarioOutcome.LaterDays(d3, fare)), "fare Day 3");
        Assert.Equal("Days 4 to 7", ScenarioOutcome.LaterDaysWords(ScenarioOutcome.LaterDays(d3, fare)), "fare Day 3 words");
        Assert.Equal("6,7", string.Join(",", ScenarioOutcome.LaterDays(d5, fare)), "fare Day 5");
        Assert.Equal("4,6", string.Join(",", ScenarioOutcome.LaterDays(d2, payLater)), "pay later Day 2");
        Assert.Equal(fare.maliReactionNoLater, ScenarioOutcome.ReactionFor(d7, fare), "fare Day 7 reaction");

        Assert.Equal("Days 4 to 7", ScenarioOutcome.LaterDaysWords(new[] { 4, 5, 6, 7 }), "words 4-7");
        Assert.Equal("Days 3 and 4", ScenarioOutcome.LaterDaysWords(new[] { 3, 4 }), "words 3,4");
        Assert.Equal("Days 4 and 6", ScenarioOutcome.LaterDaysWords(new[] { 4, 6 }), "words 4,6");
        Assert.Equal("Day 7", ScenarioOutcome.LaterDaysWords(new[] { 7 }), "words 7");
        Assert.Equal("", ScenarioOutcome.LaterDaysWords(new int[0]), "words none");

        Dictionary<string, string> extra = ScenarioOutcome.ReactionExtra(d3, fare);
        Assert.Equal("R34 today, Thandeka. The fare comes off again on Days 4 to 7.",
                     MaliText.Fill(ScenarioOutcome.ReactionFor(d3, fare), d3, extra), "fare reaction filled");
    }

    public static void TestPayLaterChosenDay2IsChargedOnNights4And6()
    {
        PlayerData d = State(2);
        d.financialStats.cash = 5000f;
        OpenDay(d, 2);
        ScenarioDefinition s = Def("credit_bnpl");
        Assert.True(ScenarioOutcome.Apply(d, s, ChoiceOf(s, "pay_later")), "pay later applies");

        var charged = new List<int>();
        while (!d.chapter.complete)
        {
            NightResult night = DayCycle.EndDay(d);
            Assert.True(night != null, "night " + d.currentDay);
            foreach (MoneyEvent e in night.record.events)
            {
                if (e.sourceId == "bill:credit_bnpl_pay_later")
                {
                    Assert.Equal(-130f, e.cashDelta, "instalment amount");
                    charged.Add(night.endedDay);
                }
            }
        }

        Assert.Equal("4,6", string.Join(",", charged), "charged nights");
    }

    public static void TestFollowUps()
    {
        PlayerData part = Choose(4, "family_obligation", "send_part");
        Assert.Equal("family_callback@6", string.Join(",", part.followUps), "send_part on Day 4");
        PlayerData cant = Choose(4, "family_obligation", "cant_this_week");
        Assert.Equal("family_callback_full@6", string.Join(",", cant.followUps), "cant_this_week on Day 4");

        PlayerData late = Choose(6, "family_obligation", "send_part");
        Assert.True(late.followUps.Length == 0, "send_part on Day 6 adds nothing");
        PlayerData lateCant = Choose(6, "family_obligation", "cant_this_week");
        Assert.True(lateCant.followUps.Length == 0, "cant_this_week on Day 6 adds nothing");

        // The follow-up is active on Day 6 and not before.
        Assert.True(!ChapterSchedule.ActiveScenarioIds(part, true, "food", part.followUps).Contains("family_callback"),
                    "not active on Day 4");
        OpenDay(part, 6);
        Assert.True(ChapterSchedule.ActiveScenarioIds(part, true, "food", part.followUps).Contains("family_callback"),
                    "active on Day 6");

        // Neither follow-up sets anything off.
        foreach (string id in new[] { "family_callback", "family_callback_full" })
        {
            foreach (ScenarioChoice c in Def(id).choices)
            {
                PlayerData d = Choose(6, id, c.choiceId);
                Assert.True(d.followUps.Length == 0, id + "/" + c.choiceId + " sets nothing off");
                Assert.True(d.obligations.All(o => o.createdDay == 0), id + "/" + c.choiceId + " adds no obligation");
            }
        }

        // The Later column for a follow-up.
        ScenarioChoice sendPart = ChoiceOf(Def("family_obligation"), "send_part");
        Assert.Equal("Call back", ScenarioOutcome.LaterColumn(State(4), sendPart, out string small), "follow-up later");
        Assert.Equal("on Day 6", small, "follow-up later small");
        Assert.Equal("—", ScenarioOutcome.LaterColumn(State(6), sendPart, out small), "follow-up on Day 6");
        Assert.Equal("", small, "follow-up on Day 6 small");
    }

    public static void TestLaterColumnAndChips()
    {
        string small;
        ScenarioChoice fare = ChoiceOf(Def("taxi_fare_rise"), "pay_new_fare");
        Assert.Equal("R34 × 4", ScenarioOutcome.LaterColumn(State(3), fare, out small), "fare later");
        Assert.Equal("Days 4–7", small, "fare later small");
        Assert.Equal("R34 × 2", ScenarioOutcome.LaterColumn(State(5), fare, out small), "fare Day 5 later");
        Assert.Equal("Days 6, 7", small, "fare Day 5 small");
        Assert.Equal("—", ScenarioOutcome.LaterColumn(State(7), fare, out small), "fare Day 7 later");

        ScenarioChoice bundle = ChoiceOf(Def("data_runs_out"), "day_bundle");
        Assert.Equal("R15 × 2", ScenarioOutcome.LaterColumn(State(2), bundle, out small), "bundles later");
        Assert.Equal("Days 3, 4", small, "bundles small");

        ScenarioChoice payLater = ChoiceOf(Def("credit_bnpl"), "pay_later");
        Assert.Equal("R130 × 2", ScenarioOutcome.LaterColumn(State(2), payLater, out small), "pay later");
        Assert.Equal("Days 4, 6", small, "pay later small");
        Assert.Equal("R130 × 2", ScenarioOutcome.LaterColumn(State(6), payLater, out small), "pay later Day 6");
        Assert.Equal("from Day 8", small, "pay later Day 6 small");
        Assert.Equal("R130 × 2", ScenarioOutcome.LaterColumn(State(5), payLater, out small), "pay later Day 5");
        Assert.Equal("from Day 7", small, "pay later Day 5 small");

        ScenarioChoice cold = ChoiceOf(Def("emergency_expense"), "cold_showers");
        Assert.Equal("R350", ScenarioOutcome.LaterColumn(State(5), cold, out small), "cold showers later");
        Assert.Equal("on payday", small, "cold showers small");

        ScenarioChoice loan = ChoiceOf(Def("mashonisa_offer"), "borrow_400");
        Assert.Equal("R600", ScenarioOutcome.LaterColumn(State(5), loan, out small), "loan later");
        Assert.Equal("on Day 7", small, "loan small");

        ScenarioChoice kota = ChoiceOf(Def("food_decision"), "kota");
        Assert.Equal("—", ScenarioOutcome.LaterColumn(State(1), kota, out small), "kota later");
        Assert.Equal("−R50", ScenarioOutcome.MoneyColumn(kota.cashDelta), "kota cash");
        Assert.Equal("—", ScenarioOutcome.MoneyColumn(kota.savingsDelta), "kota savings");
        Assert.Equal("—", ScenarioOutcome.EnergyColumn(kota.energyDelta), "kota energy");
        Assert.Equal("−45", ScenarioOutcome.EnergyColumn(-45f), "energy column");
        Assert.Equal("+R400", ScenarioOutcome.MoneyColumn(400f), "money in column");

        Assert.Equal("Cash −R50", string.Join("|", ScenarioOutcome.Chips(State(1), kota)), "kota chips");
        Assert.Equal("Energy −45", string.Join("|", ScenarioOutcome.Chips(State(1), ChoiceOf(Def("food_decision"), "skip_lunch"))),
                     "skip lunch chips");
        Assert.Equal("Cash −R34|Energy −5|Later R34 × 4", string.Join("|", ScenarioOutcome.Chips(State(3), fare)), "fare chips");
        Assert.Equal("Energy −10|Later R350", string.Join("|", ScenarioOutcome.Chips(State(5), cold)), "cold showers chips");
        ScenarioChoice sendPart = ChoiceOf(Def("family_obligation"), "send_part");
        Assert.Equal("Cash −R80|Call back Day 6", string.Join("|", ScenarioOutcome.Chips(State(4), sendPart)), "send part chips");
        Assert.Equal("Cash −R80", string.Join("|", ScenarioOutcome.Chips(State(6), sendPart)), "send part Day 6 chips");
        ScenarioChoice toSavings = ChoiceOf(Def("impulse_purchase"), "to_savings");
        Assert.Equal("Cash −R120|Savings +R120", string.Join("|", ScenarioOutcome.Chips(State(3), toSavings)), "to savings chips");

        Assert.True(ScenarioOutcome.HasSavingsColumn(Def("impulse_purchase")), "hoodie has a savings column");
        Assert.True(!ScenarioOutcome.HasSavingsColumn(Def("food_decision")), "lunch has none");
    }

    // ------------------------------------------------------------------ greeting (4.2.1)

    static string Greeting(PlayerData d, bool useSchedule, out Dictionary<string, string> extra)
    {
        return MaliGreeting.Build(d, useSchedule, out extra);
    }

    public static void TestGreetingNotYetBeforeTheGateAndNotAfter()
    {
        PlayerData d = State(1);
        string line = Greeting(d, true, out Dictionary<string, string> extra);
        Assert.Equal("Two things are waiting today, at the kota shop and at the taxi rank. The shift opens after {gate}.", line,
                     "Day 1 before lunch");
        Assert.Equal("lunch", extra["gate"], "gate noun");
        Assert.Equal("Two things are waiting today, at the kota shop and at the taxi rank. The shift opens after lunch.",
                     MaliText.Fill(line, d, extra), "filled");

        ScenarioDefinition food = Def("food_decision");
        Assert.True(ScenarioOutcome.Apply(d, food, ChoiceOf(food, "vetkoek")), "lunch");
        line = Greeting(d, true, out extra);
        Assert.Equal("One thing is waiting today, at the taxi rank. There's a shift going at the far end of the main road.", line,
                     "Day 1 after lunch");
        Assert.True(!extra.ContainsKey("gate"), "no gate after lunch");

        PlayerData tired = State(1);
        Assert.True(ScenarioOutcome.Apply(tired, food, ChoiceOf(food, "skip_lunch")), "skip lunch");
        Assert.Equal("One thing is waiting today, at the taxi rank. You're too tired for a shift today.",
                     Greeting(tired, true, out _), "tired");

        // Worked: no suffix. Then nothing waiting.
        WorkRules.DoShift(d, ChapterSchedule.GateScenario(1, "food"));
        Assert.Equal("One thing is waiting today, at the taxi rank.", Greeting(d, true, out _), "worked");
        ScenarioDefinition trip = Def("transport_decision");
        Assert.True(ScenarioOutcome.Apply(d, trip, ChoiceOf(trip, "usual")), "trip");
        Assert.Equal("That's everything for today, {name}. Sleep at home when you're ready.", Greeting(d, true, out _), "nothing waiting");

        // Every focus and travel mode: NotYet with that day's gate noun before the gate, never after.
        foreach (var (focus, travel) in Profiles())
        {
            for (int day = 1; day <= 7; day++)
            {
                PlayerData p = State(day, focus, travel);
                string gateId = ChapterSchedule.GateScenario(day, focus);
                ScenarioDefinition gate = Def(gateId, focus, travel);
                string before = Greeting(p, true, out extra);
                Assert.True(before.EndsWith(MaliGreeting.NotYetSuffix, StringComparison.Ordinal),
                            focus + "/" + travel + " Day " + day + " before: " + before);
                Assert.Equal(gate.gateNoun, extra["gate"], focus + " Day " + day + " gate noun");
                ScenarioChoice free = gate.choices.First(c => c.cashDelta >= 0 && c.savingsDelta >= 0);
                Assert.True(ScenarioOutcome.Apply(p, gate, free), "gate resolves");
                string after = Greeting(p, true, out extra);
                Assert.True(!after.Contains("The shift opens after"), focus + " Day " + day + " after: " + after);
            }
        }
    }

    public static void TestGreetingVariants()
    {
        PlayerData fresh = State(1);
        fresh.hasMetMali = false;
        Assert.Equal(MaliDialogueLibrary.FindById(MaliDialogueLibrary.FirstMeetingId).line, Greeting(fresh, true, out _), "first meeting");

        PlayerData stretched = State(1);
        stretched.financialStats.financialStress = 60f;
        Assert.True(Greeting(stretched, true, out _).StartsWith("You seem stretched, {name}. Two things", StringComparison.Ordinal),
                    "stretched prefix");

        PlayerData owing = State(3);
        Obligation rent = Find(owing, ObligationDefaults.RentId);
        rent.arrears = 80f;
        string line = Greeting(owing, true, out Dictionary<string, string> extra);
        Assert.True(line.EndsWith(" R{owed} is still owed for {label}.", StringComparison.Ordinal), "owed suffix: " + line);
        Assert.Equal("80", extra["owed"], "owed raw");
        Assert.Equal("Rent", extra["label"], "owed label");

        PlayerData all = State(1);
        line = Greeting(all, false, out extra);
        Assert.True(line.StartsWith("6 things are waiting today, at the kota shop, at the taxi rank, ", StringComparison.Ordinal),
                    "schedule off: " + line);
        Assert.True(line.EndsWith(" There's a shift going at the far end of the main road.", StringComparison.Ordinal),
                    "schedule off: no gate");
        Assert.True(!line.Contains("{places}") && !line.Contains("{count}"), "places and count substituted");

        Assert.Equal("A", MaliGreeting.JoinPlaces(new[] { "A" }), "join 1");
        Assert.Equal("A and B", MaliGreeting.JoinPlaces(new[] { "A", "B" }), "join 2");
        Assert.Equal("A, B and C", MaliGreeting.JoinPlaces(new[] { "A", "B", "C" }), "join 3");

        // Profile place names.
        PlayerData car = State(1, "transport", "car");
        Assert.True(Greeting(car, true, out _).StartsWith("Two things are waiting today, at the petrol station and at the corner shop.",
                                                          StringComparison.Ordinal), "transport/car places");

        // Home and down the road read naturally (Revision 4): home_family Day 1 is the spaza shop and the call from home.
        PlayerData home = State(1, "home_family", "taxi");
        Assert.True(Greeting(home, true, out _).StartsWith("Two things are waiting today, at the spaza shop and at home.",
                                                           StringComparison.Ordinal), "home_family places: " + Greeting(home, true, out _));

        // The selector delegates to the greeting.
        Assert.Equal(Greeting(all, true, out _), MaliContextualDialogueSelector.SelectLine(all).line, "selector delegates");
        Assert.True(MaliContextualDialogueSelector.SelectLine(fresh).triggerType == MaliDialogueTriggerType.FirstMeeting,
                    "selector first meeting");
        Assert.True(MaliGreeting.Build(null, true, out _) != null, "null data");
    }

    // ------------------------------------------------------------------ fit with Aileron metrics

    /// <summary>Greedy line breaking like UiTextLayout.Wrap: Rand amounts ("R1 250") never split.</summary>
    static List<string> Wrap(string text, string weight, float size, float width)
    {
        var lines = new List<string>();
        float space = AileronMetrics.Width(" ", weight, size);
        foreach (string paragraph in text.Split('\n'))
        {
            List<string> units = Units(paragraph);
            if (units.Count == 0)
            {
                lines.Add("");
                continue;
            }

            string line = units[0];
            float w = AileronMetrics.Width(units[0], weight, size);
            for (int i = 1; i < units.Count; i++)
            {
                float uw = AileronMetrics.Width(units[i], weight, size);
                if (w + space + uw <= width + 0.01f)
                {
                    line += " " + units[i];
                    w += space + uw;
                }
                else
                {
                    lines.Add(line);
                    line = units[i];
                    w = uw;
                }
            }

            lines.Add(line);
        }

        return lines;
    }

    static List<string> Units(string paragraph)
    {
        var units = new List<string>();
        string[] words = paragraph.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (word.Length == 0)
            {
                continue;
            }

            if (EndsWithRandHead(word))
            {
                while (i + 1 < words.Length && IsDigitGroup(words[i + 1]))
                {
                    word += " " + words[i + 1];
                    i++;
                    if (words[i].Length != 3)
                    {
                        break;
                    }
                }
            }

            units.Add(word);
        }

        return units;
    }

    static bool EndsWithRandHead(string word)
    {
        int digits = 0;
        while (digits < word.Length && digits < 4 && char.IsDigit(word[word.Length - 1 - digits]))
        {
            digits++;
        }

        if (digits < 1 || digits > 3)
        {
            return false;
        }

        int r = word.Length - 1 - digits;
        return r >= 0 && word[r] == 'R' && (r == 0 || !char.IsLetterOrDigit(word[r - 1]));
    }

    static bool IsDigitGroup(string word)
    {
        if (word.Length < 3 || !char.IsDigit(word[0]) || !char.IsDigit(word[1]) || !char.IsDigit(word[2]))
        {
            return false;
        }

        for (int i = 3; i < word.Length; i++)
        {
            if (char.IsLetterOrDigit(word[i]))
            {
                return false;
            }
        }

        return true;
    }

    static void AssertFits(string text, string weight, float size, float width, int maxLines, string where)
    {
        List<string> lines = Wrap(text, weight, size, width);
        float widest = lines.Max(l => AileronMetrics.Width(l, weight, size));
        Assert.True(lines.Count <= maxLines && widest <= width + 0.01f,
                    where + ": " + lines.Count + " line(s) (max " + maxLines + "), widest " + widest.ToString("0") + " u of " + width
                    + " u: \"" + text + "\"");
    }

    public static void TestEveryLabelSituationAndPromptFits()
    {
        foreach (var (focus, travel) in Profiles())
        {
            PlayerData named = State(1, focus, travel, WideName);
            foreach (string id in ScenarioLibrary.AllIds)
            {
                ScenarioDefinition s = Def(id, focus, travel);
                string where = focus + "/" + travel + " " + id;
                float labelBox = ScenarioOutcome.HasSavingsColumn(s) ? 373f : 493f;
                foreach (ScenarioChoice c in s.choices)
                {
                    AssertFits(MaliText.Fill(c.label, named), "Bold", 44, labelBox, 2, where + "/" + c.choiceId + " label");
                }

                AssertFits(MaliText.Fill(s.description, named), "SemiBold", 40, 613, 6, where + " situation");
                AssertFits(MaliText.Fill(s.promptText, named), "Bold", 40, 776, 1, where + " prompt");
            }
        }
    }

    public static void TestEveryLaterAndValueColumnFits()
    {
        foreach (var (focus, travel) in Profiles())
        {
            foreach (string id in ScenarioLibrary.AllIds)
            {
                ScenarioDefinition s = Def(id, focus, travel);
                bool four = ScenarioOutcome.HasSavingsColumn(s);
                float laterWidth = four ? 190f : 200f;
                float cashWidth = four ? 150f : 160f;
                float energyWidth = four ? 120f : 130f;
                for (int day = DayOf(id, focus); day <= 7; day++)
                {
                    PlayerData d = State(day, focus, travel);
                    foreach (ScenarioChoice c in s.choices)
                    {
                        string where = focus + "/" + travel + " " + id + "/" + c.choiceId + " on Day " + day;
                        string later = ScenarioOutcome.LaterColumn(d, c, out string small);
                        AssertFits(later, "Bold", 36, laterWidth, 1, where + " Later");
                        if (small.Length > 0)
                        {
                            AssertFits(small, "SemiBold", 32, laterWidth, 1, where + " Later small line");
                        }

                        AssertFits(ScenarioOutcome.MoneyColumn(c.cashDelta), "Black", 44, cashWidth, 1, where + " Cash");
                        AssertFits(ScenarioOutcome.MoneyColumn(c.savingsDelta), "Black", 44, 150f, 1, where + " Savings");
                        AssertFits(ScenarioOutcome.EnergyColumn(c.energyDelta), "Black", 44, energyWidth, 1, where + " Energy");
                    }
                }
            }
        }
    }

    public static void TestEveryReactionFitsThreeLines()
    {
        var laterDays = new Dictionary<string, string> { { "laterDays", "Days 4 to 7" } };
        foreach (var (focus, travel) in Profiles())
        {
            foreach (string id in ScenarioLibrary.AllIds)
            {
                ScenarioDefinition s = Def(id, focus, travel);
                int day = DayOf(id, focus);
                foreach (ScenarioChoice c in s.choices)
                {
                    // Filled as the game fills it: after the change, with the widest 16-character name.
                    PlayerData d = State(day, focus, travel, WideName);
                    Assert.True(ScenarioOutcome.Apply(d, s, c), "applies");
                    string where = focus + "/" + travel + " " + id + "/" + c.choiceId;
                    AssertFits(MaliText.Fill(c.maliReactionLine, d, laterDays), "SemiBold", 46, 1180, 3, where + " reaction");
                    if (!string.IsNullOrEmpty(c.maliReactionNoLater))
                    {
                        AssertFits(MaliText.Fill(c.maliReactionNoLater, d, laterDays), "SemiBold", 46, 1180, 3, where + " no-later reaction");
                    }

                }
            }
        }
    }
}
