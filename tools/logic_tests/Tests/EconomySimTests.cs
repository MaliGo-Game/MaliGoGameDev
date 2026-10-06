// WP9 tests: the economy of the chapter, locked to the design spec 3.2 reference tables (section 8 WP9).
// The six reference play styles are played through all 7 days for every spending profile (4 foci x 4 travel
// modes) with the real game logic: ScenarioLibrary.Get, ScenarioOutcome, WorkRules (with the gate) and
// DayCycle, the way tools/sim_chapter.py plays them: each day the gate scenario, then the shift if the style
// works and it is Open, then the other active scenarios in ChapterSchedule.ActiveScenarioIds order, then sleep.
// If these numbers and the script disagree, the spec's content is the referee: fix whichever is wrong and rerun
// both.
using System;
using System.Collections.Generic;
using System.Linq;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.Scenarios;

public static class EconomySimTests
{
    // ------------------------------------------------------------------ play styles (spec 3.2)

    sealed class Style
    {
        public string Name;
        public Dictionary<string, string[]> Prefs;
        public bool Works;

        /// <summary>At the day's first scenario, avoid choices that leave less than the shift's energy.</summary>
        public bool KeepsShift;
    }

    static readonly Dictionary<string, string[]> Saver = new Dictionary<string, string[]>
    {
        { "food_decision", new[] { "skip_lunch" } },
        { "transport_decision", new[] { "walk" } },
        { "data_runs_out", new[] { "free_wifi" } },
        { "credit_bnpl", new[] { "leave_it" } },
        { "impulse_purchase", new[] { "to_savings", "walk_away" } },
        { "taxi_fare_rise", new[] { "walk_today" } },
        { "family_obligation", new[] { "cant_this_week" } },
        { "family_callback", new[] { "not_this_week" } },
        { "family_callback_full", new[] { "not_this_week" } },
        { "group_chat_contribution", new[] { "not_this_time" } },
        { "emergency_expense", new[] { "from_savings" } },
        { "mashonisa_offer", new[] { "not_today" } },
        { "stokvel_decision", new[] { "join" } },
        { "windfall", new[] { "all_to_savings" } },
        { "debit_order_check", new[] { "cancel_gym" } },
    };

    static readonly Dictionary<string, string[]> Middle = new Dictionary<string, string[]>
    {
        { "food_decision", new[] { "vetkoek" } },
        { "transport_decision", new[] { "usual" } },
        { "data_runs_out", new[] { "day_bundle" } },
        { "credit_bnpl", new[] { "pay_later" } },
        { "impulse_purchase", new[] { "walk_away" } },
        { "taxi_fare_rise", new[] { "pay_new_fare" } },
        { "family_obligation", new[] { "send_part" } },
        { "family_callback", new[] { "send_rest", "from_savings" } },
        { "group_chat_contribution", new[] { "gift_only" } },
        { "emergency_expense", new[] { "from_savings", "from_cash" } },
        { "mashonisa_offer", new[] { "not_today" } },
        { "stokvel_decision", new[] { "join" } },
        { "windfall", new[] { "half_half" } },
        { "debit_order_check", new[] { "leave_it" } },
    };

    static readonly Dictionary<string, string[]> Comfort = new Dictionary<string, string[]>
    {
        { "food_decision", new[] { "kota" } },
        { "transport_decision", new[] { "ride" } },
        { "data_runs_out", new[] { "bundle_1gb" } },
        { "credit_bnpl", new[] { "pay_in_full" } },
        { "impulse_purchase", new[] { "buy_it" } },
        { "taxi_fare_rise", new[] { "lift_club" } },
        { "family_obligation", new[] { "send_full" } },
        { "family_callback", new[] { "send_rest" } },
        { "group_chat_contribution", new[] { "in_for_dinner" } },
        { "emergency_expense", new[] { "from_cash" } },
        { "mashonisa_offer", new[] { "not_today" } },
        { "stokvel_decision", new[] { "join" } },
        { "windfall", new[] { "keep_cash" } },
        { "debit_order_check", new[] { "move_to_cash" } },
    };

    static Dictionary<string, string[]> ComfortLoan()
    {
        var prefs = new Dictionary<string, string[]>(Comfort);
        prefs["mashonisa_offer"] = new[] { "borrow_400" };
        return prefs;
    }

    // Column order of the spec 3.2 "Every profile" table.
    static readonly Style[] Styles =
    {
        new Style { Name = "saver", Prefs = Saver, Works = true, KeepsShift = false },
        new Style { Name = "middle", Prefs = Middle, Works = true, KeepsShift = false },
        new Style { Name = "comfort", Prefs = Comfort, Works = true, KeepsShift = false },
        new Style { Name = "comfort_loan", Prefs = ComfortLoan(), Works = true, KeepsShift = false },
        new Style { Name = "never_works", Prefs = Middle, Works = false, KeepsShift = false },
        new Style { Name = "always_works", Prefs = Saver, Works = true, KeepsShift = true },
    };

    static Style StyleNamed(string name) => Styles.First(s => s.Name == name);

    static readonly string[] Foci = { "food", "transport", "data_social", "home_family" };
    static readonly string[] Travels = { "taxi", "ehailing", "walk", "car" };

    /// <summary>Spec 3.2 "Every profile": end totals (cash + savings, night of Day 7) per profile, in Styles order.</summary>
    static readonly Dictionary<string, int[]> EndTotals = new Dictionary<string, int[]>
    {
        { "food/taxi", new[] { 854, 545, 615, 415, 350, 1255 } },
        { "food/ehailing", new[] { 790, 451, 615, 415, 350, 1255 } },
        { "food/walk", new[] { 854, 545, 615, 415, 350, 1255 } },
        { "food/car", new[] { 830, 509, 615, 415, 350, 1255 } },
        { "transport/taxi", new[] { 854, 545, 615, 415, 350, 1245 } },
        { "transport/ehailing", new[] { 790, 451, 615, 415, 350, 1231 } },
        { "transport/walk", new[] { 854, 545, 615, 415, 350, 1245 } },
        { "transport/car", new[] { 830, 509, 615, 415, 350, 1239 } },
        { "data_social/taxi", new[] { 854, 545, 615, 415, 350, 1255 } },
        { "data_social/ehailing", new[] { 790, 451, 615, 415, 350, 1255 } },
        { "data_social/walk", new[] { 854, 545, 615, 415, 350, 1255 } },
        { "data_social/car", new[] { 830, 509, 615, 415, 350, 1255 } },
        { "home_family/taxi", new[] { 704, 425, 205, 50, 200, 1225 } },
        { "home_family/ehailing", new[] { 640, 345, 205, 50, 200, 1211 } },
        { "home_family/walk", new[] { 704, 425, 205, 50, 200, 1225 } },
        { "home_family/car", new[] { 680, 395, 205, 50, 200, 1219 } },
    };

    // ------------------------------------------------------------------ the engine

    sealed class Run
    {
        public PlayerData Data;
        public string Where;
        public float MinCash;
        public int Shifts;
        public readonly List<int> NightsOwing = new List<int>();

        /// <summary>Energy before each day's shift, Days 1-7 (index 0 = Day 1); -1 = the style did not want to work.</summary>
        public readonly int[] EnergyAtShift = { -1, -1, -1, -1, -1, -1, -1 };

        public float Cash => Data.financialStats.cash;
        public float Savings => Data.financialStats.savings;
        public float Total => Cash + Savings;
    }

    static Run Play(string focus, string travel, Style style)
    {
        PlayerData d = PlayerData.CreateNew();
        d.characterName = "Thandeka";
        d.isCharacterCreated = true;
        d.hasMetMali = true;
        d.spendingProfile.focus = focus;
        d.spendingProfile.travel = travel;
        ChapterFlow.StartChapter(d, 1);

        var run = new Run { Data = d, Where = style.Name + " " + focus + "/" + travel, MinCash = d.financialStats.cash };
        Assert.Equal(ChapterConfig.StartCash, d.financialStats.cash, run.Where + " start cash");
        Assert.Equal(ChapterConfig.StartSavings, d.financialStats.savings, run.Where + " start savings");

        for (int i = 0; i < ChapterConfig.ChapterLength; i++)
        {
            int day = d.currentDay;
            Assert.True(day == i + 1, run.Where + ": expected Day " + (i + 1) + ", got Day " + day);
            string gate = ChapterSchedule.GateScenario(day, focus);
            Assert.True(!string.IsNullOrEmpty(gate), run.Where + ": no gate on Day " + day);

            // 1. The day's first scenario.
            if (ChapterSchedule.ActiveScenarioIds(d, true, focus, d.followUps).Contains(gate))
            {
                Resolve(run, gate, style, gate);
            }

            // 2. The shift, if the style works and it is open.
            if (style.Works)
            {
                ShiftState state = WorkRules.State(d, gate);
                if (state != ShiftState.Done)
                {
                    run.EnergyAtShift[day - 1] = (int)Math.Round(d.financialStats.energy);
                }

                if (state == ShiftState.Open)
                {
                    Assert.True(WorkRules.DoShift(d, gate) != null, run.Where + ": shift refused on Day " + day);
                    run.Shifts++;
                    Track(run);
                }
            }

            // 3. The other active scenarios, in ActiveScenarioIds order (a snapshot, like the script).
            foreach (string id in ChapterSchedule.ActiveScenarioIds(d, true, focus, d.followUps))
            {
                if (!d.IsScenarioCompleted(id))
                {
                    Resolve(run, id, style, gate);
                }
            }

            // 4. Sleep.
            NightResult night = DayCycle.EndDay(d);
            Assert.True(night != null, run.Where + ": EndDay refused on Day " + day);
            Assert.True(night.endedDay == day, run.Where + ": night ended Day " + night.endedDay);
            string broken = MoneyRecorder.CheckInvariant(d);
            Assert.True(broken == null, run.Where + " invariant after night " + day + ": " + broken);
            if (night.record.owedAtClose > MoneyRecorder.Tolerance)
            {
                run.NightsOwing.Add(day);
            }

            Track(run);
        }

        Assert.True(d.chapter.complete, run.Where + ": chapter not complete after Day 7");
        Assert.True(d.chapter.shiftsWorked == run.Shifts, run.Where + ": chapter.shiftsWorked " + d.chapter.shiftsWorked);
        GameEvents.ClearPendingMoneyChanged();
        return run;
    }

    static void Track(Run run)
    {
        run.MinCash = Math.Min(run.MinCash, run.Data.financialStats.cash);
    }

    static void Resolve(Run run, string scenarioId, Style style, string gate)
    {
        PlayerData d = run.Data;
        ScenarioDefinition s = ScenarioLibrary.Get(scenarioId, d.spendingProfile.focus, d.spendingProfile.travel);
        Assert.True(s != null, run.Where + ": no scenario " + scenarioId);
        string[] prefs = style.Prefs.TryGetValue(scenarioId, out string[] p) ? p : Array.Empty<string>();
        ScenarioChoice c = Pick(d, s, prefs, style.KeepsShift, gate);
        Assert.True(c != null, run.Where + ": stuck at " + scenarioId + " on Day " + d.currentDay + " (nothing affordable)");
        Assert.True(ScenarioOutcome.Apply(d, s, c), run.Where + ": " + scenarioId + "/" + c.choiceId + " refused on Day " + d.currentDay);
        Track(run);
    }

    /// <summary>The script's pick(): the style's preferences, the keep-the-shift rule at the gate, then authored order.</summary>
    static ScenarioChoice Pick(PlayerData d, ScenarioDefinition s, string[] prefs, bool keepsShift, string gate)
    {
        List<ScenarioChoice> available = s.choices
            .Where(c => ScenarioOutcome.Availability(d, c) == ChoiceAvailability.Available).ToList();
        if (available.Count == 0)
        {
            return null;
        }

        if (keepsShift && s.scenarioId == gate && !WorkRules.HasWorkedToday(d))
        {
            List<ScenarioChoice> keep = available
                .Where(c => d.financialStats.energy + c.energyDelta >= ChapterConfig.ShiftEnergyCost - MoneyRecorder.Tolerance)
                .ToList();
            ScenarioChoice preferred = keep.FirstOrDefault(c => prefs.Contains(c.choiceId));
            if (preferred != null)
            {
                return preferred;
            }

            if (keep.Count > 0)
            {
                // Least money over the week (out now + every later charge); ties keep authored order.
                ScenarioChoice best = keep[0];
                foreach (ScenarioChoice c in keep)
                {
                    if (MoneyCost(d, c) < MoneyCost(d, best) - MoneyRecorder.Tolerance)
                    {
                        best = c;
                    }
                }

                return best;
            }
        }

        foreach (string id in prefs)
        {
            ScenarioChoice match = available.FirstOrDefault(c => c.choiceId == id);
            if (match != null)
            {
                return match;
            }
        }

        return available[0];
    }

    static float MoneyCost(PlayerData d, ScenarioChoice c)
    {
        return -(c.cashDelta + c.savingsDelta) + ScenarioOutcome.LaterCount(d, c) * c.instalmentAmount;
    }

    static float Promised(PlayerData d) => ObligationLedger.Promised(d).Sum(x => x.Total);

    // ------------------------------------------------------------------ tests

    public static void TestEveryProfileEndTotalsMatchTheSpecTable()
    {
        var mismatches = new List<string>();
        foreach (string focus in Foci)
        {
            foreach (string travel in Travels)
            {
                string key = focus + "/" + travel;
                Assert.True(EndTotals.ContainsKey(key), "no reference row for " + key);
                int[] expected = EndTotals[key];
                for (int i = 0; i < Styles.Length; i++)
                {
                    Run run = Play(focus, travel, Styles[i]);
                    int total = (int)Math.Round(run.Total);
                    if (total != expected[i])
                    {
                        mismatches.Add($"{run.Where}: expected R{expected[i]}, got R{total}");
                    }
                }
            }
        }

        Assert.True(mismatches.Count == 0, mismatches.Count + " end total(s) differ from spec 3.2: " + string.Join("; ", mismatches));
    }

    public static void TestDefaultProfileDetailsMatchTheSpec()
    {
        // Spec 3.2 "Default profile" (food + taxi): end cash, end savings, lowest cash, nights still owing, shifts,
        // energy before each day's shift (-1 = no shift wanted), still owed and promised at payday.
        var rows = new[]
        {
            (style: "saver", cash: 384f, savings: 470f, minCash: 0f, owing: "3", shifts: 4,
             energy: "55,55,50,100,100,100,100", owed: 0f, promised: 200f),
            (style: "always_works", cash: 785f, savings: 470f, minCash: 200f, owing: "", shifts: 7,
             energy: "100,100,100,100,100,100,100", owed: 0f, promised: 200f),
            (style: "middle", cash: 345f, savings: 200f, minCash: 97f, owing: "", shifts: 7,
             energy: "100,100,95,100,100,100,100", owed: 0f, promised: 399f),
            (style: "never_works", cash: 0f, savings: 350f, minCash: 0f, owing: "3,4,5,6,7", shifts: 0,
             energy: "-1,-1,-1,-1,-1,-1,-1", owed: 455f, promised: 749f),
            (style: "comfort", cash: 614f, savings: 1f, minCash: 0f, owing: "3,4,5", shifts: 7,
             energy: "100,100,100,100,90,100,100", owed: 0f, promised: 749f),
            (style: "comfort_loan", cash: 414f, savings: 1f, minCash: 0f, owing: "3,4", shifts: 7,
             energy: "100,100,100,100,90,100,100", owed: 0f, promised: 749f),
        };

        foreach (var row in rows)
        {
            Run run = Play("food", "taxi", StyleNamed(row.style));
            Assert.Equal(row.cash, run.Cash, run.Where + " end cash");
            Assert.Equal(row.savings, run.Savings, run.Where + " end savings");
            Assert.Equal(row.minCash, run.MinCash, run.Where + " lowest cash");
            Assert.Equal(row.owing, string.Join(",", run.NightsOwing), run.Where + " nights still owing");
            Assert.True(run.Shifts == row.shifts, run.Where + " shifts: expected " + row.shifts + ", got " + run.Shifts);
            Assert.Equal(row.energy, string.Join(",", run.EnergyAtShift), run.Where + " energy before each shift");
            Assert.Equal(row.owed, ObligationLedger.TotalArrears(run.Data), run.Where + " still owed at payday");
            Assert.Equal(row.promised, Promised(run.Data), run.Where + " promised for payday");
            Assert.Equal(row.cash + row.savings - 1000f, run.Total - (ChapterConfig.StartCash + ChapterConfig.StartSavings),
                         run.Where + " week");
        }
    }

    public static void TestNoReferenceStyleEndsMoreThan255Up()
    {
        // Spec 3.2: "no style ends more than R255 up"; comfort ends below the start in every profile and
        // comfort + loan has a night with something still owed in every profile.
        float start = ChapterConfig.StartCash + ChapterConfig.StartSavings;
        foreach (string focus in Foci)
        {
            foreach (string travel in Travels)
            {
                foreach (Style style in Styles)
                {
                    Run run = Play(focus, travel, style);
                    Assert.True(run.Total - start <= 255f + MoneyRecorder.Tolerance, run.Where + " ends R" + run.Total);
                    if (style.Name == "comfort")
                    {
                        Assert.True(run.Total < start, run.Where + " is not tight: ends R" + run.Total);
                    }

                    if (style.Name == "comfort_loan")
                    {
                        Assert.True(run.NightsOwing.Count > 0, run.Where + " is never short");
                    }
                }
            }
        }
    }
}
