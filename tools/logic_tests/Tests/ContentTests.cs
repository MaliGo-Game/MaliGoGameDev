// WP3 tests: Chapter 1's scenario content, schedule, spots, place names and Mali's fixed lines
// (design spec 3.1-3.4, 4.2, 4.3 banned list, 5.2 glyph policy, section 8 WP3).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MaliGo.Data;
using MaliGo.Dialogue;
using MaliGo.Scenarios;

public static class ContentTests
{
    // ------------------------------------------------------------------ fixtures

    static readonly string[] Foci = { "food", "transport", "data_social", "home_family" };
    static readonly string[] Travels = { "taxi", "ehailing", "walk", "car" };

    static readonly string[] ScheduledIds =
    {
        "food_decision", "transport_decision", "data_runs_out", "credit_bnpl", "impulse_purchase", "taxi_fare_rise",
        "family_obligation", "group_chat_contribution", "emergency_expense", "mashonisa_offer", "stokvel_decision",
        "windfall", "debit_order_check",
    };

    static readonly string[] FollowUpIds = { "family_callback", "family_callback_full" };

    static readonly HashSet<string> Categories = new HashSet<string>
    {
        "Work", "Food", "Transport", "Phone & data", "Shopping", "Family", "Friends", "Home", "Bills", "Pay-later",
        "Loan", "Extra money", "Savings move",
    };

    // MaliText.Fill built-ins and the caller-supplied extras (spec 4.1).
    static readonly HashSet<string> KnownTokens = new HashSet<string>
    {
        "name", "0", "cash", "savings", "total", "goalName", "goalTarget", "goalLeft", "day", "paydayWhen", "energy",
        "plan", "friend", "label", "amt", "owed", "places", "count", "due", "repay", "laterDays", "gate", "focus",
    };

    static readonly HashSet<string> DeclineIds = new HashSet<string>
    {
        "credit_bnpl/leave_it", "impulse_purchase/walk_away", "impulse_purchase/to_savings",
        "family_callback/not_this_week", "family_callback_full/not_this_week", "group_chat_contribution/not_this_time",
        "mashonisa_offer/not_today", "stokvel_decision/not_for_now", "debit_order_check/cancel_gym",
    };

    static readonly HashSet<string> Needs = new HashSet<string>
    {
        "food_decision", "transport_decision", "data_runs_out", "taxi_fare_rise", "family_obligation",
        "family_callback", "family_callback_full", "emergency_expense",
    };

    static readonly string[] BannedWords =
    {
        "good", "nice", "great", "smart", "careful", "should", "mistake", "well done", "bad", "wrong", "proud", "wise",
        "smart move", "responsible", "too much", "overspent", "treat yourself", "unfortunately", "risk",
    };

    static readonly string[] Brands =
    {
        "Uber", "Bolt", "Shoprite", "Checkers", "Pick n Pay", "Spar", "Woolworths", "KFC", "Nando's", "Steers",
        "Vodacom", "MTN", "Telkom", "Cell C", "Absa", "Capitec", "FNB", "Nedbank", "Standard Bank", "Takealot", "PEP",
        "Mr Price", "Engen", "Shell", "Sasol", "Caltex", "Virgin Active", "Planet Fitness",
    };

    static ScenarioDefinition Def(string id, string focus = "food", string travel = "taxi")
    {
        ScenarioDefinition s = ScenarioLibrary.Get(id, focus, travel);
        Assert.True(s != null, "no scenario " + id + " for " + focus + "/" + travel);
        return s;
    }

    static ScenarioChoice ChoiceOf(ScenarioDefinition s, string choiceId)
    {
        ScenarioChoice c = s.choices.FirstOrDefault(x => x.choiceId == choiceId);
        Assert.True(c != null, s.scenarioId + " has no choice " + choiceId);
        return c;
    }

    static PlayerData Player(int day, params string[] completed)
    {
        return new PlayerData { currentDay = day, completedScenarioIds = completed };
    }

    /// <summary>Every player-facing string of a scenario, with a description of where it is.</summary>
    static IEnumerable<(string where, string text)> Strings(ScenarioDefinition s)
    {
        string id = s.scenarioId;
        yield return (id + ".title", s.title);
        yield return (id + ".situation", s.description);
        yield return (id + ".intro", s.introDialogue);
        yield return (id + ".prompt", s.promptText);
        yield return (id + ".place", s.placeLabel);
        yield return (id + ".gateNoun", s.gateNoun);
        foreach (ScenarioChoice c in s.choices)
        {
            string w = id + "/" + c.choiceId;
            yield return (w + ".label", c.label);
            yield return (w + ".ledger", c.ledgerLabel);
            yield return (w + ".instalmentLabel", c.instalmentLabel);
            yield return (w + ".instalmentShort", c.instalmentShortLabel);
            yield return (w + ".reaction", c.maliReactionLine);
            yield return (w + ".noLater", c.maliReactionNoLater);
            yield return (w + ".followUpLater", c.followUpLaterText);
            yield return (w + ".description", c.description);
        }
    }

    /// <summary>Mali's lines and the situation texts of a scenario (the banned-word scope).</summary>
    static IEnumerable<(string where, string text)> MaliLines(ScenarioDefinition s)
    {
        yield return (s.scenarioId + ".situation", s.description);
        yield return (s.scenarioId + ".intro", s.introDialogue);
        foreach (ScenarioChoice c in s.choices)
        {
            yield return (s.scenarioId + "/" + c.choiceId + ".reaction", c.maliReactionLine);
            yield return (s.scenarioId + "/" + c.choiceId + ".noLater", c.maliReactionNoLater);
        }
    }

    static IEnumerable<(string where, string text)> ScheduleStrings(string focus, string travel)
    {
        for (int day = 1; day <= 7; day++)
        {
            yield return (focus + " morning " + day, ChapterSchedule.MorningLine(day, focus));
            yield return (focus + " teaser " + day, ChapterSchedule.TeaserForNight(day, focus));
        }

        foreach (string id in FollowUpIds)
        {
            yield return ("follow-up teaser " + id, ChapterSchedule.FollowUpTeaser(id));
        }

        foreach (string spot in ChapterSchedule.AllSpots)
        {
            yield return (focus + "/" + travel + " " + spot + " name", ChapterSchedule.SpotPlaceName(spot, focus, travel));
            yield return (focus + "/" + travel + " " + spot + " label", ChapterSchedule.SpotPlaceLabel(spot, focus, travel));
        }

        foreach (string place in ChapterSchedule.WeekPlaces(focus, travel))
        {
            yield return (focus + "/" + travel + " week place", place);
        }
    }

    static IEnumerable<(string where, string text)> DialogueStrings()
    {
        foreach (MaliDialogueEntry e in MaliDialogueLibrary.AllEntries)
        {
            yield return ("dialogue " + e.dialogueId, e.line);
        }
    }

    static IEnumerable<(string where, string text)> AllPlayerStrings(string focus, string travel)
    {
        foreach (string id in ScenarioLibrary.AllIds)
        {
            foreach (var s in Strings(Def(id, focus, travel)))
            {
                yield return s;
            }
        }

        foreach (var s in ScheduleStrings(focus, travel))
        {
            yield return s;
        }

        foreach (var s in DialogueStrings())
        {
            yield return s;
        }
    }

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

    static bool GlyphAllowed(char c)
    {
        if (c >= 0x20 && c <= 0x7E)
        {
            return true;
        }

        if ("−—–‘’“”…·×•".IndexOf(c) >= 0)
        {
            return true;
        }

        return c >= 0x00C0 && c <= 0x017F && c != 0x00D7 && c != 0x00F7 && char.IsLetter(c);
    }

    // ------------------------------------------------------------------ scenarios

    public static void TestFifteenScenarios()
    {
        Assert.True(ScenarioLibrary.AllIds.Count == 15, "AllIds has " + ScenarioLibrary.AllIds.Count + " ids");
        var expected = new HashSet<string>(ScheduledIds.Concat(FollowUpIds));
        Assert.True(expected.SetEquals(ScenarioLibrary.AllIds), "AllIds is not the 13 scheduled + 2 follow-up ids");
        foreach (string id in ScenarioLibrary.AllIds)
        {
            ScenarioDefinition s = Def(id);
            Assert.Equal(id, s.scenarioId, "scenarioId of " + id);
            Assert.True(ReferenceEquals(s, ScenarioLibrary.GetById(id)), "GetById(" + id + ") is not Get(food, taxi)");
            Assert.True(s.isFollowUp == FollowUpIds.Contains(id), id + " isFollowUp");
            Assert.True(!string.IsNullOrEmpty(s.title) && !string.IsNullOrEmpty(s.description)
                        && !string.IsNullOrEmpty(s.introDialogue) && !string.IsNullOrEmpty(s.promptText)
                        && !string.IsNullOrEmpty(s.placeLabel) && !string.IsNullOrEmpty(s.promptIcon),
                id + " has an empty title, situation, intro, prompt, place or icon");
            Assert.Equal(ChapterSchedule.SpotFor(id), s.spotId, id + " spotId");
            Assert.True(s.isFollowUp || !string.IsNullOrEmpty(s.gateNoun), id + " has no gate noun");
            Assert.True(s.requiredLifeChapters.Length == 0, id + " requires a life chapter");
        }

        Assert.True(ScenarioLibrary.GetById("nope") == null && ScenarioLibrary.GetById(null) == null,
            "unknown ids must give null");
        Assert.True(ReferenceEquals(ScenarioLibrary.Get("windfall", "FOOD ", "bus"), ScenarioLibrary.GetById("windfall")),
            "unknown focus/travel must play food/taxi");
    }

    public static void TestChoiceRules()
    {
        foreach (var (focus, travel) in Profiles())
        {
            foreach (string id in ScenarioLibrary.AllIds)
            {
                ScenarioDefinition s = Def(id, focus, travel);
                Assert.True(s.choices.Length >= 2 && s.choices.Length <= 4, id + " has " + s.choices.Length + " choices");
                Assert.True(s.choices.Select(c => c.choiceId).Distinct().Count() == s.choices.Length,
                    id + " has duplicate choice ids");
                Assert.True(s.choices.Any(c => c.cashDelta >= 0f && c.savingsDelta >= 0f),
                    id + " breaks the always-available rule");
                Assert.True(Categories.Contains(s.moneyCategory), id + " category '" + s.moneyCategory + "'");
                int named = 0;
                foreach (ScenarioChoice c in s.choices)
                {
                    string w = id + "/" + c.choiceId;
                    Assert.Equal(5f, c.financialXpDelta, w + " xp");
                    Assert.True(string.IsNullOrEmpty(c.description), w + " description must be empty (retired)");
                    Assert.True(c.label.Length <= 44, w + " label is " + c.label.Length + " chars");
                    Assert.True(c.ledgerLabel.Length <= 28, w + " ledger is " + c.ledgerLabel.Length + " chars");
                    if (Math.Abs(c.cashDelta) > 0.005f || Math.Abs(c.savingsDelta) > 0.005f)
                    {
                        Assert.True(c.ledgerLabel.Length > 0, w + " moves money but has no ledger label");
                    }

                    Assert.True(!string.IsNullOrEmpty(c.maliReactionLine), w + " has no reaction");
                    Assert.True(c.maliReactionLine.Length <= 150, w + " reaction is " + c.maliReactionLine.Length);
                    Assert.True(c.maliReactionNoLater.Length <= 150, w + " no-later reaction is too long");
                    if (c.maliReactionLine.Contains("{name}") || c.maliReactionNoLater.Contains("{name}"))
                    {
                        named++;
                    }

                    if (c.instalmentCount > 0)
                    {
                        Assert.True(c.instalmentAmount > 0f && c.instalmentIntervalDays >= 1, w + " instalment shape");
                        Assert.True(Categories.Contains(c.instalmentCategory), w + " instalment category");
                        Assert.True(new[] { "instalment", "loan", "commitment", "repeat" }.Contains(c.instalmentKind),
                            w + " instalment kind '" + c.instalmentKind + "'");
                        Assert.True(c.instalmentLabel.Length > 0 && c.instalmentShortLabel.Length > 0
                                    && c.instalmentShortLabel.Length <= 10, w + " instalment labels");
                    }

                    if (c.instalmentLastDueDay > 0)
                    {
                        Assert.True(c.maliReactionNoLater.Length > 0, w + " needs a no-later reaction");
                    }

                    if (!string.IsNullOrEmpty(c.followUpScenarioId))
                    {
                        Assert.True(FollowUpIds.Contains(c.followUpScenarioId) && c.followUpAfterDays > 0
                                    && c.followUpLaterText.Length > 0, w + " follow-up shape");
                    }
                }

                Assert.True(named <= 1, id + " uses {name} in " + named + " reactions");
                Assert.True(s.description.Length <= 190, id + " situation is " + s.description.Length + " chars");
            }
        }
    }

    public static void TestContentNumbers()
    {
        // Spot checks against the spec tables (3.4).
        Assert.Equal(-50f, ChoiceOf(Def("food_decision"), "kota").cashDelta, "kota");
        Assert.Equal(-45f, ChoiceOf(Def("food_decision"), "skip_lunch").energyDelta, "skip lunch energy");
        ScenarioChoice bnpl = ChoiceOf(Def("credit_bnpl"), "pay_later");
        Assert.True(bnpl.instalmentCount == 2 && bnpl.instalmentIntervalDays == 2 && bnpl.instalmentFirstDueDay == 0,
            "pay-later schedule");
        Assert.Equal(130f, bnpl.instalmentAmount, "pay-later amount");
        ScenarioChoice bundle = ChoiceOf(Def("data_runs_out"), "day_bundle");
        Assert.True(bundle.instalmentCount == 2 && bundle.instalmentIntervalDays == 1 && bundle.instalmentLastDueDay == 7
                    && bundle.instalmentKind == "repeat", "day bundle schedule");
        ScenarioChoice toSavings = ChoiceOf(Def("impulse_purchase"), "to_savings");
        Assert.True(toSavings.cashDelta == -120f && toSavings.savingsDelta == 120f, "hoodie money moves, not created");
        ScenarioChoice cold = ChoiceOf(Def("emergency_expense"), "cold_showers");
        Assert.True(cold.instalmentFirstDueDay == 8 && cold.instalmentAmount == 350f && cold.instalmentKind == "commitment",
            "cold showers promise");
        ScenarioChoice loan = ChoiceOf(Def("mashonisa_offer"), "borrow_400");
        Assert.True(loan.cashDelta == 400f && loan.instalmentAmount == 600f && loan.instalmentKind == "loan"
                    && loan.instalmentIntervalDays == 2, "Bra K loan");
        Assert.Equal("family_callback", ChoiceOf(Def("family_obligation"), "send_part").followUpScenarioId,
            "send_part follow-up");
        Assert.Equal("family_callback_full", ChoiceOf(Def("family_obligation"), "cant_this_week").followUpScenarioId,
            "cant_this_week follow-up");
        Assert.True(ChoiceOf(Def("family_obligation"), "send_part").followUpAfterDays == 2, "follow-up after 2 days");
        Assert.True(new ScenarioChoice().instalmentIntervalDays == 2, "instalmentIntervalDays default is 2");
    }

    public static void TestEveryChoiceTaggedTheSameXp()
    {
        int choices = 0;
        foreach (string id in ScenarioLibrary.AllIds)
        {
            foreach (ScenarioChoice c in Def(id).choices)
            {
                choices++;
                Assert.Equal(5f, c.financialXpDelta, id + "/" + c.choiceId);
            }
        }

        Assert.True(choices >= 30, "only " + choices + " choices");
    }

    // ------------------------------------------------------------------ strings

    public static void TestGlyphWhitelist()
    {
        foreach (var (focus, travel) in Profiles())
        {
            foreach (var (where, text) in AllPlayerStrings(focus, travel))
            {
                if (text == null)
                {
                    continue;
                }

                foreach (char c in text)
                {
                    Assert.True(GlyphAllowed(c), where + " has U+" + ((int)c).ToString("X4") + " in \"" + text + "\"");
                }
            }
        }
    }

    public static void TestBannedWordsAbsent()
    {
        var patterns = BannedWords
            .Select(w => new Regex(@"\b" + Regex.Escape(w).Replace(@"\ ", @"\s+") + @"\b", RegexOptions.IgnoreCase))
            .ToArray();
        foreach (var (focus, travel) in Profiles())
        {
            var lines = new List<(string where, string text)>();
            foreach (string id in ScenarioLibrary.AllIds)
            {
                lines.AddRange(MaliLines(Def(id, focus, travel)));
            }

            for (int day = 1; day <= 7; day++)
            {
                lines.Add((focus + " morning " + day, ChapterSchedule.MorningLine(day, focus)));
                lines.Add((focus + " teaser " + day, ChapterSchedule.TeaserForNight(day, focus)));
            }

            lines.AddRange(DialogueStrings());
            foreach (var (where, text) in lines)
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                Assert.True(text.IndexOf('!') < 0, where + " contains \"!\"");
                foreach (Regex p in patterns)
                {
                    Assert.True(!p.IsMatch(text), where + " contains banned \"" + p + "\": " + text);
                }
            }
        }
    }

    public static void TestNoBrandNames()
    {
        var patterns = Brands
            .Select(b => new Regex(@"(?<![A-Za-z])" + Regex.Escape(b).Replace(@"\ ", @"\s+") + @"(?![A-Za-z])",
                RegexOptions.IgnoreCase))
            .ToArray();
        foreach (var (focus, travel) in Profiles())
        {
            foreach (var (where, text) in AllPlayerStrings(focus, travel))
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                foreach (Regex p in patterns)
                {
                    Assert.True(!p.IsMatch(text), where + " names a brand (" + p + "): " + text);
                }
            }
        }
    }

    public static void TestOnlyKnownTokensAndNoPlaceholders()
    {
        var token = new Regex(@"\{([^{}]*)\}");
        foreach (var (focus, travel) in Profiles())
        {
            foreach (var (where, text) in AllPlayerStrings(focus, travel))
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                foreach (Match m in token.Matches(text))
                {
                    Assert.True(KnownTokens.Contains(m.Groups[1].Value), where + " has unknown token " + m.Value);
                }

                foreach (string placeholder in new[] { "[place]", "[Place]", "[trip]", "[daily]" })
                {
                    Assert.True(text.IndexOf(placeholder, StringComparison.Ordinal) < 0,
                        where + " still has " + placeholder + ": " + text);
                }
            }
        }
    }

    public static void TestDialogueLibraryHasOnlyFirstMeeting()
    {
        Assert.True(MaliDialogueLibrary.AllEntries.Count == 1, "AllEntries has " + MaliDialogueLibrary.AllEntries.Count);
        MaliDialogueEntry e = MaliDialogueLibrary.AllEntries[0];
        Assert.Equal("first_meeting", e.dialogueId, "the one entry");
        Assert.True(e.triggerType == MaliDialogueTriggerType.FirstMeeting, "first_meeting trigger type");
        Assert.Equal("Hi {name}, I'm Mali. I'll be with you all week, all the way to payday.", e.line, "first meeting line");
        Assert.True(MaliDialogueLibrary.FindById("default_greeting_student") == null, "student greeting deleted");
    }

    // ------------------------------------------------------------------ schedule

    public static void TestScheduleEveryFocus()
    {
        foreach (string focus in Foci)
        {
            var seen = new List<string>();
            for (int day = 1; day <= 7; day++)
            {
                IReadOnlyList<string> ids = ChapterSchedule.ScenariosForDay(day, focus);
                Assert.True(ids.Count >= 1, focus + " Day " + day + " is empty");
                Assert.Equal(ids[0], ChapterSchedule.GateScenario(day, focus), focus + " gate of Day " + day);
                foreach (string id in ids)
                {
                    seen.Add(id);
                    Assert.True(ChapterSchedule.ScheduledDay(id, focus) == day, focus + " ScheduledDay(" + id + ")");
                }

                Assert.True(!string.IsNullOrEmpty(ChapterSchedule.MorningLine(day, focus)), focus + " morning " + day);
                Assert.True(!string.IsNullOrEmpty(ChapterSchedule.TeaserForNight(day, focus)), focus + " teaser " + day);
            }

            Assert.True(seen.Count == 13 && new HashSet<string>(seen).SetEquals(ScheduledIds),
                focus + ": the 13 scheduled ids must each appear exactly once");
            foreach (string id in FollowUpIds)
            {
                Assert.True(!seen.Contains(id) && ChapterSchedule.ScheduledDay(id, focus) == 0,
                    focus + " schedules follow-up " + id);
            }

            Assert.Equal("data_runs_out", ChapterSchedule.ScenariosForDay(2, focus)[0], focus + " Day 2 gate");
            Assert.Equal("taxi_fare_rise", ChapterSchedule.ScenariosForDay(3, focus)[0], focus + " Day 3 gate");
            Assert.True(ChapterSchedule.ScheduledDay("mashonisa_offer", focus) == 5, focus + " Bra K on Day 5");
            IReadOnlyList<string> day7 = ChapterSchedule.ScenariosForDay(7, focus);
            Assert.True(day7.Count == 1 && day7[0] == "debit_order_check", focus + " Day 7 is the debit order alone");

            // The gates of Days 1-3 are needs with a free option that uses at least 45 energy.
            for (int day = 1; day <= 3; day++)
            {
                string gate = ChapterSchedule.GateScenario(day, focus);
                Assert.True(Needs.Contains(gate), focus + " Day " + day + " gate " + gate + " is not a need");
                Assert.True(Def(gate, focus).choices.Any(c => c.cashDelta >= 0f && c.savingsDelta >= 0f
                                                              && c.energyDelta <= -45f),
                    focus + " Day " + day + " gate has no free option using >= 45 energy");
            }

            Assert.True(ChapterSchedule.GateScenario(0, focus) == null && ChapterSchedule.GateScenario(8, focus) == null,
                "no gate outside Days 1-7");
            Assert.Equal("Tomorrow is payday.", ChapterSchedule.TeaserForNight(7, focus), focus + " teaser after Day 7");
        }

        Assert.True(ChapterSchedule.ScenariosForDay(1, "food").Contains("food_decision"), "food Day 1");
        Assert.True(ChapterSchedule.ScenariosForDay(1, "transport").Contains("transport_decision"), "transport Day 1");
        Assert.True(ChapterSchedule.ScenariosForDay(1, "data_social").Contains("group_chat_contribution"),
            "data_social Day 1");
        Assert.True(ChapterSchedule.ScenariosForDay(1, "home_family").Contains("family_obligation"), "home_family Day 1");
        Assert.Equal("transport_decision", ChapterSchedule.GateScenario(1, "transport"), "transport Day 1 gate");
        Assert.Equal(ChapterSchedule.GateScenario(4, "food"), ChapterSchedule.GateScenario(4, "bus"),
            "unknown focus plays food");
        Assert.Equal("Day 1, {name}. Lunch first, then the shift. A trip across town too.",
            ChapterSchedule.MorningLine(1, "food"), "default Day 1 morning line");
        Assert.Equal("Day 5. There's a trip across town, and Bra K wants a word.",
            ChapterSchedule.MorningLine(5, "home_family"), "home_family Day 5 morning line");
        Assert.Equal("Tomorrow: a call from home, and a trip across town.",
            ChapterSchedule.TeaserForNight(3, "data_social"), "data_social teaser for Day 4");
        foreach (string id in FollowUpIds)
        {
            Assert.Equal("Tomorrow: your aunt calls back.", ChapterSchedule.FollowUpTeaser(id), "follow-up teaser " + id);
        }

        Assert.True(ChapterSchedule.FollowUpTeaser("windfall") == null, "no follow-up teaser for a scheduled scenario");
    }

    public static void TestSpotQueuesMatchSection33()
    {
        var expected = new Dictionary<string, string[]>
        {
            { "CORNER", new[] { "food_decision", "data_runs_out", "group_chat_contribution" } },
            { "TAXI", new[] { "transport_decision", "taxi_fare_rise" } },
            { "HUB", new[] { "credit_bnpl", "stokvel_decision" } },
            { "SHOPFRONT", new[] { "impulse_purchase" } },
            { "GATE", new[] { "family_obligation", "emergency_expense", "debit_order_check" } },
            { "EAST", new[] { "mashonisa_offer", "windfall" } },
        };
        List<string> all = ChapterSchedule.ActiveScenarioIds(Player(7), false, "food", Array.Empty<string>());
        Assert.True(all.Count == 13, "schedule off: all 13 scheduled scenarios are active, got " + all.Count);
        foreach (var pair in expected)
        {
            string[] queue = all.Where(id => ChapterSchedule.SpotFor(id) == pair.Key).ToArray();
            Assert.Equal(string.Join(",", pair.Value), string.Join(",", queue), "queue at " + pair.Key);
        }

        foreach (string id in FollowUpIds)
        {
            Assert.Equal("GATE", ChapterSchedule.SpotFor(id), "spot of " + id);
        }

        // Today's gate is first in its spot's queue, ahead of any carry-over.
        Assert.Equal("data_runs_out",
            ChapterSchedule.ActiveScenarioAtSpot(Player(2), "CORNER", true, "food", Array.Empty<string>()),
            "food_decision carried to Day 2 waits behind data_runs_out");
        Assert.Equal("taxi_fare_rise",
            ChapterSchedule.ActiveScenarioAtSpot(Player(3), "TAXI", true, "food", Array.Empty<string>()),
            "transport_decision carried to Day 3 waits behind taxi_fare_rise");
        Assert.Equal("food_decision",
            ChapterSchedule.ActiveScenarioAtSpot(Player(2, "data_runs_out"), "CORNER", true, "food", Array.Empty<string>()),
            "the carry-over appears once the gate is done");
        Assert.True(ChapterSchedule.ActiveScenarioAtSpot(Player(1), "HUB", true, "food", Array.Empty<string>()) == null,
            "nothing at HUB on Day 1");
        List<string> day1 = ChapterSchedule.ActiveScenarioIds(Player(1), true, "food", Array.Empty<string>());
        Assert.Equal("food_decision,transport_decision", string.Join(",", day1), "Day 1 active");
        List<string> day4 = ChapterSchedule.ActiveScenarioIds(Player(4, "food_decision"), true, "food", null);
        Assert.Equal("family_obligation", day4[0], "Day 4 gate first");
        Assert.True(ChapterSchedule.ActiveScenarioIds(null, true, "food", null).Count == 0, "null data");
    }

    public static void TestFollowUpActivation()
    {
        string key = ChapterSchedule.FollowUpKey("family_callback", 6);
        Assert.Equal("family_callback@6", key, "FollowUpKey");
        Assert.True(ChapterSchedule.TryParseFollowUp(key, out string id, out int day) && id == "family_callback" && day == 6,
            "TryParseFollowUp round trip");
        foreach (string fid in FollowUpIds)
        {
            for (int d = 1; d <= 7; d++)
            {
                Assert.True(ChapterSchedule.TryParseFollowUp(ChapterSchedule.FollowUpKey(fid, d), out string i2, out int d2)
                            && i2 == fid && d2 == d, "round trip " + fid + "@" + d);
            }
        }

        foreach (string bad in new[] { null, "", "family_callback", "@3", "family_callback@", "family_callback@x",
                                       "family_callback@0", "family_callback@-1" })
        {
            Assert.True(!ChapterSchedule.TryParseFollowUp(bad, out _, out _), "TryParseFollowUp accepted '" + bad + "'");
        }

        string[] followUps = { "family_callback@6" };
        string[] doneBeforeDay6 =
        {
            "food_decision", "transport_decision", "data_runs_out", "credit_bnpl", "taxi_fare_rise", "impulse_purchase",
            "family_obligation", "group_chat_contribution", "mashonisa_offer",
        };

        // Not before its day.
        Assert.True(!ChapterSchedule.ActiveScenarioIds(Player(5, doneBeforeDay6), true, "food", followUps)
                .Contains("family_callback"), "follow-up active before its day");

        // On its day, after the scheduled scenarios at its spot (the geyser carried over from Day 5).
        List<string> day6 = ChapterSchedule.ActiveScenarioIds(Player(6, doneBeforeDay6), true, "food", followUps);
        Assert.True(day6.Contains("family_callback"), "follow-up not active on its day");
        Assert.True(day6.IndexOf("emergency_expense") < day6.IndexOf("family_callback"),
            "follow-up must come after the scheduled scenarios at its spot");
        Assert.Equal("emergency_expense",
            ChapterSchedule.ActiveScenarioAtSpot(Player(6, doneBeforeDay6), "GATE", true, "food", followUps),
            "GATE head on Day 6");
        string[] doneWithGeyser = doneBeforeDay6.Concat(new[] { "emergency_expense" }).ToArray();
        Assert.Equal("family_callback",
            ChapterSchedule.ActiveScenarioAtSpot(Player(6, doneWithGeyser), "GATE", true, "food", followUps),
            "follow-up heads GATE once the geyser is done");

        // Still active on a later day, behind today's gate at the same spot.
        List<string> day7 = ChapterSchedule.ActiveScenarioIds(Player(7, doneWithGeyser), true, "food", followUps);
        Assert.True(day7.IndexOf("debit_order_check") == 0 && day7.Contains("family_callback"),
            "Day 7: the gate first, the follow-up still active");

        // Completed follow-ups and follow-ups not in followUps are never active.
        string[] doneAll = doneWithGeyser.Concat(new[] { "family_callback" }).ToArray();
        Assert.True(!ChapterSchedule.ActiveScenarioIds(Player(7, doneAll), true, "food", followUps)
                .Contains("family_callback"), "completed follow-up active");
        foreach (string focus in Foci)
        {
            for (int d = 1; d <= 7; d++)
            {
                foreach (bool schedule in new[] { true, false })
                {
                    List<string> active = ChapterSchedule.ActiveScenarioIds(Player(d), schedule, focus, Array.Empty<string>());
                    Assert.True(!active.Contains("family_callback") && !active.Contains("family_callback_full"),
                        focus + " Day " + d + ": a follow-up is active without being in followUps");
                    List<string> withOther = ChapterSchedule.ActiveScenarioIds(Player(d), schedule, focus,
                        new[] { "family_callback@1", "windfall@1", "bogus" });
                    Assert.True(!withOther.Contains("family_callback_full"),
                        focus + " Day " + d + ": family_callback_full active without its key");
                    Assert.True(withOther.Count(x => x == "windfall") <= 1, "a scheduled id passed as a follow-up key");
                }
            }
        }
    }

    // ------------------------------------------------------------------ places and travel

    public static void TestPlacesEveryProfile()
    {
        var weekPlaces = new Dictionary<string, string[]>
        {
            { "food", new[] { "Kota shop", "[taxi]", "Shops by the bank" } },
            { "transport", new[] { "[taxi]", "Corner shop", "Shops by the bank" } },
            { "data_social", new[] { "Corner shop", "Shops by the bank", "[taxi]" } },
            { "home_family", new[] { "Spaza shop", "Home", "Shops by the bank" } },
        };
        var taxiLabel = new Dictionary<string, string>
        {
            { "taxi", "Taxi rank" }, { "ehailing", "Pick-up point" }, { "walk", "Taxi rank" }, { "car", "Petrol station" },
        };
        foreach (var (focus, travel) in Profiles())
        {
            foreach (string spot in ChapterSchedule.AllSpots)
            {
                Assert.True(!string.IsNullOrEmpty(ChapterSchedule.SpotPlaceName(spot, focus, travel))
                            && !string.IsNullOrEmpty(ChapterSchedule.SpotPlaceLabel(spot, focus, travel)),
                    focus + "/" + travel + " " + spot + " has no place name or label");
            }

            string[] expected = weekPlaces[focus].Select(p => p == "[taxi]" ? taxiLabel[travel] : p).ToArray();
            Assert.Equal(string.Join(" · ", expected), string.Join(" · ", ChapterSchedule.WeekPlaces(focus, travel)),
                "WeekPlaces " + focus + "/" + travel);
        }

        Assert.Equal("the kota shop", ChapterSchedule.SpotPlaceName("CORNER", "food", "taxi"), "food CORNER");
        Assert.Equal("the spaza shop", ChapterSchedule.SpotPlaceName("CORNER", "home_family", "car"), "home CORNER");
        Assert.Equal("Clothing shop", ChapterSchedule.SpotPlaceLabel("SHOPFRONT", "data_social", "walk"), "data SHOPFRONT");
        Assert.Equal("the pick-up point", ChapterSchedule.SpotPlaceName("TAXI", "food", "ehailing"), "ehailing TAXI");
        Assert.Equal("your gate", ChapterSchedule.SpotPlaceName("GATE", "transport", "car"), "GATE");
        Assert.Equal("Lunch at the kota shop", Def("food_decision").promptText, "food prompt");
        Assert.Equal("Kota shop", Def("food_decision").placeLabel, "food place");
        Assert.Equal("Clothing shop", Def("impulse_purchase", "data_social", "taxi").promptText, "hoodie prompt, data_social");
        Assert.Equal("News at the petrol station", Def("taxi_fare_rise", "food", "car").promptText, "fare prompt, car");
        Assert.Equal("Petrol went up", Def("taxi_fare_rise", "food", "car").title, "fare title, car");
    }

    public static void TestTravelModes()
    {
        foreach (string travel in Travels)
        {
            foreach (string focus in Foci)
            {
                foreach (string id in ScenarioLibrary.AllIds)
                {
                    var ids = Def(id, focus, travel).choices.Select(c => c.choiceId).OrderBy(x => x, StringComparer.Ordinal);
                    var defaults = Def(id).choices.Select(c => c.choiceId).OrderBy(x => x, StringComparer.Ordinal);
                    Assert.True(ids.SequenceEqual(defaults), id + " choice ids differ for " + focus + "/" + travel);
                }

                ScenarioDefinition trip = Def("transport_decision", focus, travel);
                ScenarioChoice usual = ChoiceOf(trip, "usual");
                Assert.True(-usual.cashDelta >= 30f && -usual.cashDelta <= 50f, travel + " trip R" + (-usual.cashDelta));
                Assert.Equal(-90f, ChoiceOf(trip, "ride").cashDelta, travel + " ride");
                Assert.Equal(-45f, ChoiceOf(trip, "walk").energyDelta, travel + " walk energy");
                Assert.True((trip.choices[0].choiceId == "walk") == (travel == "walk"),
                    travel + ": the walk option is first only for walk");

                ScenarioDefinition fare = Def("taxi_fare_rise", focus, travel);
                ScenarioChoice pay = ChoiceOf(fare, "pay_new_fare");
                ScenarioChoice walkToday = ChoiceOf(fare, "walk_today");
                Assert.True(-pay.cashDelta >= 34f && -pay.cashDelta <= 50f, travel + " daily R" + (-pay.cashDelta));
                Assert.Equal(-pay.cashDelta, pay.instalmentAmount, travel + " daily instalment = today's fare");
                Assert.Equal(pay.instalmentAmount, walkToday.instalmentAmount, travel + " walk today then the fare");
                Assert.True(pay.instalmentCount == 4 && pay.instalmentIntervalDays == 1 && pay.instalmentLastDueDay == 7
                            && pay.instalmentKind == "repeat", travel + " daily schedule");
                Assert.Equal(-120f, ChoiceOf(fare, "lift_club").cashDelta, travel + " lift club");
                Assert.Equal(-50f, walkToday.energyDelta, travel + " walk today energy");
            }
        }

        Assert.Equal(-30f, ChoiceOf(Def("transport_decision", "food", "taxi"), "usual").cashDelta, "taxi trip");
        Assert.Equal(-44f, ChoiceOf(Def("transport_decision", "food", "ehailing"), "usual").cashDelta, "ehailing trip");
        Assert.Equal(-36f, ChoiceOf(Def("transport_decision", "food", "car"), "usual").cashDelta, "car trip");
        Assert.Equal(-40f, ChoiceOf(Def("taxi_fare_rise", "food", "car"), "pay_new_fare").cashDelta, "car daily");
        Assert.Equal("R50 today, {name}. That's the last trip before payday.",
            ChoiceOf(Def("taxi_fare_rise", "food", "ehailing"), "pay_new_fare").maliReactionNoLater, "ehailing no-later");
    }

    // ------------------------------------------------------------------ fair choices (3.2, mirrors tools/sim_chapter.py)

    static int PaymentCount(ScenarioChoice c, int day)
    {
        if (c.instalmentCount <= 0)
        {
            return 0;
        }

        int interval = Math.Max(1, c.instalmentIntervalDays);
        int first = c.instalmentFirstDueDay > 0 ? c.instalmentFirstDueDay : day + interval;
        int count = c.instalmentCount;
        if (c.instalmentLastDueDay > 0)
        {
            count = first > c.instalmentLastDueDay ? 0 : Math.Min(count, (c.instalmentLastDueDay - first) / interval + 1);
        }

        return Math.Max(0, count);
    }

    public static void TestFairChoices()
    {
        foreach (var (focus, travel) in Profiles())
        {
            var dayOf = new Dictionary<string, int>();
            foreach (string id in ScheduledIds)
            {
                dayOf[id] = ChapterSchedule.ScheduledDay(id, focus);
            }

            dayOf["family_callback"] = dayOf["family_obligation"] + 2;
            dayOf["family_callback_full"] = dayOf["family_obligation"] + 2;

            foreach (string id in ScenarioLibrary.AllIds)
            {
                ScenarioDefinition s = Def(id, focus, travel);
                int day = dayOf[id];
                var rows = s.choices.Select(c =>
                {
                    int n = PaymentCount(c, day);
                    float later = n * c.instalmentAmount;
                    float money = -(c.cashDelta + c.savingsDelta) + later;
                    int entries = (later > 0f ? 1 : 0) + (string.IsNullOrEmpty(c.followUpScenarioId) ? 0 : 1);
                    return (c, money, cash: c.cashDelta, sav: c.savingsDelta, energy: -c.energyDelta, entries);
                }).ToList();

                foreach (var r in rows)
                {
                    if (DeclineIds.Contains(id + "/" + r.c.choiceId))
                    {
                        continue;
                    }

                    var others = rows.Where(o => !ReferenceEquals(o.c, r.c)).ToList();
                    bool dominates = others.Count > 0 && others.All(o =>
                        r.money <= o.money && r.cash >= o.cash && r.sav >= o.sav && r.energy <= o.energy
                        && r.entries <= o.entries);
                    Assert.True(!dominates, focus + "/" + travel + " " + id + "/" + r.c.choiceId + " (Day " + day
                                            + ") is at least as good as every other option");
                }
            }
        }
    }
}
