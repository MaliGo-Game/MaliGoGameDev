// WP1 tests: the money recorder, the night, bills, the chapter, work, bank, formats, tokens and the
// spending profile (design spec 2.3-2.7, 7.3, section 8 WP1).
using System;
using System.Collections.Generic;
using System.Linq;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;

public static class CoreTests
{
    // ------------------------------------------------------------------ helpers

    static PlayerData NewPlayer()
    {
        PlayerData d = PlayerData.CreateNew();
        d.characterName = "Thandi";
        return d;
    }

    static NightResult Night(PlayerData d)
    {
        NightResult result = DayCycle.EndDay(d);
        Assert.True(result != null, "EndDay on Day " + d.currentDay + " returned null");
        AssertInvariant(d, "after night " + result.endedDay);
        return result;
    }

    static void AssertInvariant(PlayerData d, string when)
    {
        string violation = MoneyRecorder.CheckInvariant(d);
        Assert.True(violation == null, "invariant " + when + ": " + violation);
    }

    static float BillTotal(DayRecord record, string obligationId = null)
    {
        float total = 0f;
        foreach (MoneyEvent e in record.events)
        {
            if (e.sourceId.StartsWith("bill:", StringComparison.Ordinal)
                && (obligationId == null || e.sourceId == "bill:" + obligationId))
            {
                total += e.cashDelta;
            }
        }

        return total;
    }

    static Obligation Find(PlayerData d, string id) => d.obligations.FirstOrDefault(o => o.obligationId == id);

    static Obligation Instalment(string id, string shortLabel, float amount, int interval, int firstDue, int payments,
                                 int createdDay, string kind = Obligation.KindInstalment, string category = MoneyCategory.PayLater)
    {
        return new Obligation
        {
            obligationId = id,
            label = shortLabel,
            shortLabel = shortLabel,
            amount = amount,
            intervalDays = interval,
            nextDueDay = firstDue,
            paymentsRemaining = payments,
            createdDay = createdDay,
            kind = kind,
            category = category
        };
    }

    // ------------------------------------------------------------------ recorder

    public static void TestRecorderInvariantOver200RandomApplies()
    {
        var random = new Random(20261004);
        PlayerData d = NewPlayer();
        int applied = 0, refused = 0, nights = 0;
        for (int i = 0; i < 200; i++)
        {
            int roll = random.Next(100);
            if (roll < 8)
            {
                if (d.chapter.complete)
                {
                    ChapterFlow.RestartChapter(d);
                }
                else
                {
                    Night(d);
                    nights++;
                }
            }
            else
            {
                float cash = random.Next(-250, 251);
                float savings = roll < 40 ? random.Next(-150, 151) : 0f;
                if (roll >= 40 && roll < 55)
                {
                    savings = -cash; // a transfer
                }

                bool zero = Math.Abs(cash) < 0.005f && Math.Abs(savings) < 0.005f;
                bool ok = MoneyRecorder.CanAfford(d, cash, savings) && !d.today.closed;
                int before = d.today.events.Length;
                MoneyEvent e = null;
                Expect.Errors(ok || zero ? 0 : 1, () => e = MoneyRecorder.Apply(d, cash, savings, "Random", MoneyCategory.Shopping, "test:" + i),
                              "apply " + i);
                if (ok && !zero)
                {
                    Assert.True(e != null, "affordable apply " + i + " returned null");
                    Assert.True(d.today.events.Length == before + 1, "event appended " + i);
                    applied++;
                }
                else
                {
                    Assert.True(e == null, "unaffordable or zero apply " + i + " returned an event");
                    Assert.True(d.today.events.Length == before, "nothing appended " + i);
                    refused++;
                }
            }

            Assert.True(d.financialStats.cash >= -0.005f && d.financialStats.savings >= -0.005f, "pools stay >= 0 at step " + i);
            AssertInvariant(d, "at step " + i);
        }

        Assert.True(applied > 50 && refused > 5 && nights > 3, $"random walk exercised enough (applied {applied}, refused {refused}, nights {nights})");
    }

    public static void TestApplyRefusesNegativeResult()
    {
        PlayerData d = NewPlayer();
        MoneyEvent e = null;
        Expect.Errors(1, () => e = MoneyRecorder.Apply(d, -601f, 0f, "Too much", MoneyCategory.Shopping, "test:a"), "cash below zero");
        Assert.True(e == null, "returns null");
        Expect.Errors(1, () => e = MoneyRecorder.Apply(d, 0f, -401f, "Too much", MoneyCategory.Shopping, "test:b"), "savings below zero");
        Assert.True(e == null, "returns null (savings)");
        Assert.Equal(600f, d.financialStats.cash, "cash unchanged");
        Assert.Equal(400f, d.financialStats.savings, "savings unchanged");
        Assert.True(d.today.events.Length == 0, "no event");
        Assert.True(!MoneyRecorder.CanAfford(d, -600.5f, 0f), "CanAfford false past the tolerance");
        Assert.True(MoneyRecorder.CanAfford(d, -600f, -400f), "CanAfford exactly to zero");

        e = MoneyRecorder.Apply(d, -600f, 0f, "Everything", MoneyCategory.Shopping, "test:c");
        Assert.True(e != null, "spending exactly to zero works");
        Assert.Equal(0f, d.financialStats.cash, "cash at zero");
        Assert.True(MoneyRecorder.Apply(d, 0f, 0f, "Nothing", MoneyCategory.Shopping, "test:d") == null, "zero deltas -> null");
        AssertInvariant(d, "end");
    }

    public static void TestApplyOnWrongDayReturnsNullAndOpensNothing()
    {
        PlayerData d = NewPlayer();
        d.currentDay = 2; // today is still Day 1's record
        MoneyEvent e = null;
        Expect.Errors(1, () => e = MoneyRecorder.Apply(d, -20f, 0f, "Lunch", MoneyCategory.Food, "test:a"), "wrong day");
        Assert.True(e == null, "returns null");
        Assert.True(d.today.day == 1, "today not reopened");
        Assert.True(d.today.events.Length == 0, "no event");
        Assert.True(d.chapter.days.Length == 0, "nothing closed");
        Assert.Equal(600f, d.financialStats.cash, "cash unchanged");

        PlayerData closed = NewPlayer();
        MoneyRecorder.CloseDay(closed);
        Expect.Errors(1, () => e = MoneyRecorder.Apply(closed, -20f, 0f, "Lunch", MoneyCategory.Food, "test:b"), "closed day");
        Assert.True(e == null, "closed day returns null");
        Assert.Equal(600f, closed.financialStats.cash, "cash unchanged on a closed day");
        Assert.True(MoneyRecorder.CloseDay(closed) == null, "CloseDay twice -> null");
    }

    public static void TestEventKinds()
    {
        PlayerData d = NewPlayer();
        MoneyEvent pay = MoneyRecorder.Apply(d, 150f, 0f, "Shift at work", MoneyCategory.Work, "work:1");
        MoneyEvent lunch = MoneyRecorder.Apply(d, -20f, 0f, "Vetkoek and mince", MoneyCategory.Food, "scenario:food_decision/vetkoek");
        MoneyEvent move = MoneyRecorder.Apply(d, -100f, 100f, "Moved to savings", MoneyCategory.SavingsMove, "bank:to_savings");
        MoneyEvent fromSavings = MoneyRecorder.Apply(d, 0f, -50f, "Geyser", MoneyCategory.Home, "scenario:x/y");
        MoneyEvent mixedIn = MoneyRecorder.Apply(d, 30f, -10f, "Mixed", MoneyCategory.ExtraMoney, "scenario:x/z");
        Assert.True(pay.kind == MoneyEventKind.In, "+150 is In");
        Assert.True(lunch.kind == MoneyEventKind.Out, "-20 is Out");
        Assert.True(move.kind == MoneyEventKind.Transfer, "cash to savings is Transfer");
        Assert.True(fromSavings.kind == MoneyEventKind.Out, "savings -50 is Out");
        Assert.True(mixedIn.kind == MoneyEventKind.In, "+30/-10 is In");
        Assert.Equal(-20f, lunch.TotalDelta, "TotalDelta");
        Assert.Equal("scenario:food_decision/vetkoek", lunch.sourceId, "sourceId kept");
        Assert.Equal(MoneyCategory.Food, lunch.category, "category kept");
        Assert.True(d.today.events.Length == 5, "five events recorded");
        AssertInvariant(d, "kinds");
    }

    public static void TestMoneyChangedOnlyOnFlush()
    {
        PlayerData d = NewPlayer();
        var seen = new List<MoneyEvent>();
        Action<MoneyEvent> handler = e => seen.Add(e);
        GameEvents.MoneyChanged += handler;
        try
        {
            MoneyEvent a = MoneyRecorder.Apply(d, -20f, 0f, "Lunch", MoneyCategory.Food, "test:a");
            MoneyEvent b = MoneyRecorder.Apply(d, -30f, 0f, "Taxi", MoneyCategory.Transport, "test:b");
            Assert.True(seen.Count == 0, "not raised inside the mutator");
            Assert.True(GameEvents.PendingMoneyChangedCount == 2, "two queued");
            GameEvents.FlushMoneyChanged();
            Assert.True(seen.Count == 2 && seen[0] == a && seen[1] == b, "raised once per event, in order");
            Assert.True(GameEvents.PendingMoneyChangedCount == 0, "queue emptied");
            GameEvents.FlushMoneyChanged();
            Assert.True(seen.Count == 2, "a second flush raises nothing");
        }
        finally
        {
            GameEvents.MoneyChanged -= handler;
        }
    }

    // ------------------------------------------------------------------ bills and the night

    public static void TestBillTimingNights123()
    {
        PlayerData d = NewPlayer();
        NightResult n1 = Night(d);
        NightResult n2 = Night(d);
        NightResult n3 = Night(d);
        Assert.Equal(0f, BillTotal(n1.record), "night 1 charges nothing");
        Assert.Equal(-60f, BillTotal(n2.record), "night 2 charges airtime R60");
        Assert.Equal(-500f, BillTotal(n3.record), "night 3 charges rent R500");
        MoneyEvent airtime = n2.record.events.Single(e => e.sourceId == "bill:" + ObligationDefaults.AirtimeId);
        Assert.Equal("Airtime", airtime.label, "airtime label");
        Assert.Equal(MoneyCategory.PhoneData, airtime.category, "airtime category");
        MoneyEvent rent = n3.record.events.Single(e => e.sourceId == "bill:" + ObligationDefaults.RentId);
        Assert.Equal("Rent", rent.label, "rent label");
        Assert.Equal(MoneyCategory.Bills, rent.category, "rent category");
        Assert.True(n1.settlement != null && n1.settlement.day == 1, "settlement for the ended day");
        Assert.True(Find(d, ObligationDefaults.AirtimeId).nextDueDay == 9, "airtime next due Day 9");
        Assert.True(Find(d, ObligationDefaults.RentId).nextDueDay == 10, "rent next due Day 10");
        Assert.Equal(40f, d.financialStats.cash, "R600 - R60 - R500");
        Assert.True(d.currentDay == 4 && d.today.day == 4 && !d.today.closed, "Day 4 open");
    }

    public static void TestArrearsPaidAndCarried()
    {
        PlayerData d = NewPlayer();
        Night(d);
        Night(d); // airtime: cash 540
        MoneyRecorder.Apply(d, -240f, 0f, "Spent", MoneyCategory.Shopping, "test:spend");
        float stressBefore = d.financialStats.financialStress;
        NightResult n3 = Night(d);
        MoneyEvent part = n3.record.events.Single(e => e.sourceId == "bill:rent");
        Assert.Equal(-300f, part.cashDelta, "night 3 pays what there is");
        Assert.Equal("Rent (part)", part.label, "part label");
        Assert.Equal(200f, Find(d, "rent").arrears, "R200 carried");
        Assert.Equal(200f, n3.record.owedAtClose, "owedAtClose on the record");
        Assert.Equal(200f, d.chapter.days[2].owedAtClose, "owedAtClose in chapter.days");
        Assert.Equal(stressBefore + ObligationDefaults.MissedPaymentStress, d.financialStats.financialStress, "+10 stress");
        Assert.True(n3.settlement.AnyStillOwed, "settlement still owed");
        Assert.True(ObligationLedger.LargestArrears(d) == Find(d, "rent"), "largest arrears is rent");

        Assert.True(WorkRules.DoShift(d, null) != null, "shift on Day 4");
        NightResult n4 = Night(d); // arrears first: 150 of 200
        Assert.Equal(-150f, BillTotal(n4.record, "rent"), "night 4 pays arrears with what there is");
        Assert.Equal(50f, Find(d, "rent").arrears, "R50 still carried");
        Assert.Equal(50f, n4.record.owedAtClose, "owed after night 4");

        BankRules.TakeOut(d, 100f);
        NightResult n5 = Night(d);
        MoneyEvent rest = n5.record.events.Single(e => e.sourceId == "bill:rent");
        Assert.Equal(-50f, rest.cashDelta, "night 5 pays the rest");
        Assert.Equal("Rent", rest.label, "full label once nothing is left owing");
        Assert.Equal(0f, Find(d, "rent").arrears, "nothing owed");
        Assert.Equal(0f, n5.record.owedAtClose, "owedAtClose 0");
        Assert.True(ObligationLedger.LargestArrears(d) == null, "no arrears");
        Assert.True(Find(d, "rent").nextDueDay == 10, "rent schedule unchanged by arrears");
    }

    public static void TestZeroCashNightRecordsNoEventButOwed()
    {
        PlayerData d = NewPlayer();
        Night(d);
        Night(d);
        float cash = d.financialStats.cash;
        MoneyRecorder.Apply(d, -cash, cash, "Moved to savings", MoneyCategory.SavingsMove, "bank:to_savings");
        NightResult n3 = Night(d);
        Assert.True(n3.record.events.Length == 1, "only the transfer, no bill event");
        Assert.Equal(0f, BillTotal(n3.record), "nothing charged");
        Assert.Equal(500f, n3.record.owedAtClose, "R500 owed at close");
        Assert.Equal(500f, d.chapter.days[2].owedAtClose, "owed persisted");
        Assert.Equal(cash + 400f, d.financialStats.savings, "savings never touched by bills");
        Assert.True(n3.settlement.payments.Count == 1 && n3.settlement.payments[0].paid == 0f, "settlement lists the unpaid bill");
    }

    public static void TestPayLaterChosenDay2ChargedNights4And6()
    {
        PlayerData d = NewPlayer();
        Night(d); // Day 2
        MoneyRecorder.Apply(d, -120f, 0f, "Speaker deposit", MoneyCategory.PayLater, "scenario:credit_bnpl/pay_later");
        ObligationDefaults.AddObligation(d, Instalment("speaker", "Speaker", 130f, 2, 4, 2, 2));
        var charged = new Dictionary<int, float>();
        for (int day = 2; day <= 7; day++)
        {
            if (d.financialStats.cash < 200f)
            {
                MoneyRecorder.Apply(d, 300f, 0f, "Top up", MoneyCategory.ExtraMoney, "test:topup");
            }

            NightResult night = Night(d);
            charged[night.endedDay] = BillTotal(night.record, "speaker");
        }

        Assert.Equal(0f, charged[2], "not night 2");
        Assert.Equal(0f, charged[3], "not night 3");
        Assert.Equal(-130f, charged[4], "night 4");
        Assert.Equal(0f, charged[5], "not night 5");
        Assert.Equal(-130f, charged[6], "night 6");
        Assert.Equal(0f, charged[7], "not night 7");
        Assert.True(Find(d, "speaker") == null, "finished instalment removed");
    }

    public static void TestLoanDay5ChargedNight7BeforeCompletion()
    {
        PlayerData d = NewPlayer();
        for (int i = 0; i < 4; i++)
        {
            Night(d);
        }

        Assert.True(d.currentDay == 5, "Day 5");
        MoneyRecorder.Apply(d, 400f, 0f, "Loan from Bra K", MoneyCategory.Loan, "scenario:mashonisa_offer/borrow_400");
        ObligationDefaults.AddObligation(d, Instalment("brak", "Bra K", 600f, 2, 7, 1, 5, Obligation.KindLoan, MoneyCategory.Loan));
        WorkRules.DoShift(d, null);
        Night(d);
        WorkRules.DoShift(d, null);
        Night(d);
        WorkRules.DoShift(d, null);
        Assert.True(d.currentDay == 7 && !d.chapter.complete, "Day 7, not complete yet");
        NightResult n7 = Night(d);
        Assert.True(n7.chapterComplete && d.chapter.complete, "chapter complete after night 7");
        Assert.Equal(-600f, BillTotal(n7.record, "brak"), "loan repaid on night 7, inside Day 7's record");
        Assert.True(d.chapter.days[6].events.Any(e => e.sourceId == "bill:brak"), "repayment in the closed record");
        Assert.True(Find(d, "brak") == null, "loan finished");
    }

    public static void TestRepeatObligationChargedNightly()
    {
        PlayerData d = NewPlayer();
        ObligationDefaults.AddObligation(d, Instalment("bundles", "Data", 15f, 1, 2, 2, 1, Obligation.KindRepeat, MoneyCategory.PhoneData));
        NightResult n1 = Night(d);
        NightResult n2 = Night(d);
        NightResult n3 = Night(d);
        NightResult n4 = Night(d);
        Assert.Equal(0f, BillTotal(n1.record, "bundles"), "not night 1");
        Assert.Equal(-15f, BillTotal(n2.record, "bundles"), "night 2");
        Assert.Equal(-15f, BillTotal(n3.record, "bundles"), "night 3");
        Assert.Equal(0f, BillTotal(n4.record, "bundles"), "done after two");
        Assert.True(n2.record.events.Single(e => e.sourceId == "bill:bundles").category == MoneyCategory.PhoneData, "its own category");
    }

    // ------------------------------------------------------------------ the chapter

    public static void TestChapterCompletesAfterNight7()
    {
        PlayerData d = NewPlayer();
        NightResult last = null;
        for (int i = 1; i <= 7; i++)
        {
            Assert.True(!d.chapter.complete, "not complete before night " + i);
            last = Night(d);
            Assert.True(last.endedDay == i, "ended day " + i);
            Assert.True(d.revealPendingForDay == i, "reveal pending for " + i);
            Assert.Equal(100f, d.financialStats.energy, "energy reset");
        }

        Assert.True(d.chapter.complete && last.chapterComplete, "complete");
        Assert.True(last.newDay == 7, "newDay == endedDay when complete");
        Assert.True(d.currentDay == 7, "currentDay stays 7");
        Assert.True(d.chapter.days.Length == 7, "7 records");
        for (int i = 0; i < 7; i++)
        {
            Assert.True(d.chapter.days[i].day == i + 1 && d.chapter.days[i].closed, "record " + (i + 1));
            if (i > 0)
            {
                Assert.Equal(d.chapter.days[i - 1].endCash, d.chapter.days[i].startCash, "continuous cash " + i);
                Assert.Equal(d.chapter.days[i - 1].endSavings, d.chapter.days[i].startSavings, "continuous savings " + i);
            }
        }

        Assert.Equal(600f, d.chapter.days[0].startCash, "Day 1 starts at the chapter start");
        AssertInvariant(d, "complete");
        Assert.True(DayCycle.EndDay(d) == null, "no night after the chapter ends");
        Assert.True(d.chapter.days.Length == 7, "still 7 records");
    }

    public static void TestSecondEndDayOnClosedDayReturnsNull()
    {
        PlayerData d = NewPlayer();
        MoneyRecorder.CloseDay(d); // today closed but the day not advanced (as after a crash mid-night)
        float cash = d.financialStats.cash;
        Assert.True(DayCycle.EndDay(d) == null, "closed day -> null");
        Assert.True(d.currentDay == 1 && d.financialStats.cash == cash, "nothing changed");

        PlayerData other = NewPlayer();
        other.currentDay = 3; // today.day (1) != currentDay
        Assert.True(DayCycle.EndDay(other) == null, "today.day != currentDay -> null");
        Assert.True(other.chapter.days.Length == 0, "nothing closed");
    }

    public static void TestRebuildMatchesEndDay()
    {
        PlayerData d = NewPlayer();
        for (int i = 1; i <= 7; i++)
        {
            MoneyRecorder.Apply(d, -10f * i, 0f, "Something", MoneyCategory.Food, "test:" + i);
            if (i % 2 == 0)
            {
                WorkRules.DoShift(d, null);
            }

            NightResult fresh = Night(d);
            NightResult rebuilt = DayCycle.Rebuild(d, fresh.endedDay);
            Assert.True(rebuilt != null, "rebuilt " + i);
            Assert.True(rebuilt.settlement == null, "settlement null when rebuilt");
            Assert.True(rebuilt.endedDay == fresh.endedDay, "endedDay " + i);
            Assert.True(rebuilt.chapterComplete == fresh.chapterComplete, "chapterComplete " + i);
            Assert.True(rebuilt.newDay == fresh.newDay, "newDay " + i);
            DayRecord a = fresh.record, b = rebuilt.record;
            Assert.True(a.day == b.day && a.closed && b.closed, "record day " + i);
            Assert.Equal(a.startCash, b.startCash, "startCash " + i);
            Assert.Equal(a.startSavings, b.startSavings, "startSavings " + i);
            Assert.Equal(a.endCash, b.endCash, "endCash " + i);
            Assert.Equal(a.endSavings, b.endSavings, "endSavings " + i);
            Assert.Equal(a.owedAtClose, b.owedAtClose, "owedAtClose " + i);
            Assert.True(a.events.Length == b.events.Length, "events " + i);
            for (int k = 0; k < a.events.Length; k++)
            {
                Assert.Equal(a.events[k].sourceId, b.events[k].sourceId, "event source " + i + "/" + k);
                Assert.Equal(a.events[k].cashDelta, b.events[k].cashDelta, "event cash " + i + "/" + k);
            }
        }

        Assert.True(DayCycle.Rebuild(d, 9) == null, "no record -> null");
        Assert.True(DayCycle.Rebuild(NewPlayer(), 1) == null, "nothing closed yet -> null");
        Assert.True(DayCycle.Rebuild(d, 3).chapterComplete == false && DayCycle.Rebuild(d, 3).newDay == 4, "an earlier night of a complete chapter");
    }

    public static void TestStartChapterValues()
    {
        PlayerData d = NewPlayer();
        d.appearance.skinTone = "deep";
        d.goals = new[] { GoalPresets.CreateGoal(GoalPresets.PhoneId) };
        d.paydayPlanId = PaydayPlans.SaveFirstId;
        d.paydayPlanText = PaydayPlans.Get(PaydayPlans.SaveFirstId).text;
        d.hasMetMali = true;
        SpendingProfiles.SetFromOnboarding(d.spendingProfile, SpendingFocus.HomeFamily, TravelMode.Car, 1700000000L);
        // Play a bit so everything has something to reset.
        WorkRules.DoShift(d, null);
        d.completedScenarioIds = new[] { "food_decision" };
        d.followUps = new[] { "family_callback@6" };
        d.financialStats.financialStress = 70f;
        d.financialStats.spendingBehaviourScore = 0.9f;
        Night(d);
        Night(d);
        d.morningLineDay = 3;

        ChapterFlow.StartChapter(d, 3);
        FinancialStats s = d.financialStats;
        Assert.Equal(600f, s.cash, "cash");
        Assert.Equal(400f, s.savings, "savings");
        Assert.Equal(100f, s.energy, "energy");
        Assert.Equal(25f, s.financialStress, "stress");
        Assert.Equal(0.5f, s.spendingBehaviourScore, "spending score");
        Assert.Equal(0.5f, s.savingBehaviourScore, "saving score");
        Assert.True(d.obligations.Length == 2, "two default bills");
        Assert.True(Find(d, ObligationDefaults.AirtimeId).nextDueDay == 2 && Find(d, ObligationDefaults.RentId).nextDueDay == 3, "airtime Day 2, rent Day 3");
        Assert.True(Find(d, ObligationDefaults.RentId).arrears == 0f, "no arrears");
        Assert.True(d.completedScenarioIds.Length == 0, "completed emptied");
        Assert.True(d.followUps.Length == 0, "followUps emptied");
        Assert.True(d.currentDay == 1 && d.lastWorkedDay == 0, "Day 1, not worked");
        Assert.True(d.revealPendingForDay == 0 && d.morningLineDay == 0, "reveal and morning line reset");
        Assert.True(d.chapter.chapterNumber == 1 && d.chapter.runNumber == 3, "chapter 1, run 3");
        Assert.Equal(600f, d.chapter.startCash, "chapter start cash");
        Assert.Equal(400f, d.chapter.startSavings, "chapter start savings");
        Assert.True(d.chapter.days.Length == 0 && d.chapter.choices.Length == 0 && d.chapter.shiftsWorked == 0 && !d.chapter.complete, "chapter emptied");
        Assert.True(d.today.day == 1 && !d.today.closed && d.today.events.Length == 0, "today opened");
        Assert.Equal(600f, d.today.startCash, "today start cash");
        Assert.Equal(400f, d.today.startSavings, "today start savings");
        Assert.True(d.saveVersion == PlayerData.CurrentSaveVersion && d.saveVersion == 2, "save version 2");
        // Kept.
        Assert.Equal("Thandi", d.characterName, "name kept");
        Assert.Equal("deep", d.appearance.skinTone, "look kept");
        Assert.Equal(GoalPresets.PhoneId, d.GetPrimaryGoal().goalId, "goal kept");
        Assert.Equal(PaydayPlans.SaveFirstId, d.paydayPlanId, "plan id kept");
        Assert.True(d.paydayPlanText.StartsWith("When pay lands, R200", StringComparison.Ordinal), "plan text kept");
        Assert.True(d.hasMetMali, "hasMetMali kept");
        Assert.Equal(SpendingFocus.HomeFamily, d.spendingProfile.focus, "profile focus kept");
        Assert.Equal(TravelMode.Car, d.spendingProfile.travel, "profile travel kept");
        Assert.Equal(SpendingProfileSource.Onboarding, d.spendingProfile.source, "profile source kept");
        AssertInvariant(d, "after StartChapter");
    }

    public static void TestRestartChapterKeepsIdentityAndBumpsRun()
    {
        PlayerData d = NewPlayer();
        d.goals = new[] { GoalPresets.CreateGoal(GoalPresets.DecemberId) };
        d.paydayPlanId = PaydayPlans.WaitADayId;
        d.paydayPlanText = PaydayPlans.Get(PaydayPlans.WaitADayId).text;
        SpendingProfiles.SetFromOnboarding(d.spendingProfile, SpendingFocus.Transport, TravelMode.Walk, 42L);
        for (int i = 0; i < 7; i++)
        {
            Night(d);
        }

        d.followUps = new[] { "family_callback_full@6" };
        Assert.True(d.chapter.complete && d.chapter.runNumber == 1, "first run complete");
        ChapterFlow.RestartChapter(d);
        Assert.True(d.chapter.runNumber == 2 && !d.chapter.complete, "run 2, not complete");
        Assert.True(d.currentDay == 1 && d.morningLineDay == 0, "back to Day 1");
        Assert.Equal("Thandi", d.characterName, "name kept");
        Assert.Equal(GoalPresets.DecemberId, d.GetPrimaryGoal().goalId, "goal kept");
        Assert.Equal(PaydayPlans.WaitADayId, d.paydayPlanId, "plan kept");
        Assert.Equal(SpendingFocus.Transport, d.spendingProfile.focus, "profile kept");
        Assert.True(d.followUps.Length == 0, "followUps emptied");
        ChapterFlow.RestartChapter(d);
        Assert.True(d.chapter.runNumber == 3, "run 3");
    }

    public static void TestCreateNewIsValidDay1()
    {
        PlayerData d = PlayerData.CreateNew();
        Assert.True(d.saveVersion == 2 && !PlayerData.ShouldReset(d.saveVersion), "new save is current");
        Assert.True(d.currentDay == 1 && d.today.day == 1 && !d.today.closed, "Day 1 open");
        Assert.Equal(600f, d.financialStats.cash, "cash 600");
        Assert.Equal(400f, d.financialStats.savings, "savings 400");
        Assert.True(d.chapter.runNumber == 1, "run 1");
        Assert.True(d.spendingProfile != null && d.spendingProfile.focus == SpendingFocus.Food && d.spendingProfile.travel == TravelMode.Taxi
                    && d.spendingProfile.source == SpendingProfileSource.Default, "default profile");
        Assert.True(d.followUps != null && d.followUps.Length == 0, "no follow-ups");
        FinancialGoal goal = d.GetPrimaryGoal();
        Assert.Equal(GoalPresets.BufferId, goal.goalId, "buffer goal");
        Assert.Equal(2000f, goal.targetAmount, "target 2 000");
        Assert.Equal(0f, goal.currentAmount, "retired pot empty");
        Assert.Equal(0.2f, goal.Progress(400f), "progress = savings / target");
        Assert.Equal(1600f, goal.Remaining(400f), "remaining");
        Assert.Equal(0f, goal.Remaining(5000f), "remaining never negative");
        Assert.Equal(1f, goal.Progress(5000f), "progress clamped");
        Obligation airtime = Find(d, ObligationDefaults.AirtimeId);
        Assert.Equal("Airtime", airtime.label, "airtime label");
        Assert.Equal("Airtime", airtime.shortLabel, "airtime short label");
        Assert.Equal(MoneyCategory.PhoneData, airtime.category, "airtime category");
        Assert.Equal(Obligation.KindBill, airtime.kind, "airtime kind");
        Obligation rent = Find(d, ObligationDefaults.RentId);
        Assert.Equal("Rent", rent.shortLabel, "rent short label");
        Assert.Equal(MoneyCategory.Bills, rent.category, "rent category");
        Assert.True(rent.paymentsRemaining == -1 && rent.intervalDays == 7 && rent.createdDay == 0, "rent recurring weekly");
        AssertInvariant(d, "new");
    }

    public static void TestShouldReset()
    {
        Assert.True(PlayerData.ShouldReset(0), "version 0 resets");
        Assert.True(PlayerData.ShouldReset(1), "version 1 resets");
        Assert.True(!PlayerData.ShouldReset(2), "version 2 is kept");
        Assert.True(PlayerData.CurrentSaveVersion == 2, "current version is 2");
    }

    // ------------------------------------------------------------------ shared bill helpers

    public static void TestDueHelpersOnConstructedStates()
    {
        PlayerData d = NewPlayer();
        Assert.True(ObligationLedger.DueOnNight(d, 1).Count == 0, "nothing due night 1");
        List<DueItem> night2 = ObligationLedger.DueOnNight(d, 2);
        Assert.True(night2.Count == 1 && night2[0].obligationId == ObligationDefaults.AirtimeId && night2[0].amount == 60f
                    && night2[0].count == 1 && night2[0].day == 2, "airtime due night 2");
        Assert.Equal("Airtime", night2[0].shortLabel, "short label");
        List<DueItem> night3 = ObligationLedger.DueOnNight(d, 3);
        Assert.True(night3.Count == 2 && night3[0].obligationId == "rent" && night3[1].obligationId == ObligationDefaults.AirtimeId,
                    "what SettleDue(d, 3) would charge from Day 1, largest first");

        DueItem? next = ObligationLedger.NextDue(d);
        Assert.True(next.HasValue && next.Value.obligationId == ObligationDefaults.AirtimeId && next.Value.day == 2, "next due airtime Day 2");
        ObligationDefaults.AddObligation(d, Instalment("tie", "Tie", 130f, 2, 2, 2, 1));
        next = ObligationLedger.NextDue(d);
        Assert.True(next.Value.obligationId == "tie", "tie on the day goes to the larger amount");

        // Arrears are excluded from DueOnNight.
        PlayerData owing = NewPlayer();
        Find(owing, "rent").arrears = 120f;
        Assert.True(ObligationLedger.DueOnNight(owing, 1).Count == 0, "arrears are not newly due");

        // A payday (Day 8) commitment: promised, never due on a played night.
        PlayerData p = NewPlayer();
        ObligationDefaults.AddObligation(p, Instalment("stokvel", "Stokvel", 200f, 7, 8, 1, 6, Obligation.KindCommitment, MoneyCategory.Bills));
        ObligationDefaults.AddObligation(p, Instalment("speaker", "Speaker", 130f, 2, 4, 2, 2));
        ObligationDefaults.AddObligation(p, Instalment("late", "Late", 50f, 2, 6, 3, 4));
        for (int night = 1; night <= 7; night++)
        {
            Assert.True(ObligationLedger.DueOnNight(p, night).All(i => i.obligationId != "stokvel"), "stokvel never due on night " + night);
        }

        List<DueItem> promised = ObligationLedger.Promised(p);
        Assert.True(promised.Count == 2, "stokvel and the late instalment are promised; rent/airtime and the speaker are not");
        Assert.True(promised[0].obligationId == "stokvel" && promised[0].amount == 200f && promised[0].count == 1 && promised[0].day == 8,
                    "stokvel R200 on payday");
        Assert.True(promised[1].obligationId == "late" && promised[1].amount == 50f && promised[1].count == 2 && promised[1].day == 8,
                    "late: 2 of its 3 payments fall on or after Day 8 (8, 10)");
        Assert.Equal(100f, promised[1].Total, "Total = amount x count");

        List<DueItem> newOnDay2 = ObligationLedger.NewPromises(p, 2);
        Assert.True(newOnDay2.Count == 1 && newOnDay2[0].obligationId == "speaker" && newOnDay2[0].amount == 130f
                    && newOnDay2[0].count == 2 && newOnDay2[0].day == 4, "new promise on Day 2: Speaker R130 x 2 from Day 4");
        Assert.True(ObligationLedger.NewPromises(p, 3).Count == 0, "none on Day 3");
        Assert.True(ObligationLedger.NewPromises(p, 6).Single().obligationId == "stokvel", "stokvel promised on Day 6");
        Assert.True(ObligationLedger.NewPromises(p, 0).Count == 0, "base bills are never new promises");

        // Late in the week only the payday commitment is next.
        PlayerData late = NewPlayer();
        ObligationDefaults.AddObligation(late, Instalment("stokvel", "Stokvel", 200f, 7, 8, 1, 6, Obligation.KindCommitment, MoneyCategory.Bills));
        for (int i = 0; i < 6; i++)
        {
            MoneyRecorder.Apply(late, 300f, 0f, "Top up", MoneyCategory.ExtraMoney, "test:topup");
            Night(late);
        }

        Assert.True(late.currentDay == 7, "Day 7");
        DueItem? last = ObligationLedger.NextDue(late);
        Assert.True(last.HasValue && last.Value.obligationId == "stokvel" && last.Value.day == 8, "payday commitment is next (day 8)");
        Assert.True(ObligationLedger.DueOnNight(late, 7).Count == 0, "but nothing is due on night 7");

        // Largest arrears.
        PlayerData a = NewPlayer();
        Assert.True(ObligationLedger.LargestArrears(a) == null, "none");
        Find(a, "rent").arrears = 50f;
        Find(a, ObligationDefaults.AirtimeId).arrears = 60f;
        Assert.True(ObligationLedger.LargestArrears(a).obligationId == ObligationDefaults.AirtimeId, "largest arrears");
        Assert.Equal(110f, ObligationLedger.TotalArrears(a), "total arrears");
    }

    // ------------------------------------------------------------------ work and bank

    public static void TestWorkNeeds60EnergyOncePerDay()
    {
        PlayerData d = NewPlayer();
        d.financialStats.energy = 59f;
        Assert.True(WorkRules.State(d, null) == ShiftState.Tired, "59 energy is tired");
        Assert.True(WorkRules.DoShift(d, null) == null, "no shift when tired");
        Assert.Equal(600f, d.financialStats.cash, "no pay when tired");
        d.financialStats.energy = 60f;
        Assert.True(WorkRules.State(d, null) == ShiftState.Open, "60 energy is open");
        MoneyEvent pay = WorkRules.DoShift(d, null);
        Assert.True(pay != null && pay.cashDelta == 150f && pay.label == "Shift at work" && pay.category == MoneyCategory.Work
                    && pay.sourceId == "work:1", "shift pay event");
        Assert.Equal(750f, d.financialStats.cash, "+R150");
        Assert.Equal(0f, d.financialStats.energy, "energy -60");
        Assert.Equal(5f, d.financialStats.financialXP, "xp +5");
        Assert.True(d.lastWorkedDay == 1 && d.chapter.shiftsWorked == 1, "worked Day 1, one shift");
        d.financialStats.energy = 100f;
        Assert.True(WorkRules.State(d, null) == ShiftState.Done, "done for the day");
        Assert.True(WorkRules.DoShift(d, null) == null, "once per day");
        Night(d);
        Assert.True(WorkRules.State(d, null) == ShiftState.Open, "open again the next day");
        Assert.True(WorkRules.DoShift(d, null).sourceId == "work:2", "Day 2 shift");
        Assert.True(d.chapter.shiftsWorked == 2, "two shifts");
    }

    public static void TestWorkGate()
    {
        PlayerData d = NewPlayer();
        const string gate = "food_decision";
        Assert.True(d.financialStats.energy == 100f, "full energy");
        Assert.True(WorkRules.State(d, gate) == ShiftState.NotYet, "NotYet before the gate, even at 100 energy");
        float cash = d.financialStats.cash, energy = d.financialStats.energy;
        Assert.True(WorkRules.DoShift(d, gate) == null, "DoShift null while NotYet");
        Assert.True(d.financialStats.cash == cash && d.financialStats.energy == energy && d.today.events.Length == 0
                    && d.lastWorkedDay == 0 && d.chapter.shiftsWorked == 0 && d.financialStats.financialXP == 0f, "nothing changed");

        d.financialStats.energy = 30f;
        Assert.True(WorkRules.State(d, gate) == ShiftState.NotYet, "NotYet comes before Tired");
        d.completedScenarioIds = new[] { gate };
        Assert.True(WorkRules.State(d, gate) == ShiftState.Tired, "Tired below 60 after the gate");
        Assert.True(WorkRules.DoShift(d, gate) == null && d.financialStats.cash == cash, "DoShift null while Tired");
        d.financialStats.energy = 100f;
        Assert.True(WorkRules.State(d, gate) == ShiftState.Open, "Open after the gate");
        Assert.True(WorkRules.DoShift(d, gate) != null, "shift taken");
        Assert.True(WorkRules.State(d, gate) == ShiftState.Done, "Done after a shift");
        Assert.True(WorkRules.State(d, "other_gate") == ShiftState.Done, "Done wins over NotYet");

        PlayerData n = NewPlayer();
        n.financialStats.energy = 100f;
        Assert.True(WorkRules.State(n, null) == ShiftState.Open, "null gate: never NotYet");
        Assert.True(WorkRules.State(n, "") == ShiftState.Open, "empty gate: never NotYet");
        n.financialStats.energy = 10f;
        Assert.True(WorkRules.State(n, null) == ShiftState.Tired && WorkRules.State(n, "") == ShiftState.Tired, "no gate, tired");
        Assert.True(WorkRules.HasEnergy(NewPlayer()) && !WorkRules.HasWorkedToday(NewPlayer()), "helpers");
    }

    public static void TestBankRefusesPartialMoves()
    {
        PlayerData d = NewPlayer();
        Assert.True(BankRules.Amounts.Length == 3 && BankRules.Amounts[0] == 50f && BankRules.Amounts[1] == 100f && BankRules.Amounts[2] == 200f, "chips");
        Assert.True(BankRules.MoveToSavings(d, 700f) == null, "cash < amount refused");
        Assert.True(BankRules.TakeOut(d, 500f) == null, "savings < amount refused");
        Assert.True(BankRules.MoveToSavings(d, 0f) == null && BankRules.TakeOut(d, -50f) == null, "non-positive refused");
        Assert.True(d.today.events.Length == 0 && d.financialStats.cash == 600f && d.financialStats.savings == 400f, "nothing moved");

        MoneyEvent move = BankRules.MoveToSavings(d, 200f);
        Assert.True(move != null && move.kind == MoneyEventKind.Transfer && move.sourceId == "bank:to_savings"
                    && move.label == "Moved to savings" && move.category == MoneyCategory.SavingsMove, "move to savings");
        Assert.Equal(400f, d.financialStats.cash, "cash 400");
        Assert.Equal(600f, d.financialStats.savings, "savings 600");
        MoneyEvent take = BankRules.TakeOut(d, 600f);
        Assert.True(take != null && take.sourceId == "bank:from_savings" && take.label == "Taken out of savings" && take.cashDelta == 600f
                    && take.savingsDelta == -600f, "take out all of it");
        Assert.True(BankRules.TakeOut(d, 50f) == null, "savings now 0");
        Assert.True(BankRules.MoveToSavings(d, 1000f).savingsDelta == 1000f, "everything back exactly");
        Assert.True(BankRules.MoveToSavings(d, 50f) == null, "cash now 0");
        AssertInvariant(d, "bank");
    }

    // ------------------------------------------------------------------ formats and copy

    public static void TestMoneyFormat()
    {
        Assert.Equal("R0", MoneyFormat.Rand(0f), "0");
        Assert.Equal("R5", MoneyFormat.Rand(5f), "5");
        Assert.Equal("R1 250", MoneyFormat.Rand(1250f), "1250");
        Assert.Equal("R12 450", MoneyFormat.Rand(12450f), "12450");
        Assert.Equal("R1 000 000", MoneyFormat.Rand(1000000f), "1000000");
        Assert.Equal("R100", MoneyFormat.Rand(100f), "100");
        Assert.Equal("R999", MoneyFormat.Rand(999f), "999");
        Assert.Equal("R1 000", MoneyFormat.Rand(1000f), "1000");
        Assert.Equal("−R45", MoneyFormat.Rand(-45f), "-45 (U+2212)");
        Assert.Equal("−R45", MoneyFormat.Signed(-45f), "Signed -45");
        Assert.Equal("+R150", MoneyFormat.Signed(150f), "Signed +150");
        Assert.Equal("R0", MoneyFormat.Signed(0f), "Signed 0");
        Assert.Equal("R0", MoneyFormat.Signed(-0.3f), "Signed rounds to 0 without a sign");
        Assert.Equal("R50", MoneyFormat.Rand(49.5f), "49.5 rounds half away from zero");
        Assert.Equal("R49", MoneyFormat.Rand(48.5f), "48.5 -> 49 (not banker's)");
        Assert.Equal("−R50", MoneyFormat.Rand(-49.5f), "-49.5 -> -50");
        Assert.Equal("+R1 250", MoneyFormat.Signed(1250f), "Signed grouped");
        Assert.Equal("1 250", MoneyFormat.Digits(1250f), "Digits");
        Assert.Equal("45", MoneyFormat.Digits(-45f), "Digits has no sign");
        Assert.Equal("0", MoneyFormat.Digits(0f), "Digits 0");
        Assert.Equal("12 450", MoneyFormat.Digits(12450f), "Digits grouped");
    }

    public static void TestMaliTextFill()
    {
        PlayerData d = NewPlayer();
        d.financialStats.cash = 1250f;
        d.financialStats.savings = 400f;
        d.financialStats.energy = 35.4f;
        d.currentDay = 3;
        d.paydayPlanText = "When pay lands, I wait one day before any shopping.";
        Assert.Equal("Hi Thandi.", MaliText.Fill("Hi {name}.", d), "name");
        Assert.Equal("Hi Thandi.", MaliText.Fill("Hi {0}.", d), "legacy {0}");
        Assert.Equal("R1 250 cash, R400 saved, R1 650 in all.", MaliText.Fill("R{cash} cash, R{savings} saved, R{total} in all.", d), "amounts");
        Assert.Equal("Your emergency buffer: R2 000, R1 600 to go.", MaliText.Fill("Your {goalName}: R{goalTarget}, R{goalLeft} to go.", d), "goal");
        Assert.Equal("Day 3, payday in 5 days.", MaliText.Fill("Day {day}, payday {paydayWhen}.", d), "day and paydayWhen");
        Assert.Equal("Energy 35.", MaliText.Fill("Energy {energy}.", d), "energy whole number");
        Assert.Equal("\"When pay lands, I wait one day before any shopping.\"", MaliText.Fill("\"{plan}\"", d), "plan");
        d.currentDay = 7;
        Assert.Equal("Payday tomorrow.", MaliText.Fill("Payday {paydayWhen}.", d), "paydayWhen tomorrow");
        d.currentDay = 1;
        Assert.Equal("in 7 days", MaliText.Fill("{paydayWhen}", d), "Day 1");

        Assert.Equal("It's Lerato's birthday.", MaliText.Fill("It's {friend}'s birthday.", d), "friend for a Thandi");
        d.characterName = "thandiwe";
        Assert.Equal("Lerato", MaliText.Fill("{friend}", d), "friend ignores case");
        d.characterName = "Sipho";
        Assert.Equal("Thandi", MaliText.Fill("{friend}", d), "friend default");
        d.characterName = "";
        Assert.Equal("Morning, friend.", MaliText.Fill("Morning, {name}.", d), "empty name -> friend");
        Assert.Equal("Thandi", MaliText.Fill("{friend}", d), "friend for an empty name");

        var extra = new Dictionary<string, string> { { "label", "Rent" }, { "amt", "500" }, { "laterDays", "Days 4 to 7" }, { "gate", "lunch" } };
        Assert.Equal("Rent takes R500, Days 4 to 7, after lunch.", MaliText.Fill("{label} takes R{amt}, {laterDays}, after {gate}.", d, extra), "extra keys");
        Assert.Equal("Keep {mystery} visible.", MaliText.Fill("Keep {mystery} visible.", d), "unknown token left visible");
        Assert.Equal("Braces { like this } and {} stay.", MaliText.Fill("Braces { like this } and {} stay.", d), "non-token braces untouched");
        Assert.Equal("", MaliText.Fill(null, d), "null template");
        Assert.Equal("Hi friend.", MaliText.Fill("Hi {name}.", null), "null data");
    }

    public static void TestGoalPresetsAndPlans()
    {
        Assert.True(GoalPresets.All.Length == 4, "four goals");
        string[] ids = { "buffer", "december", "phone", "hustle" };
        float[] targets = { 2000f, 2500f, 3000f, 1500f };
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(ids[i], GoalPresets.All[i].id, "goal order " + i);
            Assert.Equal(targets[i], GoalPresets.All[i].target, "goal target " + i);
            FinancialGoal goal = GoalPresets.CreateGoal(ids[i]);
            Assert.True(goal.goalId == ids[i] && goal.goalName == GoalPresets.All[i].title && goal.targetAmount == targets[i] && goal.currentAmount == 0f, "CreateGoal " + ids[i]);
            Assert.Equal(GoalPresets.All[i].shortName, GoalPresets.ShortName(goal), "ShortName " + ids[i]);
        }

        Assert.Equal("buffer", GoalPresets.Get("nope").id, "unknown goal -> buffer");
        Assert.Equal("December trip home", GoalPresets.ShortName(GoalPresets.CreateGoal("december")), "short name keeps its capital");
        Assert.Equal("pay down debt", GoalPresets.ShortName(new FinancialGoal("goal_debt", "Pay Down Debt", 1f, 0f, 0f)), "fallback lower case");

        Assert.True(PaydayPlans.All.Length == 4, "four plans");
        Assert.Equal("save_first", PaydayPlans.All[0].id, "plan order");
        Assert.Equal("wait_a_day", PaydayPlans.All[3].id, "plan order 4");
        Assert.Equal("When pay lands, I pay what I owe first, then everything else.", PaydayPlans.Get("owed_first").text, "plan text");
        Assert.True(string.IsNullOrEmpty(PaydayPlans.Get("nope").id), "unknown plan -> empty");
        Assert.True(ChapterConfig.DaysToPayday(1) == 7 && ChapterConfig.DaysToPayday(7) == 1, "DaysToPayday");
    }

    // ------------------------------------------------------------------ spending profile (Revision 3)

    public static void TestNewSpendingProfileDefaults()
    {
        var p = new SpendingProfile();
        Assert.Equal("food", p.focus, "focus");
        Assert.Equal("taxi", p.travel, "travel");
        Assert.Equal("default", p.source, "source");
        Assert.True(p.version == 1 && SpendingProfile.CurrentVersion == 1, "version 1");
        Assert.True(p.categoryShares != null && p.categoryShares.Length == 0, "categoryShares empty");
        Assert.True(p.recurringDebits != null && p.recurringDebits.Length == 0, "recurringDebits empty");
        Assert.True(p.paydayDayOfMonth == 0 && p.updatedUnixSeconds == 0, "unknown payday, never set");
    }

    public static void TestProfileNormalize()
    {
        foreach (string id in new[] { "food", "transport", "data_social", "home_family" })
        {
            Assert.Equal(id, SpendingFocus.Normalize(id), "focus " + id);
        }

        foreach (string id in new[] { "taxi", "ehailing", "walk", "car" })
        {
            Assert.Equal(id, TravelMode.Normalize(id), "travel " + id);
        }

        foreach (string bad in new[] { null, "", "FOOD ", "bus", "Food", "TAXI" })
        {
            Assert.Equal("food", SpendingFocus.Normalize(bad), "focus default for '" + bad + "'");
            Assert.Equal("taxi", TravelMode.Normalize(bad), "travel default for '" + bad + "'");
        }

        Assert.True(SpendingFocus.All.Length == 4 && TravelMode.All.Length == 4, "four options each");
        Assert.Equal("Data, airtime and going out", SpendingFocus.All[2].cardLabel, "focus card label");
        Assert.Equal("getting around", SpendingFocus.All[1].maliPhrase, "focus Mali phrase");
        Assert.Equal("E-hailing rides", TravelMode.All[1].cardLabel, "travel card label");
        foreach (ProfileOption option in TravelMode.All)
        {
            Assert.Equal(option.cardLabel.ToLowerInvariant(), option.maliPhrase, "travel phrase = label in lower case");
        }
    }

    public static void TestSetFromOnboardingAll16()
    {
        foreach (ProfileOption focus in SpendingFocus.All)
        {
            foreach (ProfileOption travel in TravelMode.All)
            {
                var p = new SpendingProfile();
                SpendingProfiles.SetFromOnboarding(p, focus.id, travel.id, 1760000000L);
                Assert.Equal(focus.id, p.focus, "focus " + focus.id + "/" + travel.id);
                Assert.Equal(travel.id, p.travel, "travel " + focus.id + "/" + travel.id);
                Assert.Equal(SpendingProfileSource.Onboarding, p.source, "source " + focus.id + "/" + travel.id);
                Assert.True(p.updatedUnixSeconds == 1760000000L, "time " + focus.id + "/" + travel.id);
            }
        }

        var odd = new SpendingProfile();
        SpendingProfiles.SetFromOnboarding(odd, "bus", null, 5L);
        Assert.True(odd.focus == "food" && odd.travel == "taxi" && odd.source == "onboarding", "unknown taps are normalised");
    }

    public static void TestSpendingFocusCategories()
    {
        Assert.True(SpendingFocus.Categories("food").SequenceEqual(new[] { MoneyCategory.Food }), "food");
        Assert.True(SpendingFocus.Categories("transport").SequenceEqual(new[] { MoneyCategory.Transport }), "transport");
        Assert.True(SpendingFocus.Categories("data_social").SequenceEqual(new[] { MoneyCategory.PhoneData, MoneyCategory.Friends }), "data_social");
        Assert.True(SpendingFocus.Categories("home_family").SequenceEqual(new[] { MoneyCategory.Home, MoneyCategory.Family }), "home_family");
        Assert.True(SpendingFocus.Categories("nope").SequenceEqual(new[] { MoneyCategory.Food }), "unknown -> food");
        foreach (ProfileOption option in SpendingFocus.All)
        {
            Assert.True(SpendingFocus.Categories(option.id).All(MoneyCategory.IsKnown), "known categories for " + option.id);
        }
    }

    public static void TestOnboardingCopy()
    {
        Assert.Equal("Live the week before payday.", OnboardingCopy.PromiseLine1, "promise 1");
        Assert.Equal("See where your money goes.", OnboardingCopy.PromiseLine2, "promise 2");
        Assert.Equal("What should we call you?", OnboardingCopy.NameQuestion, "name question");
        Assert.Equal("Where does most of your money go?", OnboardingCopy.SpendQuestion, "spend question");
        Assert.Equal("How do you usually get around?", OnboardingCopy.TravelQuestion, "travel question");
        Assert.Equal("We've built your week around where your money goes.", OnboardingCopy.WeekBuiltLine, "week line");
        Assert.Equal("Style 6", OnboardingCopy.StyleLabel(6), "style label");
    }

    public static void TestMoneyCategories()
    {
        Assert.True(MoneyCategory.All.Length == 13, "13 categories");
        Assert.Equal("Phone & data", MoneyCategory.PhoneData, "phone & data");
        Assert.Equal("Pay-later", MoneyCategory.PayLater, "pay-later");
        Assert.Equal("Extra money", MoneyCategory.ExtraMoney, "extra money");
        Assert.Equal("Savings move", MoneyCategory.SavingsMove, "savings move");
    }
}
