// WP7 tests: HudCopy, RevealLineBuilder and ChapterReflection (design spec 4.3, 4.4, 4.8, 5.4.1, 5.4.8, 5.4.9,
// 7.3, 7.4, section 8 WP7), including the copy-fit checks with Aileron metrics.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MaliGo.Copy;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.Scenarios;

public static class CopyTests
{
    // ------------------------------------------------------------------ fixtures

    static readonly string[] Foci = { "food", "transport", "data_social", "home_family" };
    static readonly string[] Travels = { "taxi", "ehailing", "walk", "car" };
    const string WideName = "Mmmmmmmmmmmmmmmm";

    static readonly string[] BannedWords =
    {
        "good", "nice", "great", "smart", "careful", "should", "mistake", "well done", "bad", "wrong", "proud", "wise",
        "smart move", "responsible", "too much", "overspent", "treat yourself", "unfortunately", "risk",
    };

    /// <summary>The middle play style of tools/sim_chapter.py (spec 3.2).</summary>
    static readonly Dictionary<string, string[]> Middle = new Dictionary<string, string[]>
    {
        { "food_decision", new[] { "vetkoek" } }, { "transport_decision", new[] { "usual" } },
        { "data_runs_out", new[] { "day_bundle" } }, { "credit_bnpl", new[] { "pay_later" } },
        { "impulse_purchase", new[] { "walk_away" } }, { "taxi_fare_rise", new[] { "pay_new_fare" } },
        { "family_obligation", new[] { "send_part" } }, { "family_callback", new[] { "send_rest", "from_savings" } },
        { "group_chat_contribution", new[] { "gift_only" } }, { "emergency_expense", new[] { "from_savings", "from_cash" } },
        { "mashonisa_offer", new[] { "not_today" } }, { "stokvel_decision", new[] { "join" } },
        { "windfall", new[] { "half_half" } }, { "debit_order_check", new[] { "leave_it" } },
        { "kota_run", new[] { "share_kota" } },
    };

    static PlayerData Fresh(string focus = "food", string travel = "taxi", string name = "Thandeka")
    {
        PlayerData d = PlayerData.CreateNew();
        d.characterName = name;
        d.hasMetMali = true;
        d.spendingProfile.focus = focus;
        d.spendingProfile.travel = travel;
        return d;
    }

    static void PlayChoice(PlayerData d, string scenarioId, string choiceId)
    {
        ScenarioDefinition s = ScenarioLibrary.Get(scenarioId, d.spendingProfile.focus, d.spendingProfile.travel);
        ScenarioChoice c = s.choices.First(x => x.choiceId == choiceId);
        Assert.True(ScenarioOutcome.Apply(d, s, c), "apply " + scenarioId + "/" + choiceId);
    }

    static void PlayPreferred(PlayerData d, string scenarioId, Dictionary<string, string[]> prefs)
    {
        ScenarioDefinition s = ScenarioLibrary.Get(scenarioId, d.spendingProfile.focus, d.spendingProfile.travel);
        ScenarioChoice pick = null;
        if (prefs.TryGetValue(scenarioId, out string[] wanted))
        {
            foreach (string id in wanted)
            {
                ScenarioChoice c = s.choices.FirstOrDefault(x => x.choiceId == id);
                if (c != null && ScenarioOutcome.Availability(d, c) == ChoiceAvailability.Available)
                {
                    pick = c;
                    break;
                }
            }
        }

        pick ??= s.choices.First(x => ScenarioOutcome.Availability(d, x) == ChoiceAvailability.Available);
        Assert.True(ScenarioOutcome.Apply(d, s, pick), "apply " + scenarioId);
    }

    /// <summary>Plays one day the way sim_chapter.py does (gate, shift, the rest) and sleeps.</summary>
    static NightResult PlayDay(PlayerData d, Dictionary<string, string[]> prefs, bool works = true)
    {
        string focus = d.spendingProfile.focus;
        string gate = ChapterSchedule.GateScenario(d.currentDay, focus);
        List<string> active = ChapterSchedule.ActiveScenarioIds(d, true, focus, d.followUps);
        if (gate != null && active.Contains(gate))
        {
            PlayPreferred(d, gate, prefs);
        }

        if (works && WorkRules.State(d, gate) == ShiftState.Open)
        {
            Assert.True(WorkRules.DoShift(d, gate) != null, "shift");
        }

        foreach (string id in ChapterSchedule.ActiveScenarioIds(d, true, focus, d.followUps))
        {
            PlayPreferred(d, id, prefs);
        }

        GameEvents.ClearPendingMoneyChanged();
        NightResult night = DayCycle.EndDay(d);
        Assert.True(night != null, "EndDay");
        Assert.True(MoneyRecorder.CheckInvariant(d) == null, "invariant: " + MoneyRecorder.CheckInvariant(d));
        return night;
    }

    static List<NightResult> PlayWeek(PlayerData d, Dictionary<string, string[]> prefs, bool works = true)
    {
        var nights = new List<NightResult>();
        for (int i = 0; i < 7; i++)
        {
            nights.Add(PlayDay(d, prefs, works));
        }

        Assert.True(d.chapter.complete, "chapter complete after 7 nights");
        return nights;
    }

    static MoneyEvent Ev(string label, string category, float cash, float savings, string source)
    {
        float total = cash + savings;
        return new MoneyEvent
        {
            label = label, category = category, cashDelta = cash, savingsDelta = savings, sourceId = source,
            kind = total > 0.005f ? MoneyEventKind.In : total < -0.005f ? MoneyEventKind.Out : MoneyEventKind.Transfer
        };
    }

    /// <summary>A state whose Day <paramref name="day"/> is open, carrying <paramref name="events"/>, then closed by EndDay.</summary>
    static NightResult NightWith(PlayerData d, params MoneyEvent[] events)
    {
        foreach (MoneyEvent e in events)
        {
            Assert.True(MoneyRecorder.Apply(d, e.cashDelta, e.savingsDelta, e.label, e.category, e.sourceId) != null, "apply " + e.label);
        }

        GameEvents.ClearPendingMoneyChanged();
        NightResult night = DayCycle.EndDay(d);
        Assert.True(night != null, "EndDay");
        return night;
    }

    static void Contains(string text, string part, string where)
    {
        Assert.True(text != null && text.Contains(part), where + ": \"" + text + "\" lacks \"" + part + "\"");
    }

    // ------------------------------------------------------------------ HUD copy

    public static void TestHudDayAndTodayPill()
    {
        PlayerData d = Fresh();
        var day = HudCopy.DayPill(d);
        Assert.Equal("Day 1 of 7", day.line1, "day line 1");
        Assert.Equal("Payday in 7 days", day.line2, "day line 2");
        d.currentDay = 7;
        Assert.Equal("Payday tomorrow", HudCopy.DayPill(d).line2, "day 7");

        d = Fresh();
        var today = HudCopy.TodayPill(d);
        Assert.Equal("Today from R1 000", today.line1, "today line 1");
        Assert.Equal("R1 000", today.nowText, "today now");
        MoneyRecorder.Apply(d, 100f, 0f, "Test", MoneyCategory.ExtraMoney, "test");
        GameEvents.ClearPendingMoneyChanged();
        today = HudCopy.TodayPill(d);
        Assert.Equal("Today from R1 000", today.line1, "today line 1 after +R100");
        Assert.Equal("R1 100", today.nowText, "today now after +R100");
    }

    public static void TestHudBillPill()
    {
        PlayerData d = Fresh();
        var bill = HudCopy.BillPill(d);
        Assert.Equal("Airtime", bill.line1, "day 1 bill");
        Assert.Equal("R60 · tomorrow", bill.line2, "day 1 bill amount");
        Assert.True(!bill.attention, "no attention");

        DayCycle.EndDay(d); // night 1 -> Day 2
        Assert.Equal("R60 · tonight", HudCopy.BillPill(d).line2, "airtime tonight");

        d.obligations.First(o => o.obligationId == "rent").arrears = 335f;
        bill = HudCopy.BillPill(d);
        Assert.Equal("Rent owed", bill.line1, "arrears line 1");
        Assert.Equal("R335", bill.line2, "arrears line 2");
        Assert.True(bill.attention, "attention");

        d = Fresh();
        d.obligations = Array.Empty<Obligation>();
        bill = HudCopy.BillPill(d);
        Assert.Equal("No bills", bill.line1, "empty 1");
        Assert.Equal("till payday", bill.line2, "empty 2");

        Assert.Equal("Day 5", HudCopy.When(5, 2), "when day n");
        Assert.Equal("payday", HudCopy.When(8, 2), "when payday");
    }

    // ------------------------------------------------------------------ reveal line A rules

    public static void TestRevealA1NoEvents()
    {
        PlayerData d = Fresh();
        NightResult n = NightWith(d);
        Contains(RevealLineBuilder.Build(d, n), "Nothing moved your money today. You ended where you started: R1 000.", "A1");
    }

    public static void TestRevealA2OnlyTransfers()
    {
        PlayerData d = Fresh();
        NightResult n = NightWith(d, Ev("Moved to savings", MoneyCategory.SavingsMove, -100f, 100f, "bank:to_savings"));
        Contains(RevealLineBuilder.Build(d, n), "Money moved between cash and savings today, and your total stayed at R1 000.", "A2");
    }

    public static void TestRevealA3Work()
    {
        PlayerData d = Fresh();
        NightResult n = NightWith(d, Ev("Vetkoek and mince", MoneyCategory.Food, -20f, 0f, "scenario:food_decision/vetkoek"),
                                  Ev("Shift at work", MoneyCategory.Work, 150f, 0f, "work:1"));
        Assert.Equal("The shift brought in the most today: R150. Tomorrow night, Airtime takes R60. You've got R730 in cash.",
                     RevealLineBuilder.Build(d, n), "A3 + B3 (spec 1.1)");
    }

    public static void TestRevealA4Loan()
    {
        PlayerData d = Fresh();
        for (int i = 0; i < 4; i++)
        {
            d.obligations = Array.Empty<Obligation>();
            DayCycle.EndDay(d);
        }

        Assert.True(d.currentDay == 5, "day 5");
        PlayChoice(d, "mashonisa_offer", "borrow_400");
        GameEvents.ClearPendingMoneyChanged();
        NightResult n = DayCycle.EndDay(d);
        Contains(RevealLineBuilder.Build(d, n), "The R400 from Bra K was the biggest change today. R600 goes back on Day 7.", "A4");
        Assert.Equal("+R400 today · R400 borrowed", RevealLineBuilder.DeltaPill(n.record), "a loan day says so in the pill");
        List<RevealRow> rows = RevealLineBuilder.LedgerRows(d, n.record);
        Assert.True(rows.Count == 1 && rows[0].isLoan && rows[0].label.EndsWith(" · R600 back Day 7"), "loan row: " + rows[0].label);
        Assert.Equal("+R400", rows[0].amountText, "loan amount");
    }

    public static void TestRevealA5AndA6()
    {
        PlayerData d = Fresh();
        NightResult n = NightWith(d, Ev("From the neighbour", MoneyCategory.ExtraMoney, 300f, 0f, "scenario:windfall/keep_cash"));
        Contains(RevealLineBuilder.Build(d, n), "From the neighbour: R300 in. That was the biggest change today.", "A5");

        d = Fresh();
        n = NightWith(d, Ev("Speaker deposit", MoneyCategory.PayLater, -120f, 0f, "scenario:credit_bnpl/pay_later"));
        Contains(RevealLineBuilder.Build(d, n), "Speaker deposit took the most today: R120.", "A6");
    }

    public static void TestRevealTieGoesToLaterEvent()
    {
        PlayerData d = Fresh();
        NightResult n = NightWith(d, Ev("First thing", MoneyCategory.Food, -50f, 0f, "scenario:a/b"),
                                  Ev("Second thing", MoneyCategory.Shopping, -50f, 0f, "scenario:c/d"));
        Assert.Equal("Second thing", RevealLineBuilder.BiggestMover(n.record).label, "tie -> later");
        Contains(RevealLineBuilder.Build(d, n), "Second thing took the most today", "tie in line");
    }

    public static void TestRevealPartLabelCleaned()
    {
        Assert.Equal("Rent", RevealLineBuilder.CleanLabel("Rent (part)"), "part removed");
    }

    // ------------------------------------------------------------------ reveal line B rules

    public static void TestRevealB1Day7()
    {
        PlayerData d = Fresh();
        List<NightResult> nights = PlayWeek(d, Middle);
        NightResult last = nights[6];
        Assert.True(RevealLineBuilder.Build(d, last).EndsWith("Tomorrow is payday."), "B1");
        Assert.Equal("On to payday", RevealLineBuilder.ButtonLabel(last.record), "button day 7");
        string[] coming = RevealLineBuilder.ComingUp(d, 7);
        Assert.True(coming.Length == 1 && coming[0] == "Tomorrow is payday.", "coming up day 7: " + string.Join("|", coming));
        Assert.Equal("On to Day 2", RevealLineBuilder.ButtonLabel(nights[0].record), "button day 1");
    }

    public static void TestRevealB2Arrears()
    {
        PlayerData d = Fresh();
        DayCycle.EndDay(d); // night 1
        DayCycle.EndDay(d); // night 2: airtime
        MoneyRecorder.Apply(d, -500f, 0f, "Spent", MoneyCategory.Shopping, "test");
        GameEvents.ClearPendingMoneyChanged();
        NightResult n = DayCycle.EndDay(d); // night 3: rent 500 with R40 cash
        string line = RevealLineBuilder.Build(d, n);
        Contains(line, "R460 is still owed for Rent. It comes off tomorrow night from your cash, and the Bank can move savings into cash before then.", "B2");
        Assert.True(n.record.owedAtClose > 459f, "owedAtClose");
        Assert.Equal("Still owed: R460 (R0 this morning)", RevealLineBuilder.StillOwedLine(d, n.record), "still owed line");
        Assert.Equal("−R540 today", RevealLineBuilder.DeltaPill(n.record), "a short night is still shown as money out");
    }

    public static void TestRevealB4B5B6()
    {
        // B4: the new day is 7 and nothing is due on night 7.
        PlayerData d = Fresh();
        d.obligations = Array.Empty<Obligation>();
        for (int i = 0; i < 5; i++)
        {
            DayCycle.EndDay(d);
        }

        NightResult n = DayCycle.EndDay(d); // night 6
        Assert.True(RevealLineBuilder.Build(d, n).EndsWith("Tomorrow is the last day before payday."), "B4");

        // B5: savings moved, nothing due tomorrow.
        d = Fresh();
        d.obligations = Array.Empty<Obligation>();
        n = NightWith(d, Ev("Moved to savings", MoneyCategory.SavingsMove, -100f, 100f, "bank:to_savings"));
        Assert.True(RevealLineBuilder.Build(d, n).EndsWith("Savings is at R500 of your R2 000 emergency buffer."), "B5: " + RevealLineBuilder.Build(d, n));

        // B6.
        d = Fresh();
        d.obligations = Array.Empty<Obligation>();
        n = NightWith(d);
        Assert.True(RevealLineBuilder.Build(d, n).EndsWith("Payday is in 6 days."), "B6: " + RevealLineBuilder.Build(d, n));
    }

    public static void TestRevealStretchedPrefix()
    {
        PlayerData d = Fresh();
        d.financialStats.financialStress = 65f;
        NightResult n = NightWith(d);
        string line = RevealLineBuilder.Build(d, n);
        Assert.True(line.StartsWith("You seem stretched, Thandeka. "), "prefix: " + line);
        Assert.True(!RevealLineBuilder.Build(d, n, false).StartsWith("You seem"), "no prefix when dropped");
    }

    public static void TestRevealFreshEqualsRebuilt()
    {
        foreach (string focus in Foci)
        {
            foreach (var prefs in new[] { Middle })
            {
                PlayerData d = Fresh(focus);
                for (int i = 0; i < 7; i++)
                {
                    NightResult fresh = PlayDay(d, prefs);
                    NightResult rebuilt = DayCycle.Rebuild(d, fresh.endedDay);
                    Assert.True(rebuilt != null && rebuilt.settlement == null, "rebuild");
                    Assert.Equal(RevealLineBuilder.Build(d, fresh), RevealLineBuilder.Build(d, rebuilt), focus + " night " + fresh.endedDay);
                    Assert.Equal(string.Join("|", RevealLineBuilder.ComingUp(d, fresh.endedDay)),
                                 string.Join("|", RevealLineBuilder.ComingUp(d, rebuilt.endedDay)), "coming up");
                    Assert.Equal(string.Join("|", RevealLineBuilder.LedgerRows(d, fresh.record).Select(r => r.label + r.amountText)),
                                 string.Join("|", RevealLineBuilder.LedgerRows(d, rebuilt.record).Select(r => r.label + r.amountText)), "rows");
                    Assert.Equal(RevealLineBuilder.StillOwedLine(d, fresh.record) ?? "", RevealLineBuilder.StillOwedLine(d, rebuilt.record) ?? "", "owed");
                    Assert.Equal(fresh.record.EndTotal, rebuilt.record.EndTotal, "end total");
                    Assert.Equal(fresh.record.endCash + fresh.record.endSavings, fresh.record.EndTotal, "total = cash + savings");
                }
            }
        }
    }

    public static void TestRevealNumbersEqualTheDayRecord()
    {
        PlayerData d = Fresh();
        NightResult n = PlayDay(d, Middle);
        Assert.Equal(1000f, n.record.StartTotal, "start");
        Assert.Equal(1100f, n.record.EndTotal, "end");
        Assert.Equal("+R100 today", RevealLineBuilder.DeltaPill(n.record), "pill");
        Assert.Equal("Cash R700 · Savings R400", RevealLineBuilder.SplitLine(n.record), "split");
        float sum = RevealLineBuilder.LedgerRows(d, n.record).Where(r => r.kind != MoneyEventKind.Transfer).Sum(r => r.amount);
        Assert.Equal(n.record.EndTotal - n.record.StartTotal, sum, "rows sum to the day's change");
        string[] coming = RevealLineBuilder.ComingUp(d, 1);
        Assert.Equal("Day 2 night: Airtime R60", coming[0], "coming up bill");
        Assert.Equal("Tomorrow: no data, and a deal at the phone shop.", coming[1], "teaser");
        Assert.Equal("Day 1 is done. You started with R1 000 and ended with R1 100.", RevealLineBuilder.PlainLine(n.record), "plain");
    }

    public static void TestRevealDay3MiddlePath()
    {
        PlayerData d = Fresh();
        PlayDay(d, Middle);
        PlayDay(d, Middle);
        NightResult n = PlayDay(d, Middle);
        Assert.Equal(1055f, n.record.StartTotal, "day 3 start");
        Assert.Equal(656f, n.record.EndTotal, "day 3 end");
        Assert.Equal("−R399 today", RevealLineBuilder.DeltaPill(n.record), "pill");
        string[] promises = RevealLineBuilder.NewPromiseLines(d, 3);
        Assert.True(promises.Length == 1 && promises[0] == "New promise: Taxi R34 × 4, Days 4–7", "promise: " + string.Join("|", promises));
        Assert.Equal("Rent took the most today: R500. Tomorrow night, Speaker takes R130. You've got R256 in cash.",
                     RevealLineBuilder.Build(d, n), "spec 1.2 line");
        string[] coming = RevealLineBuilder.ComingUp(d, 3);
        Assert.Equal("Day 4 night: Speaker R130", coming[0], "coming 1");
        Assert.Equal("Tomorrow: a call from home, and a birthday.", coming[1], "coming 2");
    }

    public static void TestComingUpFollowUpSecond()
    {
        foreach (string choice in new[] { "send_part", "cant_this_week" })
        {
            PlayerData d = Fresh();
            d.obligations = Array.Empty<Obligation>();
            for (int i = 0; i < 3; i++)
            {
                DayCycle.EndDay(d);
            }

            PlayChoice(d, "family_obligation", choice); // Day 4 -> call-back on Day 6
            GameEvents.ClearPendingMoneyChanged();
            DayCycle.EndDay(d); // night 4
            d.obligations = new[] { new Obligation { obligationId = "x", label = "Speaker", shortLabel = "Speaker", amount = 130f, nextDueDay = 6, paymentsRemaining = 1, intervalDays = 2 } };
            string[] coming = RevealLineBuilder.ComingUp(d, 5);
            Assert.True(coming.Length == 2 && coming[0] == "Day 6 night: Speaker R130" && coming[1] == "Tomorrow: your aunt calls back.",
                        choice + ": " + string.Join("|", coming));
            d.obligations = Array.Empty<Obligation>();
            coming = RevealLineBuilder.ComingUp(d, 5);
            Assert.True(coming[0] == "Tomorrow: your aunt calls back." && coming[1] == "Tomorrow: the stokvel, and your neighbour.",
                        choice + " without a bill: " + string.Join("|", coming));
        }

        PlayerData plain = Fresh();
        plain.obligations = Array.Empty<Obligation>();
        string[] none = RevealLineBuilder.ComingUp(plain, 5);
        Assert.True(none.Length == 1 && none[0] == "Tomorrow: the stokvel, and your neighbour.", "teaser only");
    }

    // ------------------------------------------------------------------ ledger rows

    public static void TestLedgerBankMovesAreGrouped()
    {
        PlayerData d = Fresh();
        NightResult n = NightWith(d,
            Ev("Moved to savings", MoneyCategory.SavingsMove, -50f, 50f, "bank:to_savings"),
            Ev("Vetkoek and mince", MoneyCategory.Food, -20f, 0f, "scenario:food_decision/vetkoek"),
            Ev("Moved to savings", MoneyCategory.SavingsMove, -100f, 100f, "bank:to_savings"),
            Ev("Taken out of savings", MoneyCategory.SavingsMove, 200f, -200f, "bank:from_savings"),
            Ev("Taken out of savings", MoneyCategory.SavingsMove, 50f, -50f, "bank:from_savings"));
        List<RevealRow> rows = RevealLineBuilder.LedgerRows(d, n.record);
        Assert.True(rows.Count == 3, "three rows, got " + rows.Count);
        Assert.Equal("Moved to savings", rows[0].label, "grouped at the first");
        Assert.Equal("R150 to savings", rows[0].amountText, "to savings sum");
        Assert.True(rows[0].kind == MoneyEventKind.Transfer, "transfer kind");
        Assert.Equal("Vetkoek and mince", rows[1].label, "chronological");
        Assert.Equal("−R20", rows[1].amountText, "out");
        Assert.Equal("Taken out of savings", rows[2].label, "from savings row");
        Assert.Equal("R250 from savings", rows[2].amountText, "from savings sum");
        Assert.Equal("−R20 today", RevealLineBuilder.DeltaPill(n.record), "transfers neutral");
    }

    public static void TestLedgerOtherBeyondSeven()
    {
        PlayerData d = Fresh();
        var events = new List<MoneyEvent>();
        for (int i = 1; i <= 9; i++)
        {
            events.Add(Ev("Thing " + i, MoneyCategory.Shopping, -10f, 0f, "scenario:s" + i + "/c"));
        }

        NightResult n = NightWith(d, events.ToArray());
        List<RevealRow> rows = RevealLineBuilder.LedgerRows(d, n.record);
        Assert.True(rows.Count == 7, "7 rows");
        Assert.Equal("Thing 6", rows[5].label, "rows 1-6 kept");
        Assert.Equal("Other (3)", rows[6].label, "other row");
        Assert.Equal("−R30", rows[6].amountText, "other summed");

        PlayerData seven = Fresh();
        n = NightWith(seven, events.Take(7).ToArray());
        Assert.True(RevealLineBuilder.LedgerRows(seven, n.record).All(r => !r.isOther), "exactly 7: no Other row");

        PlayerData empty = Fresh();
        n = NightWith(empty);
        Assert.True(RevealLineBuilder.LedgerRows(empty, n.record).Count == 0, "empty ledger");
    }

    // ------------------------------------------------------------------ chapter summary

    public static void TestChapterSummaryMiddlePath()
    {
        PlayerData d = Fresh();
        PlayWeek(d, Middle);
        ChapterSummary s = ChapterReflection.Summary(d);
        Assert.Equal(1000f, s.startTotal, "start");
        Assert.Equal(515f, s.endTotal, "end");
        Assert.Equal(315f, s.endCash, "cash");
        Assert.Equal(200f, s.endSavings, "savings");
        Assert.Equal("−R485 this week", ChapterReflection.WeekPill(s), "week pill");
        Assert.True(ChapterReflection.StillOwedLine(s) == null, "nothing owed: omitted");
        Assert.Equal("Already promised for payday: R399", ChapterReflection.PromisedLine(s), "promised");
        Assert.Equal("Stokvel R200 · Gym R199", ChapterReflection.PromisedList(s), "promised list");
        Assert.True(s.top.Length == 3, "three moments");
        Assert.Equal("Geyser repair from savings", s.top[0].label, "top 1");
        Assert.True(s.topDays[0] == 5, "top 1 day");
        Assert.Equal(300f, s.top[1].TotalDelta, "top 2 neighbour +300");
        Assert.True(s.topDays[1] == 6, "top 2 day");
        Assert.True(s.topDays[2] == 2 && Math.Abs(s.top[2].TotalDelta + 120f) < 0.01f, "top 3 = Day 2 speaker deposit (tie to earlier)");
        Assert.True(s.top.All(e => e.sourceId.StartsWith("scenario:") && e.kind != MoneyEventKind.Transfer), "only scenario events");
        Assert.Equal(-986f, s.billsTotal, "bills");
        Assert.Equal("Bills and repayments: −R986", ChapterReflection.BillsLine(s), "bills line");
    }

    public static void TestTopExcludesTransfersAndNonScenario()
    {
        PlayerData d = Fresh();
        NightWith(d,
            Ev("Hoodie money to savings", MoneyCategory.SavingsMove, -300f, 300f, "scenario:impulse_purchase/to_savings"),
            Ev("Shift at work", MoneyCategory.Work, 150f, 0f, "work:1"),
            Ev("Moved to savings", MoneyCategory.SavingsMove, -200f, 200f, "bank:to_savings"),
            Ev("Vetkoek and mince", MoneyCategory.Food, -20f, 0f, "scenario:food_decision/vetkoek"));
        ChapterSummary s = ChapterReflection.Summary(d);
        Assert.True(s.top.Length == 1 && s.top[0].label == "Vetkoek and mince", "only the scenario out event");
        Assert.True(ChapterReflection.BillsLine(s) == null, "no bills: line omitted");
    }

    public static void TestBillsLineSumsBillEvents()
    {
        PlayerData d = Fresh();
        DayCycle.EndDay(d);
        DayCycle.EndDay(d);
        DayCycle.EndDay(d);
        ChapterSummary s = ChapterReflection.Summary(d);
        Assert.Equal(-560f, s.billsTotal, "airtime + rent");
    }

    // ------------------------------------------------------------------ noticed

    static PlayerData Chapter(Action<PlayerData> build)
    {
        PlayerData d = Fresh();
        build(d);
        return d;
    }

    static void AddChoice(PlayerData d, string scenario, string choice, string tag)
    {
        var list = new List<ChoiceRecord>(d.chapter.choices) { new ChoiceRecord { day = d.currentDay, scenarioId = scenario, choiceId = choice, tag = tag } };
        d.chapter.choices = list.ToArray();
    }

    static string[] Noticed(PlayerData d)
    {
        string[] lines = ChapterReflection.Noticed(d);
        Assert.True(lines.Length >= 2 && lines.Length <= 3, "2-3 lines, got " + lines.Length);
        return lines;
    }

    public static void TestNoticedN0EveryFocus()
    {
        foreach (string focus in Foci)
        {
            PlayerData d = Fresh(focus);
            d.spendingProfile.source = SpendingProfileSource.Onboarding;
            PlayWeek(d, Middle);
            string[] cats = SpendingFocus.Categories(focus);
            float expected = d.chapter.days.SelectMany(x => x.events)
                .Where(e => e.kind == MoneyEventKind.Out && cats.Contains(e.category)).Sum(e => -e.TotalDelta);
            string line = Noticed(d)[0];
            if (expected >= ChapterReflection.N0Minimum)
            {
                Assert.Equal("You said most of your money goes on " + SpendingFocus.Get(focus).maliPhrase + ". This week that came to R"
                             + MoneyFormat.Digits(expected) + ".", line, "N0 " + focus);
            }
            else
            {
                // Revision 4: under R100 N0 is skipped, so a small week never reads as contradicting the player.
                Assert.True(!Noticed(d).Any(l => l.StartsWith("You said")), "no N0 under R100 for " + focus + " (R" + expected + ")");
            }

            if (focus == "transport")
            {
                Assert.Equal(200f, expected, "transport middle path R200");
            }

            if (focus == "food")
            {
                Assert.Equal(50f, expected, "food middle path: vetkoek R20 + half a kota R30");
                PlayerData comfort = Fresh(focus);
                comfort.spendingProfile.source = SpendingProfileSource.Onboarding;
                PlayWeek(comfort, Comfort);
                Assert.Equal("You said most of your money goes on food and takeaways. This week that came to R115.",
                             Noticed(comfort)[0], "N0 food comfort: kota R50 + kota run R65");
            }

            PlayerData def = Fresh(focus);
            PlayWeek(def, Middle);
            Assert.True(!Noticed(def).Any(l => l.StartsWith("You said")), "no N0 with the default source");
        }
    }

    public static void TestNoticedRules()
    {
        Assert.Equal("When the geyser broke, you reached for savings first.",
                     Noticed(Chapter(d => AddChoice(d, "emergency_expense", "from_savings", "Neutral")))[0], "N1");
        Assert.Equal("When the geyser broke, you paid for it from cash the same day.",
                     Noticed(Chapter(d => AddChoice(d, "emergency_expense", "from_cash", "Neutral")))[0], "N1b");
        Assert.Equal("When the geyser broke, you waited it out with cold water.",
                     Noticed(Chapter(d => AddChoice(d, "emergency_expense", "cold_showers", "Deferred")))[0], "N1c");

        PlayerData n2 = Fresh();
        NightWith(n2, Ev("R80 to family", MoneyCategory.Family, -80f, 0f, "scenario:family_obligation/send_part"),
                  Ev("Birthday gift", MoneyCategory.Friends, -50f, 0f, "scenario:group_chat_contribution/gift_only"));
        Assert.Equal("You made room for other people this week: R130 to family and friends.", Noticed(n2)[0], "N2");

        Assert.Equal("Twice you moved a cost to later.", Noticed(Chapter(d =>
        {
            AddChoice(d, "a", "x", "Deferred");
            AddChoice(d, "b", "x", "Deferred");
        }))[0], "N3");
        Assert.Equal("Three times you held on to your money rather than spend it.", Noticed(Chapter(d =>
        {
            for (int i = 0; i < 3; i++) AddChoice(d, "s" + i, "x", "Frugal");
        }))[0], "N4");
        Assert.Equal("Four times you chose the option that made the day easier.", Noticed(Chapter(d =>
        {
            for (int i = 0; i < 4; i++) AddChoice(d, "s" + i, "x", "Discretionary");
        }))[0], "N5");
        Assert.Equal("6 times", ChapterReflection.CountWord(6), "count word");

        PlayerData n6 = Fresh();
        NightWith(n6, Ev("Moved to savings", MoneyCategory.SavingsMove, -100f, 100f, "bank:to_savings"),
                  Ev("Neighbour, split", MoneyCategory.ExtraMoney, 150f, 150f, "scenario:windfall/half_half"));
        Assert.Equal("R250 went into savings over the week.", Noticed(n6)[0], "N6");

        // N6 is the net movement: money taken out of savings counts against money put in.
        PlayerData net = Fresh();
        NightWith(net, Ev("Moved to savings", MoneyCategory.SavingsMove, -200f, 200f, "bank:to_savings"),
                  Ev("Taken out of savings", MoneyCategory.SavingsMove, 150f, -150f, "bank:from_savings"));
        Assert.True(!Noticed(net).Any(l => l.Contains("went into savings")), "N6 net R50: under R100, no line");
        PlayerData net2 = Fresh();
        NightWith(net2, Ev("Neighbour's money to savings", MoneyCategory.ExtraMoney, 0f, 300f, "scenario:windfall/all_to_savings"),
                  Ev("Taken out of savings", MoneyCategory.SavingsMove, 100f, -100f, "bank:from_savings"));
        Assert.Equal("R200 went into savings over the week.", Noticed(net2)[0], "N6 net");
    }

    public static void TestNoticedN7FromOwedAtCloseOnly()
    {
        PlayerData d = Fresh();
        d.chapter.days = new[] { new DayRecord { day = 1, closed = true, owedAtClose = 100f } };
        d.obligations[0].arrears = 45f;
        Assert.Equal("Some nights there wasn't enough cash for everything due. R45 is still owed going into payday.", Noticed(d)[0], "N7");

        d.obligations[0].arrears = 0f;
        Assert.Equal("Some nights there wasn't enough cash for everything due. It carried over and got paid.", Noticed(d)[0], "N7b");

        // Arrears now but no night recorded as owing: neither N7 nor N7b.
        PlayerData e = Fresh();
        e.obligations[0].arrears = 45f;
        Assert.True(!Noticed(e).Any(l => l.StartsWith("Some nights")), "N7 needs owedAtClose");
    }

    public static void TestNoticedFallbacks()
    {
        PlayerData d = Fresh();
        string[] lines = Noticed(d);
        Assert.Equal("You didn't take a shift this week.", lines[0], "F1 zero");
        Assert.Equal("You lived the whole week, one choice at a time.", lines[1], "F2");
        d.chapter.shiftsWorked = 1;
        Assert.Equal("You took a shift on one day this week.", Noticed(d)[0], "F1 one");
        d.chapter.shiftsWorked = 5;
        Assert.Equal("You took a shift on 5 days this week.", Noticed(d)[0], "F1 many");

        // One rule matched: topped up with F1 only.
        PlayerData one = Chapter(x => AddChoice(x, "emergency_expense", "from_cash", "Neutral"));
        one.chapter.shiftsWorked = 7;
        lines = Noticed(one);
        Assert.True(lines.Length == 2 && lines[1] == "You took a shift on 7 days this week.", "F1 after one rule");
    }

    public static void TestNoticedNeverMoreThanThree()
    {
        PlayerData d = Fresh();
        d.spendingProfile.source = SpendingProfileSource.Onboarding;
        AddChoice(d, "emergency_expense", "from_savings", "Neutral");
        for (int i = 0; i < 5; i++) AddChoice(d, "s" + i, "x", "Deferred");
        NightWith(d, Ev("Kota and a cold drink", MoneyCategory.Food, -50f, 0f, "scenario:food_decision/kota"),
                  Ev("Kota run with friends", MoneyCategory.Food, -65f, 0f, "scenario:kota_run/full_kota"),
                  Ev("Towards Gogo's meds", MoneyCategory.Family, -80f, 0f, "scenario:family_obligation/send_part"));
        string[] lines = Noticed(d);
        Assert.True(lines.Length == 3, "three");
        Assert.True(lines[0].StartsWith("You said") && lines[1].StartsWith("When the geyser") && lines[2].StartsWith("You made room"),
                    "first three in order");
    }

    // ------------------------------------------------------------------ banned words

    static readonly Regex[] BannedPatterns = BannedWords
        .Select(w => new Regex(@"\b" + Regex.Escape(w).Replace(@"\ ", @"\s+") + @"\b", RegexOptions.IgnoreCase))
        .ToArray();

    static void AssertClean(string text, string where)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Assert.True(text.IndexOf('!') < 0, where + " contains \"!\": " + text);
        Assert.True(text.IndexOf('{') < 0, where + " has an unfilled token: " + text);
        foreach (Regex p in BannedPatterns)
        {
            Assert.True(!p.IsMatch(text), where + " contains banned \"" + p + "\": " + text);
        }

        foreach (char c in text)
        {
            Assert.True(AileronMetrics.Has(c) && c != ' ', where + " has a glyph Aileron lacks: U+" + ((int)c).ToString("X4"));
        }
    }

    static readonly Dictionary<string, string[]> Comfort = new Dictionary<string, string[]>
    {
        { "food_decision", new[] { "kota" } }, { "transport_decision", new[] { "ride" } }, { "data_runs_out", new[] { "bundle_1gb" } },
        { "credit_bnpl", new[] { "pay_in_full" } }, { "impulse_purchase", new[] { "buy_it" } }, { "taxi_fare_rise", new[] { "lift_club" } },
        { "family_obligation", new[] { "send_full" } }, { "family_callback", new[] { "send_rest" } },
        { "group_chat_contribution", new[] { "in_for_dinner" } }, { "emergency_expense", new[] { "from_cash" } },
        { "mashonisa_offer", new[] { "borrow_400" } }, { "stokvel_decision", new[] { "join" } }, { "windfall", new[] { "keep_cash" } },
        { "debit_order_check", new[] { "move_to_cash" } }, { "kota_run", new[] { "full_kota" } },
    };

    static readonly Dictionary<string, string[]> Deferrer = new Dictionary<string, string[]>
    {
        { "food_decision", new[] { "skip_lunch" } }, { "transport_decision", new[] { "walk" } }, { "data_runs_out", new[] { "day_bundle" } },
        { "credit_bnpl", new[] { "pay_later" } }, { "impulse_purchase", new[] { "walk_away" } },
        { "taxi_fare_rise", new[] { "walk_today", "pay_new_fare" } }, { "family_obligation", new[] { "cant_this_week" } },
        { "family_callback", new[] { "not_this_week" } }, { "family_callback_full", new[] { "not_this_week" } },
        { "group_chat_contribution", new[] { "not_this_time" } }, { "emergency_expense", new[] { "cold_showers" } },
        { "mashonisa_offer", new[] { "borrow_400" } }, { "stokvel_decision", new[] { "not_for_now" } },
        { "windfall", new[] { "all_to_savings" } }, { "debit_order_check", new[] { "leave_it" } },
        { "kota_run", new[] { "cook_home" } },
    };

    public static void TestBannedWordsAbsent()
    {
        foreach (string focus in Foci)
        {
            foreach (var (prefs, works) in new[] { (Middle, true), (Comfort, true), (Middle, false), (Deferrer, true) })
            {
                PlayerData d = Fresh(focus);
                d.spendingProfile.source = SpendingProfileSource.Onboarding;
                for (int i = 0; i < 7; i++)
                {
                    NightResult n = PlayDay(d, prefs, works);
                    string where = focus + " night " + n.endedDay;
                    AssertClean(RevealLineBuilder.Build(d, n), where + " line");
                    foreach (string s in RevealLineBuilder.ComingUp(d, n.endedDay)) AssertClean(s, where + " coming up");
                    foreach (RevealRow r in RevealLineBuilder.LedgerRows(d, n.record)) AssertClean(r.label + " " + r.amountText, where + " row");
                    foreach (string s in RevealLineBuilder.NewPromiseLines(d, n.endedDay)) AssertClean(s, where + " promise");
                    AssertClean(RevealLineBuilder.StillOwedLine(d, n.record), where + " owed");
                    AssertClean(RevealLineBuilder.PlainLine(n.record), where + " plain");
                }

                foreach (string s in ChapterReflection.Noticed(d)) AssertClean(s, focus + " noticed");
                ChapterSummary sum = ChapterReflection.Summary(d);
                AssertClean(ChapterReflection.StillOwedLine(sum), "chapter owed");
                AssertClean(ChapterReflection.PromisedList(sum), "chapter promised");
                AssertClean(ChapterReflection.BillsLine(sum), "chapter bills");
                foreach (MoneyEvent e in sum.top) AssertClean(e.label, "moment");
            }
        }

        foreach (string s in new[]
                 {
                     ChapterReflection.PlanSaved, ChapterReflection.CloseLine(Fresh(), false), ChapterReflection.CloseCaption,
                     ChapterReflection.PlanPrompt1, ChapterReflection.PlanPrompt2, ChapterReflection.NoMoments,
                     RevealLineBuilder.EmptyLedger
                 })
        {
            AssertClean(s, "fixed string");
        }

        foreach (PaydayPlan p in PaydayPlans.All) AssertClean(p.text, "plan");
    }

    // ------------------------------------------------------------------ fit with Aileron metrics

    static List<string> Wrap(string text, string weight, float size, float width)
    {
        var lines = new List<string>();
        float space = AileronMetrics.Width(" ", weight, size);
        var units = new List<string>();
        string[] words = text.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i];
            // Keep "R1 250" whole.
            while (Regex.IsMatch(w, @"R\d{1,3}$") && i + 1 < words.Length && Regex.IsMatch(words[i + 1], @"^\d{3}\b"))
            {
                w += " " + words[++i];
            }

            units.Add(w);
        }

        string line = null;
        float lw = 0f;
        foreach (string u in units)
        {
            float uw = AileronMetrics.Width(u, weight, size);
            if (line != null && lw + space + uw <= width + 0.01f)
            {
                line += " " + u;
                lw += space + uw;
            }
            else
            {
                if (line != null) lines.Add(line);
                line = u;
                lw = uw;
            }
        }

        if (line != null) lines.Add(line);
        return lines;
    }

    static void AssertFits(string text, string weight, float size, float width, int maxLines, string where)
    {
        List<string> lines = Wrap(text, weight, size, width);
        float widest = lines.Max(l => AileronMetrics.Width(l, weight, size));
        Assert.True(lines.Count <= maxLines && widest <= width + 0.01f,
                    where + ": " + lines.Count + " line(s), widest " + widest.ToString("0") + " of " + width + " u: \"" + text + "\"");
    }

    static void AssertOneLine(string text, string weight, float size, float width, string where)
    {
        float w = AileronMetrics.Width(text, weight, size);
        Assert.True(w <= width + 0.01f, where + ": " + w.ToString("0") + " of " + width + " u: \"" + text + "\"");
    }

    /// <summary>Every shortLabel the content can put on an obligation.</summary>
    static List<string> AllShortLabels()
    {
        var labels = new HashSet<string> { "Airtime", "Rent" };
        foreach (string f in Foci)
        foreach (string t in Travels)
        foreach (string id in ScenarioLibrary.AllIds)
        foreach (ScenarioChoice c in ScenarioLibrary.Get(id, f, t).choices)
        {
            if (!string.IsNullOrEmpty(c.instalmentShortLabel)) labels.Add(c.instalmentShortLabel);
        }

        return labels.ToList();
    }

    static readonly float[] Amounts = { 0f, 5f, 60f, 130f, 999f, 1000f, 1111f, 4444f, 9999f };

    public static void TestHudCopyFits()
    {
        PlayerData d = Fresh();
        for (int day = 1; day <= 7; day++)
        {
            d.currentDay = day;
            var dp = HudCopy.DayPill(d);
            AssertOneLine(dp.line1, "Bold", 40, 280, "day line 1");
            AssertOneLine(dp.line2, "SemiBold", 32, 280, "day line 2");
        }

        foreach (float a in Amounts)
        {
            PlayerData t = Fresh();
            t.today.startCash = a;
            t.today.startSavings = 0f;
            t.financialStats.cash = a;
            t.financialStats.savings = 0f;
            var tp = HudCopy.TodayPill(t);
            AssertOneLine(tp.line1, "SemiBold", 32, 280, "today line 1");
            AssertOneLine(tp.nowText, "Black", 44, 200, "today value");
        }

        foreach (string label in AllShortLabels())
        {
            Assert.True(label.Length <= 10, "shortLabel <= 10: " + label);
            foreach (float a in Amounts)
            {
                foreach (int due in new[] { 1, 2, 5, 8 })
                {
                    PlayerData b = Fresh();
                    b.obligations = new[] { new Obligation { obligationId = "o", label = label, shortLabel = label, amount = Math.Max(1f, a), nextDueDay = due, paymentsRemaining = 1 } };
                    var bp = HudCopy.BillPill(b);
                    AssertOneLine(bp.line1, "Bold", 36, 292, "bill line 1");
                    AssertOneLine(bp.line2, "SemiBold", 32, 292, "bill line 2");
                    b.obligations[0].arrears = Math.Max(1f, a);
                    bp = HudCopy.BillPill(b);
                    Assert.True(bp.attention, "arrears");
                    AssertOneLine(bp.line1, "Bold", 36, 292 - 28, "arrears line 1 (+ dot)");
                    AssertOneLine(bp.line2, "SemiBold", 32, 292, "arrears line 2");
                }
            }
        }

        AssertOneLine(HudCopy.NoBillsLine1, "Bold", 36, 292, "no bills");
        AssertOneLine(HudCopy.NoBillsLine2, "SemiBold", 32, 292, "till payday");
    }

    public static void TestMorningLinesAndTeasersFit()
    {
        PlayerData d = Fresh(name: WideName);
        foreach (string focus in Foci)
        {
            for (int day = 1; day <= 7; day++)
            {
                string morning = ChapterSchedule.MorningLine(day, focus);
                Assert.True(morning != null, "morning " + focus + " " + day);
                if (day > 1) morning = "You seem stretched. " + morning;
                AssertFits(MaliText.Fill(morning, d), "SemiBold", 40, 960, 2, "morning " + focus + " " + day);
                AssertOneLine(ChapterSchedule.TeaserForNight(day, focus), "Bold", 36, 940, "teaser " + focus + " " + day);
            }
        }

        AssertOneLine(ChapterSchedule.FollowUpTeaserText, "Bold", 36, 940, "follow-up teaser");
        foreach (string label in AllShortLabels())
        {
            AssertOneLine("Day 7 night: " + label + " R9 999", "Bold", 36, 940, "coming up bill line");
        }
    }

    public static void TestRevealLeftColumnLinesFit()
    {
        const float column = 757f;
        AssertOneLine("Still owed: R9 999 (R9 999 this morning)", "Bold", 36, column, "still owed");
        foreach (string label in AllShortLabels())
        {
            foreach (string tail in new[] { " on Day 7", " on payday", " × 4, Days 4–7", " × 2, Days 4, 6" })
            {
                AssertOneLine("New promise: " + label + " R9 999" + tail, "Bold", 36, column, "new promise");
            }
        }

        AssertOneLine("+9 more new promises", "Bold", 36, column, "more promises");
        AssertOneLine("Cash R9 999 · Savings R9 999", "SemiBold", 32, column, "split");
        AssertOneLine("You started Day 7 with", "SemiBold", 40, column, "started");
        // The delta pill (HudValue, Black 44) sizes to its text + 60 u inside the column (Revision 4: loan days;
        // Bra K lends R400 at most).
        AssertOneLine("−R9 999 today · R400 borrowed", "Black", 44, column - 60f, "delta pill with a loan");

        // Real promise lines from every profile's comfort and deferring paths.
        foreach (string focus in Foci)
        foreach (string travel in Travels)
        foreach (var prefs in new[] { Comfort, Middle, Deferrer })
        {
            PlayerData d = Fresh(focus, travel);
            for (int i = 0; i < 7; i++)
            {
                NightResult n = PlayDay(d, prefs);
                foreach (string s in RevealLineBuilder.NewPromiseLines(d, n.endedDay)) AssertOneLine(s, "Bold", 36, column, "promise " + focus + "/" + travel);
                string owed = RevealLineBuilder.StillOwedLine(d, n.record);
                if (owed != null) AssertOneLine(owed, "Bold", 36, column, "owed " + focus);
                foreach (string s in RevealLineBuilder.ComingUp(d, n.endedDay)) AssertOneLine(s, "Bold", 36, 940, "coming up " + focus);
            }
        }
    }

    public static void TestChapterEndBillsLineFits()
    {
        AssertOneLine(ChapterReflection.BillsLine(-9999f), "SemiBold", 40, 704, "bills line");
        Assert.Equal("Bills and repayments: −R9 999", ChapterReflection.BillsLine(-9999f), "bills text");
        AssertOneLine(ChapterReflection.BillsLine(-1650f), "SemiBold", 40, 704, "bills line max");
    }

    public static void TestShiftGateCopyFits()
    {
        var nouns = new HashSet<string>();
        foreach (string f in Foci)
        foreach (string t in Travels)
        foreach (string id in ScenarioLibrary.AllIds)
        {
            string noun = ScenarioLibrary.Get(id, f, t).gateNoun;
            if (!string.IsNullOrEmpty(noun)) nouns.Add(noun);
        }

        Assert.True(nouns.Count > 5, "gate nouns found");
        foreach (string noun in nouns)
        {
            AssertOneLine("Shift opens after " + noun, "Bold", 40, 776, "work prompt");
            AssertFits("The shift opens after " + noun + ". A shift needs 60 energy.", "SemiBold", 46, 1180, 2, "NotYet line");
        }
    }
}
