using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// Chapter 1's scenario content (design spec 3.4), built in code and cached per spending profile.
    /// Get(id, focus, travel) returns the definition for that profile: place names (3.3), travel amounts,
    /// travel labels and the order of the transport options (3.4.0). The choice ids are the same for every
    /// profile. In prompt and place strings "[place]"/"[Place]" are filled from ChapterSchedule.SpotPlaceName/
    /// SpotPlaceLabel and "[trip]"/"[daily]" from the travel amounts; "{...}" tokens are left for MaliText.Fill.
    /// </summary>
    public static class ScenarioLibrary
    {
        public const string FoodDecisionId = "food_decision";
        public const string TransportDecisionId = "transport_decision";
        public const string DataRunsOutId = "data_runs_out";
        public const string CreditBnplId = "credit_bnpl";
        public const string ImpulsePurchaseId = "impulse_purchase";
        public const string TaxiFareRiseId = "taxi_fare_rise";
        public const string FamilyObligationId = "family_obligation";
        public const string FamilyCallbackId = "family_callback";
        public const string FamilyCallbackFullId = "family_callback_full";
        public const string GroupChatContributionId = "group_chat_contribution";
        public const string EmergencyExpenseId = "emergency_expense";
        public const string MashonisaOfferId = "mashonisa_offer";
        public const string StokvelDecisionId = "stokvel_decision";
        public const string WindfallId = "windfall";
        public const string DebitOrderCheckId = "debit_order_check";
        public const string KotaRunId = "kota_run";

        static readonly string[] allIds =
        {
            FoodDecisionId, TransportDecisionId, DataRunsOutId, CreditBnplId, ImpulsePurchaseId, TaxiFareRiseId,
            FamilyObligationId, FamilyCallbackId, FamilyCallbackFullId, GroupChatContributionId, EmergencyExpenseId,
            MashonisaOfferId, StokvelDecisionId, WindfallId, DebitOrderCheckId, KotaRunId,
        };

        /// <summary>All 16 scenario ids: the 13 scheduled for every focus, kota_run (scheduled only for the
        /// food focus, Revision 4) and the 2 follow-ups.</summary>
        public static IReadOnlyList<string> AllIds => Array.AsReadOnly(allIds);

        // Money categories (design spec 2.2), written out so this file needs no other package's types.
        const string CatFood = "Food";
        const string CatTransport = "Transport";
        const string CatPhoneData = "Phone & data";
        const string CatShopping = "Shopping";
        const string CatFamily = "Family";
        const string CatFriends = "Friends";
        const string CatHome = "Home";
        const string CatBills = "Bills";
        const string CatPayLater = "Pay-later";
        const string CatLoan = "Loan";
        const string CatExtraMoney = "Extra money";

        // Obligation kinds.
        const string KindInstalment = "instalment";
        const string KindLoan = "loan";
        const string KindCommitment = "commitment";
        const string KindRepeat = "repeat";

        const float Xp = 5f;
        const int LastChapterDay = ChapterSchedule.ChapterLength;
        const int Payday = ChapterSchedule.PaydayDay;

        static readonly Dictionary<string, Dictionary<string, ScenarioDefinition>> cache =
            new Dictionary<string, Dictionary<string, ScenarioDefinition>>();

        /// <summary>The default-profile definition: Get(id, "food", "taxi").</summary>
        public static ScenarioDefinition GetById(string scenarioId)
        {
            return Get(scenarioId, ChapterSchedule.FocusFood, ChapterSchedule.TravelTaxi);
        }

        /// <summary>The definition for a spending profile (unknown focus/travel -> food/taxi); null for an unknown id.</summary>
        public static ScenarioDefinition Get(string scenarioId, string focus, string travel)
        {
            if (string.IsNullOrEmpty(scenarioId))
            {
                return null;
            }

            focus = ChapterSchedule.NormalizeFocus(focus);
            travel = ChapterSchedule.NormalizeTravel(travel);
            string key = focus + "/" + travel;
            if (!cache.TryGetValue(key, out Dictionary<string, ScenarioDefinition> set))
            {
                set = BuildAll(focus, travel);
                cache[key] = set;
            }

            return set.TryGetValue(scenarioId, out ScenarioDefinition scenario) ? scenario : null;
        }

        // ------------------------------------------------------------------ travel modes (3.4.0)

        sealed class Travel
        {
            public int trip;
            public float tripEnergy;
            public string tripLabel, tripLedger, tripReaction;
            public int daily;
            public float dailyEnergy;
            public string dailyLabel, dailyLedger, dailyObligation, dailyShort, dailyReaction;
            public string fareTitle, fareSituation, fareIntro, walkTodayLabel;
            public string tripSituation;
            public bool walkFirst;

            /// <summary>"Mostly on foot" (Revision 4, F2): the fare rise offers to keep walking, with no fares later.</summary>
            public bool keepWalking;
        }

        const string TripSituationTaxi =
            "You've got an interview on the other side of town today, off your usual route. The minibus taxi is R15 each way. You could walk it, or book a ride.";

        const string FareSituationTaxi =
            "The rank marshal says the fare is R17 each way from today. You're going to town and back every day till payday for a short course.";

        static Travel TravelFor(string travel)
        {
            switch (travel)
            {
                case ChapterSchedule.TravelEHailing:
                    return new Travel
                    {
                        trip = 44, tripEnergy = 0f, tripLabel = "Shared ride both ways (R44)",
                        tripLedger = "Shared ride there and back",
                        tripReaction = "R44 for the shared rides, {name}, there and back.",
                        daily = 50, dailyEnergy = 0f, dailyLabel = "Pay the new price (R50 a day)",
                        dailyLedger = "Shared ride at the new price", dailyObligation = "Shared rides", dailyShort = "Rides",
                        dailyReaction = "R50 today, {name}. The new price comes off again on {laterDays}.",
                        fareTitle = "Ride prices went up",
                        fareSituation = "Ride prices went up with petrol: a shared ride to town is R25 each way from today. You're going to town and back every day till payday for a short course.",
                        fareIntro = "R3 more each way, {name}, and it's every day till payday.",
                        walkTodayLabel = "Walk today, ride from tomorrow",
                        tripSituation = "You've got an interview on the other side of town today, off your usual route. A shared ride is R22 each way, a ride of your own R45. Or you could walk it.",
                    };
                case ChapterSchedule.TravelCar:
                    return new Travel
                    {
                        trip = 36, tripEnergy = 0f, tripLabel = "Petrol for the car (R36)",
                        tripLedger = "Petrol for the trip",
                        tripReaction = "R36 for petrol, {name}, there and back.",
                        daily = 40, dailyEnergy = 0f, dailyLabel = "Pay for petrol (R40 a day)",
                        dailyLedger = "Petrol for the day", dailyObligation = "Petrol", dailyShort = "Petrol",
                        dailyReaction = "R40 today, {name}. Petrol comes off again on {laterDays}.",
                        fareTitle = "Petrol went up",
                        fareSituation = "Petrol went up again: your share is R40 a day from today. You're going to town and back every day till payday for a short course.",
                        fareIntro = "Petrol's up again, {name}, and it's every day till payday.",
                        walkTodayLabel = "Walk today, drive from tomorrow",
                        tripSituation = "You've got an interview on the other side of town today, off your usual route. Petrol for the trip is about R36. You could walk it, or book a ride.",
                    };
                default: // taxi and walk ("Mostly on foot" uses the taxi amounts, with the walk option first)
                    return new Travel
                    {
                        trip = 30, tripEnergy = -5f, tripLabel = "Minibus taxi (R15 each way)",
                        tripLedger = "Taxi there and back",
                        tripReaction = "R30 for the taxi, {name}, there and back.",
                        daily = 34, dailyEnergy = -5f, dailyLabel = "Pay the new fare (R34 a day)",
                        dailyLedger = "Taxi at the new fare", dailyObligation = "Taxi fares", dailyShort = "Taxi",
                        dailyReaction = "R34 today, {name}. The fare comes off again on {laterDays}.",
                        fareTitle = "The fare went up",
                        fareSituation = FareSituationTaxi,
                        fareIntro = "R2 more each way, {name}, and it's every day till payday.",
                        walkTodayLabel = "Walk today, taxi from tomorrow",
                        tripSituation = TripSituationTaxi,
                        walkFirst = travel == ChapterSchedule.TravelWalk,
                        keepWalking = travel == ChapterSchedule.TravelWalk,
                    };
            }
        }

        // ------------------------------------------------------------------ building

        static Dictionary<string, ScenarioDefinition> BuildAll(string focus, string travel)
        {
            Travel t = TravelFor(travel);
            var set = new Dictionary<string, ScenarioDefinition>();
            foreach (ScenarioDefinition s in new[]
                     {
                         FoodDecision(), TransportDecision(t), DataRunsOut(), CreditBnpl(), ImpulsePurchase(),
                         TaxiFareRise(t), FamilyObligation(), FamilyCallback(), FamilyCallbackFull(),
                         GroupChatContribution(), EmergencyExpense(), MashonisaOffer(), StokvelDecision(), Windfall(),
                         DebitOrderCheck(), KotaRun(),
                     })
            {
                FillPlaceholders(s, focus, travel, t);
                set[s.scenarioId] = s;
            }

            return set;
        }

        static void FillPlaceholders(ScenarioDefinition s, string focus, string travel, Travel t)
        {
            string name = ChapterSchedule.SpotPlaceName(s.spotId, focus, travel);
            string label = ChapterSchedule.SpotPlaceLabel(s.spotId, focus, travel);
            string trip = t.trip.ToString(CultureInfo.InvariantCulture);
            string daily = t.daily.ToString(CultureInfo.InvariantCulture);

            string F(string text)
            {
                if (string.IsNullOrEmpty(text) || text.IndexOf('[') < 0)
                {
                    return text;
                }

                return text.Replace("[place]", name).Replace("[Place]", label)
                           .Replace("[trip]", trip).Replace("[daily]", daily);
            }

            s.title = F(s.title);
            s.description = F(s.description);
            s.introDialogue = F(s.introDialogue);
            s.promptText = F(s.promptText);
            s.placeLabel = F(s.placeLabel);
            s.gateNoun = F(s.gateNoun);
            foreach (ScenarioChoice c in s.choices)
            {
                c.label = F(c.label);
                c.ledgerLabel = F(c.ledgerLabel);
                c.instalmentLabel = F(c.instalmentLabel);
                c.instalmentShortLabel = F(c.instalmentShortLabel);
                c.maliReactionLine = F(c.maliReactionLine);
                c.maliReactionNoLater = F(c.maliReactionNoLater);
                c.followUpLaterText = F(c.followUpLaterText);
            }
        }

        static ScenarioDefinition Scenario(string id, string spot, string icon, string category, string gateNoun,
                                           string title, string place, string prompt, string situation, string intro,
                                           params ScenarioChoice[] choices)
        {
            var s = ScriptableObject.CreateInstance<ScenarioDefinition>();
            s.scenarioId = id;
            s.spotId = spot;
            s.locationHint = spot;
            s.promptIcon = icon;
            s.moneyCategory = category;
            s.gateNoun = gateNoun;
            s.title = title;
            s.placeLabel = place;
            s.promptText = prompt;
            s.description = situation;
            s.introDialogue = intro;
            s.choices = choices;
            return s;
        }

        static ScenarioChoice Choice(string id, string label, string ledger, float cash, float savings, float energy,
                                     float stress, ScenarioBehaviourTag tag, string reaction)
        {
            return new ScenarioChoice
            {
                choiceId = id,
                label = label,
                ledgerLabel = ledger ?? "",
                cashDelta = cash,
                savingsDelta = savings,
                energyDelta = energy,
                financialStressDelta = stress,
                financialXpDelta = Xp,
                behaviourTag = tag,
                maliReactionLine = reaction,
            };
        }

        static ScenarioChoice WithPayments(ScenarioChoice c, int count, float amount, int interval, int firstDue,
                                           int lastDue, string label, string shortLabel, string category, string kind)
        {
            c.instalmentCount = count;
            c.instalmentAmount = amount;
            c.instalmentIntervalDays = interval;
            c.instalmentFirstDueDay = firstDue;
            c.instalmentLastDueDay = lastDue;
            c.instalmentLabel = label;
            c.instalmentShortLabel = shortLabel;
            c.instalmentCategory = category;
            c.instalmentKind = kind;
            return c;
        }

        static ScenarioChoice WithFollowUp(ScenarioChoice c, string scenarioId, int afterDays, string laterText)
        {
            c.followUpScenarioId = scenarioId;
            c.followUpAfterDays = afterDays;
            c.followUpLaterText = laterText;
            return c;
        }

        static ScenarioChoice WithNoLater(ScenarioChoice c, string reaction)
        {
            c.maliReactionNoLater = reaction;
            return c;
        }

        const ScenarioBehaviourTag Neutral = ScenarioBehaviourTag.Neutral;
        const ScenarioBehaviourTag Frugal = ScenarioBehaviourTag.Frugal;
        const ScenarioBehaviourTag Discretionary = ScenarioBehaviourTag.Discretionary;
        const ScenarioBehaviourTag Deferred = ScenarioBehaviourTag.Deferred;

        // ------------------------------------------------------------------ 3.4.1 - 3.4.13

        static ScenarioDefinition FoodDecision()
        {
            return Scenario(FoodDecisionId, ChapterSchedule.SpotCorner, "shoppingBasket", CatFood, "lunch",
                "What's for lunch?", "[Place]", "Lunch at [place]",
                "It's midday and you're hungry. There's a kota special, vetkoek and mince, or you could push through till supper.",
                "Midday already, {name}. What are you having?",
                Choice("kota", "Kota and a cold drink (R50)", "Kota and a cold drink", -50f, 0f, 0f, -5f, Discretionary,
                    "R50 for lunch. You're full till supper."),
                Choice("vetkoek", "Vetkoek and mince (R20)", "Vetkoek and mince", -20f, 0f, 0f, -2f, Frugal,
                    "R20 for lunch. That keeps you going till supper."),
                Choice("skip_lunch", "Skip lunch", "", 0f, 0f, -45f, 5f, Frugal,
                    "No money out. Skipping lunch took 45 energy, and a shift needs 60."));
        }

        static ScenarioDefinition TransportDecision(Travel t)
        {
            ScenarioChoice usual = Choice("usual", t.tripLabel, t.tripLedger, -t.trip, 0f, t.tripEnergy, 0f, Neutral,
                t.tripReaction);
            ScenarioChoice walk = Choice("walk", "Walk there and back", "", 0f, 0f, -45f, 0f, Frugal,
                "No fare today. The walk took 45 energy.");
            ScenarioChoice ride = Choice("ride", "Book a ride both ways (R90)", "Ride there and back", -90f, 0f, 0f, -2f,
                Discretionary, "R90 for the rides. Door to door, no waiting.");

            return Scenario(TransportDecisionId, ChapterSchedule.SpotTaxi, "car", CatTransport, "the trip across town",
                "Getting across town", "[Place]", "A trip across town", t.tripSituation,
                "How are we getting there, {name}?",
                t.walkFirst ? new[] { walk, usual, ride } : new[] { usual, walk, ride });
        }

        static ScenarioDefinition DataRunsOut()
        {
            return Scenario(DataRunsOutId, ChapterSchedule.SpotCorner, "phone", CatPhoneData, "sorting your data",
                "Out of data", "Your phone", "Your phone's out of data",
                "Your data ran out this morning. You want to send a CV today and check for replies, and your next bundle only comes on payday.",
                "Your data's gone, {name}, and the CV is still sitting on your phone.",
                Choice("bundle_1gb", "Buy 1GB to last till payday (R85)", "1GB data bundle", -85f, 0f, 0f, 0f, Neutral,
                    "R85 and you're online till payday. The CV can go."),
                WithNoLater(WithPayments(
                        Choice("day_bundle", "Buy day bundles (R15 a day)", "Day data bundle", -15f, 0f, 0f, 2f, Deferred,
                            "R15 covers today, {name}. Day bundles carry on: R15 on {laterDays}."),
                        2, 15f, 1, 0, LastChapterDay, "Day bundles", "Data", CatPhoneData, KindRepeat),
                    "R15 covers today, {name}, and that's the last day before payday."),
                Choice("free_wifi", "Use the free internet at the mall", "", 0f, 0f, -45f, 3f, Frugal,
                    "No money out. The walk to the mall and the wait took 45 energy."));
        }

        static ScenarioDefinition CreditBnpl()
        {
            return Scenario(CreditBnplId, ChapterSchedule.SpotHub, "shoppingBasket", CatShopping, "the speaker",
                "Pay now or pay later?", "Phone shop", "A deal at the phone shop",
                "The phone shop has a wireless speaker you've wanted for ages: R360. Pay it all now, or take it home today for R120 and pay R130 twice over the next few days.",
                "Same speaker, {name}. Two ways to pay for it.",
                Choice("pay_in_full", "Pay R360 now", "Full price for the speaker", -360f, 0f, 0f, 0f, Neutral,
                    "R360 today, and the speaker's paid off."),
                WithPayments(
                    Choice("pay_later", "Pay later: R120 now, then R130 twice", "Speaker deposit", -120f, 0f, 0f, 3f,
                        Deferred,
                        "It's yours today, {name}. R130 comes off in two days, and again after that: R20 more in all."),
                    2, 130f, 2, 0, 0, "Speaker (pay-later)", "Speaker", CatPayLater, KindInstalment),
                Choice("leave_it", "Leave it for now", "", 0f, 0f, 0f, 0f, Frugal,
                    "Nothing spent. The speaker stays in the shop."));
        }

        static ScenarioDefinition ImpulsePurchase()
        {
            return Scenario(ImpulsePurchaseId, ChapterSchedule.SpotShopfront, "shoppingBasket", CatShopping,
                "the shop window",
                "Something caught your eye", "[Place]", "[Place]",
                "There's a hoodie in the window, R120 on special for a few more days. You don't need it, but you've been looking at it for weeks.",
                "That's the one you keep looking at, isn't it, {name}?",
                Choice("buy_it", "Buy it (R120)", "Hoodie on special", -120f, 0f, 0f, -2f, Discretionary,
                    "It's yours. That's R120 of today."),
                Choice("walk_away", "Walk away for now", "", 0f, 0f, 0f, 0f, Frugal,
                    "Nothing spent, {name}. It's on special for a few more days if you still want it."),
                Choice("to_savings", "Move R120 to savings instead", "Hoodie money to savings", -120f, 120f, 0f, 0f,
                    Frugal, "R120 moved to savings. Your {goalName} is at R{savings} of R{goalTarget}."));
        }

        static ScenarioDefinition TaxiFareRise(Travel t)
        {
            // "Mostly on foot" (Revision 4, F2): the free option is to keep walking every day, with no fares booked.
            // Its cost is the 50 energy on the card: as Day 3's first scenario it costs that day's shift.
            ScenarioChoice walk = t.keepWalking
                ? Choice("walk_today", "Keep walking, no fares", "", 0f, 0f, -50f, 2f, Frugal,
                    "No fares to pay. Walking to town and back took 50 energy today.")
                : WithNoLater(WithPayments(
                        Choice("walk_today", t.walkTodayLabel, "", 0f, 0f, -50f, 2f, Frugal,
                            "Nothing spent today. That walk took 50 energy, and it's R[daily] a day again from tomorrow."),
                        4, t.daily, 1, 0, LastChapterDay, t.dailyObligation, t.dailyShort, CatTransport, KindRepeat),
                    "Nothing spent today. That walk took 50 energy.");

            return Scenario(TaxiFareRiseId, ChapterSchedule.SpotTaxi, "car", CatTransport, "the trip to town",
                t.fareTitle, "[Place]", "News at [place]", t.fareSituation, t.fareIntro,
                WithNoLater(WithPayments(
                        Choice("pay_new_fare", t.dailyLabel, t.dailyLedger, -t.daily, 0f, t.dailyEnergy, 0f, Neutral,
                            t.dailyReaction),
                        4, t.daily, 1, 0, LastChapterDay, t.dailyObligation, t.dailyShort, CatTransport, KindRepeat),
                    "R[daily] today, {name}. That's the last trip before payday."),
                Choice("lift_club", "Join a lift club (R120 till payday)", "Lift club till payday", -120f, 0f, 0f, -3f,
                    Neutral, "R120 for the lift club. Your trips to the course are covered till payday."),
                walk);
        }

        static ScenarioDefinition FamilyObligation()
        {
            return Scenario(FamilyObligationId, ChapterSchedule.SpotGate, "token_give", CatFamily, "the call from home",
                "A call from home", "At home", "Your phone's ringing",
                "Your aunt calls. Gogo's chronic medication has run out and the clinic is out of stock. The pharmacy wants R200 to tide her over.",
                "It's your aunt, {name}. It's about Gogo.",
                Choice("send_full", "Send R200", "Gogo's meds", -200f, 0f, 0f, 0f, Neutral,
                    "R200 is on its way to Gogo. That's R200 of your week."),
                WithFollowUp(
                    Choice("send_part", "Send R80 for now", "Towards Gogo's meds", -80f, 0f, 0f, 3f, Neutral,
                        "R80 is on its way to Gogo, {name}. Your aunt will call back about the rest."),
                    FamilyCallbackId, 2, "Call back"),
                Choice("from_savings", "Send R200 from savings", "Gogo's meds from savings", 0f, -200f, 0f, 0f, Neutral,
                    "R200 from savings, and Gogo has her meds. Savings is at R{savings} now."),
                WithFollowUp(
                    Choice("cant_this_week", "Explain you can't this week", "", 0f, 0f, 0f, 8f, Neutral,
                        "That's a hard call to make. Your aunt says she'll try you again in two days."),
                    FamilyCallbackFullId, 2, "Call back"));
        }

        const string CallbackTitle = "Your aunt calls back";
        const string CallbackPrompt = "Your aunt's calling back";
        const string CallbackIntro = "It's your aunt again, {name}.";

        static ScenarioDefinition FamilyCallback()
        {
            ScenarioDefinition s = Scenario(FamilyCallbackId, ChapterSchedule.SpotGate, "token_give", CatFamily, "",
                CallbackTitle, "At home", CallbackPrompt,
                "Your aunt again. Gogo's meds are running low, and the clinic still has none. R120 would see her through to month-end.",
                CallbackIntro,
                Choice("send_rest", "Send R120", "Rest of Gogo's meds", -120f, 0f, 0f, 0f, Neutral,
                    "R120 is on its way, and Gogo's covered till month-end."),
                Choice("from_savings", "Send R120 from savings", "Gogo's meds from savings", 0f, -120f, 0f, 0f, Neutral,
                    "R120 from savings, and Gogo's covered. Savings is at R{savings} now."),
                Choice("not_this_week", "Not this week either", "", 0f, 0f, 0f, 6f, Neutral,
                    "That's a hard one to say twice. Your week stays as it was."));
            s.isFollowUp = true;
            return s;
        }

        static ScenarioDefinition FamilyCallbackFull()
        {
            ScenarioDefinition s = Scenario(FamilyCallbackFullId, ChapterSchedule.SpotGate, "token_give", CatFamily, "",
                CallbackTitle, "At home", CallbackPrompt,
                "Your aunt again. Gogo's meds are running low, and the clinic still has none. R200 would see her through to month-end.",
                CallbackIntro,
                Choice("send_full", "Send R200", "Gogo's meds", -200f, 0f, 0f, 0f, Neutral,
                    "R200 is on its way, and Gogo's covered till month-end."),
                Choice("from_savings", "Send R200 from savings", "Gogo's meds from savings", 0f, -200f, 0f, 0f, Neutral,
                    "R200 from savings, and Gogo's covered. Savings is at R{savings} now."),
                Choice("not_this_week", "Not this week either", "", 0f, 0f, 0f, 6f, Neutral,
                    "That's a hard one to say twice. Your week stays as it was."));
            s.isFollowUp = true;
            return s;
        }

        static ScenarioDefinition GroupChatContribution()
        {
            return Scenario(GroupChatContributionId, ChapterSchedule.SpotCorner, "phone", CatFriends, "the group chat",
                "Birthday in the group chat", "Group chat", "The group chat is buzzing",
                "{friend}'s birthday dinner is on the weekend. The group chat says everyone's putting in R150 for the dinner and her gift.",
                "{friend}'s birthday, {name}. The chat's asking who's in.",
                Choice("in_for_dinner", "I'm in (R150)", "Birthday dinner", -150f, 0f, 0f, -4f, Discretionary,
                    "You're in. That's R150 of the week."),
                Choice("gift_only", "Send R50 for the gift, skip the dinner", "Birthday gift", -50f, 0f, 0f, 2f, Neutral,
                    "R50 towards the gift, {name}. You'll miss the dinner, and R100 stays with you."),
                Choice("not_this_time", "Not this time", "", 0f, 0f, 0f, 4f, Frugal,
                    "That message is hard to send. Your week stays as it was."));
        }

        static ScenarioDefinition EmergencyExpense()
        {
            return Scenario(EmergencyExpenseId, ChapterSchedule.SpotGate, "home", CatHome, "the geyser",
                "The geyser broke", "At home", "Something's up at home",
                "The geyser element has burnt out: no hot water. Sipho from two doors down does plumbing on the side. He can fix it today for R350, parts included.",
                "Cold water this morning, {name}. Sipho can fix it today.",
                Choice("from_savings", "Pay from savings (R350)", "Geyser repair from savings", 0f, -350f, 0f, 0f,
                    Neutral, "R350 from savings, and there's hot water tonight. Savings is at R{savings} now."),
                Choice("from_cash", "Pay from cash (R350)", "Geyser repair", -350f, 0f, 0f, 0f, Neutral,
                    "R350 from cash, and there's hot water tonight. You've got R{cash} in cash."),
                WithPayments(
                    Choice("cold_showers", "Cold showers until payday", "", 0f, 0f, -10f, 12f, Deferred,
                        "No money out today. It's cold water for now, and the R350 repair is promised for payday."),
                    1, 350f, 2, Payday, 0, "Geyser repair", "Geyser", CatHome, KindCommitment));
        }

        static ScenarioDefinition MashonisaOffer()
        {
            return Scenario(MashonisaOfferId, ChapterSchedule.SpotEast, "hand_token", CatLoan, "Bra K",
                "Till payday", "Down the road", "Bra K wants a word",
                "Bra K from down the road lends to anyone till payday, no forms. Whatever you take, you pay back half as much again in two days.",
                "Bra K's offering a loan, {name}.",
                WithPayments(
                    Choice("borrow_400", "Borrow R400 (R600 back in two days)", "Loan from Bra K", 400f, 0f, 0f, 6f,
                        Deferred,
                        "R400 in your hand. In two days R600 goes back to Bra K, and R200 of that is what the loan costs."),
                    1, 600f, 2, 0, 0, "Bra K loan repayment", "Bra K", CatLoan, KindLoan),
                WithPayments(
                    Choice("borrow_200", "Borrow R200 (R300 back in two days)", "Loan from Bra K", 200f, 0f, 0f, 3f,
                        Deferred, "R200 now. In two days R300 goes back, and R100 of that is what the loan costs."),
                    1, 300f, 2, 0, 0, "Bra K loan repayment", "Bra K", CatLoan, KindLoan),
                Choice("not_today", "Not today", "", 0f, 0f, 0f, 0f, Neutral,
                    "Nothing borrowed, and nothing owed to Bra K."));
        }

        static ScenarioDefinition StokvelDecision()
        {
            return Scenario(StokvelDecisionId, ChapterSchedule.SpotHub, "tokens_stack", CatBills, "the stokvel meeting",
                "Joining a stokvel", "Shops by the bank", "Stokvel meeting at the shops",
                "Mam' Dlamini's stokvel is taking new members. Everyone puts in R200 each payday, and each month one member gets the whole pot. Two members sign for the account.",
                "The stokvel's meeting today, {name}.",
                WithPayments(
                    Choice("join", "Join: R200 from payday", "", 0f, 0f, 0f, -2f, Frugal,
                        "You're in, {name}. Your first R200 goes to the stokvel on payday."),
                    1, 200f, 2, Payday, 0, "Stokvel contribution", "Stokvel", CatBills, KindCommitment),
                Choice("not_for_now", "Not this one, for now", "", 0f, 0f, 0f, 0f, Neutral,
                    "Not this one. Mam' Dlamini says the door's open if you change your mind."));
        }

        static ScenarioDefinition Windfall()
        {
            return Scenario(WindfallId, ChapterSchedule.SpotEast, "coin", CatExtraMoney, "your neighbour",
                "Unexpected money", "Down the road", "Your neighbour's waving you over",
                "Your neighbour pays you R300 for helping her move furniture last weekend. You weren't counting on it.",
                "Some money you weren't expecting, {name}.",
                // Spec ledger "From the neighbour, to savings" is 30 characters; the ledger cap is 28, so it is shortened (deviation).
                Choice("all_to_savings", "Put all R300 in savings", "Neighbour's money to savings", 0f, 300f, 0f, 0f,
                    Frugal, "R300 into savings. Your {goalName} is at R{savings} of R{goalTarget}."),
                Choice("keep_cash", "Keep it as cash", "From the neighbour", 300f, 0f, 0f, -5f, Discretionary,
                    "R300 in your pocket, {name}. Yours to use."),
                Choice("half_half", "R150 cash, R150 savings", "From the neighbour", 150f, 150f, 0f, -2f, Neutral,
                    "R150 each way. Some for now, some for your {goalName}."));
        }

        static ScenarioDefinition DebitOrderCheck()
        {
            return Scenario(DebitOrderCheckId, ChapterSchedule.SpotGate, "notepad", CatBills, "the gym SMS",
                "It goes off on payday", "At home", "An SMS about your gym",
                "Your gym debit order of R199 goes off on payday. Pay sometimes lands later that day, and a bounced debit order costs a fee on top.",
                "Tomorrow's payday, {name}, and the gym goes off too.",
                WithPayments(
                    Choice("move_to_cash", "Move R199 to cash", "Gym money from savings", 199f, -199f, 0f, -3f, Neutral,
                        "R199 moved across. It'll clear on payday. Savings is at R{savings}."),
                    1, 199f, 2, Payday, 0, "Gym debit order", "Gym", CatBills, KindCommitment),
                Choice("cancel_gym", "Cancel the gym, run in the park", "", 0f, 0f, 0f, 0f, Frugal,
                    "Cancelled, {name}. From payday, that R199 stays with you."),
                WithPayments(
                    Choice("leave_it", "Leave it, hope pay comes first", "", 0f, 0f, 0f, 6f, Deferred,
                        "It goes off on payday either way. If pay lands after it, the bank adds a fee."),
                    1, 199f, 2, Payday, 0, "Gym debit order", "Gym", CatBills, KindCommitment));
        }

        // ------------------------------------------------------------------ Revision 4 (F1): the food week

        /// <summary>
        /// Day 5 for the food focus only (ChapterSchedule): friends doing a kota run. Every option has a cost the
        /// card shows: R65 for the full kota (delivered, no energy), R30 and 10 energy for half a kota you fetch,
        /// 20 energy to cook at home.
        /// </summary>
        static ScenarioDefinition KotaRun()
        {
            return Scenario(KotaRunId, ChapterSchedule.SpotCorner, "shoppingBasket", CatFood, "the kota run",
                "Kota run tonight", "[Place]", "Your friends want a kota run",
                "Your friends are doing a kota run tonight and want to know if you're in. A full kota and chips is R65, delivered. You could split one and fetch it, or cook at home.",
                "Your friends are getting kotas tonight, {name}. Are you in?",
                Choice("full_kota", "Full kota and chips (R65)", "Kota run with friends", -65f, 0f, 0f, -4f, Discretionary,
                    "R65 for supper, and you ate it with your friends."),
                Choice("share_kota", "Split one and fetch it (R30)", "Half a kota", -30f, 0f, -10f, -1f, Neutral,
                    "R30 for your half, {name}. Fetching it took 10 energy."),
                Choice("cook_home", "Cook with what's at home", "", 0f, 0f, -20f, 2f, Frugal,
                    "No money out. Cooking supper took 20 energy."));
        }
    }
}
