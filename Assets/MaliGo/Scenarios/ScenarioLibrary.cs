using System.Collections.Generic;
using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// Seeds ScenarioDefinition instances in code and indexes them by id.
    /// This exists so scenario content doesn't need to be hand-authored as
    /// ScriptableObject .asset files to ship a first vertical slice; a
    /// designer can later create ScenarioDefinition assets in the Editor
    /// (it already supports [CreateAssetMenu]) and register them here instead,
    /// without changing any consuming code.
    /// </summary>
    public static class ScenarioLibrary
    {
        public const string FoodDecisionId = "food_decision";
        public const string TransportDecisionId = "transport_decision";
        public const string ImpulsePurchaseId = "impulse_purchase";
        public const string EmergencyExpenseId = "emergency_expense";
        public const string WindfallId = "windfall";
        public const string FamilyObligationId = "family_obligation";
        public const string StokvelDecisionId = "stokvel_decision";
        public const string CreditBnplId = "credit_bnpl";

        static Dictionary<string, ScenarioDefinition> cache;

        public static ScenarioDefinition GetById(string scenarioId)
        {
            EnsureBuilt();
            return cache.TryGetValue(scenarioId, out var scenario) ? scenario : null;
        }

        static void EnsureBuilt()
        {
            if (cache != null)
            {
                return;
            }

            cache = new Dictionary<string, ScenarioDefinition>();
            Register(BuildFoodDecisionScenario());
            Register(BuildTransportDecisionScenario());
            Register(BuildImpulsePurchaseScenario());
            Register(BuildEmergencyExpenseScenario());
            Register(BuildWindfallScenario());
            Register(BuildFamilyObligationScenario());
            Register(BuildStokvelDecisionScenario());
            Register(BuildCreditBnplScenario());
        }

        static void Register(ScenarioDefinition scenario)
        {
            cache[scenario.scenarioId] = scenario;
        }

        static ScenarioDefinition BuildFoodDecisionScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = FoodDecisionId;
            scenario.title = "What's for lunch?";
            scenario.description = "A food stall by the commercial hub. You need to eat today - the question is how.";
            scenario.locationHint = "Local_Commercial_Hub";
            scenario.introDialogue = "You've got a full day ahead, {0}. Food, transport, maybe a little fun. The question is... what comes first?";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "expensive_meal",
                    label = "Buy a R50 meal",
                    description = "Cash -R50, Stress -5",
                    cashDelta = -50f,
                    financialStressDelta = -5f,
                    behaviourTag = ScenarioBehaviourTag.Discretionary,
                    financialXpDelta = 5f,
                    maliReactionLine = "Nice treat, {0}. Let's see how that affects the rest of your day."
                },
                new ScenarioChoice
                {
                    choiceId = "affordable_meal",
                    label = "Buy a R20 meal",
                    description = "Cash -R20, Stress -2",
                    cashDelta = -20f,
                    financialStressDelta = -2f,
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "Simple and sorted, {0}. That leaves you a bit more room today."
                },
                new ScenarioChoice
                {
                    choiceId = "skip_meal",
                    label = "Skip the meal",
                    description = "Cash unchanged, Stress +5, Energy -10",
                    financialStressDelta = 5f,
                    energyDelta = -10f,
                    behaviourTag = ScenarioBehaviourTag.Deferred,
                    financialXpDelta = 5f,
                    maliReactionLine = "Tough call skipping food, {0}. Let's keep an eye on your energy though."
                }
            };

            return scenario;
        }

        static ScenarioDefinition BuildTransportDecisionScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = TransportDecisionId;
            scenario.title = "Getting Around";
            scenario.description = "You need to get across town today. Cost, time, and energy all trade off differently.";
            scenario.locationHint = "Road_Main";
            scenario.introDialogue = "Time to get moving, {0}. How are we getting there today?";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "walk",
                    label = "Walk",
                    description = "Cash unchanged, Energy -15",
                    energyDelta = -15f,
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "Free is free, {0}. Your legs will feel it by this evening though."
                },
                new ScenarioChoice
                {
                    choiceId = "minibus_taxi",
                    label = "Take the minibus taxi (R15)",
                    description = "Cash -R15, Energy -5",
                    cashDelta = -15f,
                    energyDelta = -5f,
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "Quick and reasonable, {0}. That's a fair trade for your time."
                },
                new ScenarioChoice
                {
                    choiceId = "private_ride",
                    label = "Call a private ride (R45)",
                    description = "Cash -R45, Energy -2, Stress -2",
                    cashDelta = -45f,
                    energyDelta = -2f,
                    financialStressDelta = -2f,
                    behaviourTag = ScenarioBehaviourTag.Discretionary,
                    financialXpDelta = 5f,
                    maliReactionLine = "Comfortable choice, {0}. Convenience has a price tag, but you know that."
                }
            };

            return scenario;
        }

        static ScenarioDefinition BuildImpulsePurchaseScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = ImpulsePurchaseId;
            scenario.title = "Something Caught Your Eye";
            scenario.description = "A shop window display has something you don't need but really want.";
            scenario.locationHint = "Local_Commercial_Hub";
            scenario.introDialogue = "That looks nice, doesn't it, {0}? Not exactly on the list though.";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "buy_impulse_item",
                    label = "Buy it now (R120)",
                    description = "Cash -R120, Stress +2",
                    cashDelta = -120f,
                    financialStressDelta = 2f,
                    behaviourTag = ScenarioBehaviourTag.Discretionary,
                    financialXpDelta = 5f,
                    maliReactionLine = "That's yours now, {0}. Let's see how it sits with the rest of your month."
                },
                new ScenarioChoice
                {
                    choiceId = "walk_away",
                    label = "Think about it and walk away",
                    description = "Cash unchanged",
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "Nothing wrong with waiting, {0}. It'll still be there if you really want it."
                },
                new ScenarioChoice
                {
                    choiceId = "redirect_to_savings",
                    label = "Skip it, put R120 into savings instead",
                    description = "Savings +R120",
                    savingsDelta = 120f,
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "That's the move, {0}. Turned a want into progress toward your goal."
                }
            };

            return scenario;
        }

        static ScenarioDefinition BuildEmergencyExpenseScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = EmergencyExpenseId;
            scenario.title = "Something Just Broke";
            scenario.description = "An unexpected cost lands on you today - repair, medical, or similar. R600, one way or another.";
            scenario.locationHint = "Player_House";
            scenario.introDialogue = "This one's not in the plan, {0}. Let's see how you handle it.";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "pay_from_savings",
                    label = "Cover it from savings (R600)",
                    description = "Savings -R600",
                    savingsDelta = -600f,
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "That's exactly what that buffer was there for, {0}."
                },
                new ScenarioChoice
                {
                    choiceId = "pay_from_cash",
                    label = "Cover it from cash (R600)",
                    description = "Cash -R600",
                    cashDelta = -600f,
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "Handled, {0}. Just keep an eye on what's left for the rest of the week."
                },
                new ScenarioChoice
                {
                    choiceId = "delay_it",
                    label = "Delay it - you can't cover it right now",
                    description = "Stress +15, Energy -5",
                    financialStressDelta = 15f,
                    energyDelta = -5f,
                    behaviourTag = ScenarioBehaviourTag.Deferred,
                    financialXpDelta = 5f,
                    maliReactionLine = "That one's not going away on its own, {0}. We'll need to deal with it eventually."
                }
            };

            return scenario;
        }

        static ScenarioDefinition BuildWindfallScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = WindfallId;
            scenario.title = "Unexpected Money";
            scenario.description = "A bonus, gift, or side-hustle payment landed today - R300 you weren't counting on.";
            scenario.locationHint = "Player_House";
            scenario.introDialogue = "Nice surprise today, {0} - some extra money came in. What are you going to do with it?";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "bank_the_windfall",
                    label = "Bank all of it",
                    description = "Savings +R300",
                    savingsDelta = 300f,
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "Straight to the goal, {0}. Nice."
                },
                new ScenarioChoice
                {
                    choiceId = "treat_yourself",
                    label = "Treat yourself with it",
                    description = "Cash +R300, Stress -5",
                    cashDelta = 300f,
                    financialStressDelta = -5f,
                    behaviourTag = ScenarioBehaviourTag.Discretionary,
                    financialXpDelta = 5f,
                    maliReactionLine = "You earned a little enjoyment too, {0}."
                },
                new ScenarioChoice
                {
                    choiceId = "split_the_windfall",
                    label = "Split it - half spend, half save",
                    description = "Cash +R150, Savings +R150",
                    cashDelta = 150f,
                    savingsDelta = 150f,
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "Balanced call, {0} - some now, some for later."
                }
            };

            return scenario;
        }

        static ScenarioDefinition BuildFamilyObligationScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = FamilyObligationId;
            scenario.title = "A Call From Home";
            scenario.description = "A family member needs help covering an urgent cost, right while you're trying to build your own buffer.";
            scenario.locationHint = "Player_House";
            scenario.introDialogue = "Family's asking for a hand today, {0}. That's a real pull on what you've been building.";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "send_full_support",
                    label = "Send the full amount (R200)",
                    description = "Cash -R200",
                    cashDelta = -200f,
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "That's real support, {0}. It'll take a bit longer to hit your own goal, but family matters too."
                },
                new ScenarioChoice
                {
                    choiceId = "send_partial_support",
                    label = "Send what you can spare (R80)",
                    description = "Cash -R80",
                    cashDelta = -80f,
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "A middle ground, {0}. Not everything, but something."
                },
                new ScenarioChoice
                {
                    choiceId = "explain_cannot_help",
                    label = "Explain you can't help right now",
                    description = "Stress +8",
                    financialStressDelta = 8f,
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "Tough conversation, {0}. Your own plan stays on track, but that's not an easy call to make."
                }
            };

            return scenario;
        }

        static ScenarioDefinition BuildStokvelDecisionScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = StokvelDecisionId;
            scenario.title = "Joining a Stokvel";
            scenario.description = "A neighbour is starting a savings stokvel - everyone contributes monthly, and members take turns receiving the full pot. It runs on trust.";
            scenario.locationHint = "Local_Commercial_Hub";
            scenario.introDialogue = "Word's going around about a new stokvel starting up, {0}. Joining means committing to contribute every month, no matter what.";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "join_stokvel",
                    label = "Join and commit R100/month",
                    description = "Cash -R100, Savings +R100",
                    cashDelta = -100f,
                    savingsDelta = 100f,
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "You're in, {0}. That's a real commitment, and a real community behind it."
                },
                new ScenarioChoice
                {
                    choiceId = "wait_and_see",
                    label = "Wait and see how it goes first",
                    description = "Cash unchanged",
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "Fair enough, {0}. No harm in watching before committing your own money."
                },
                new ScenarioChoice
                {
                    choiceId = "decline_stokvel",
                    label = "Decide it's not worth the risk",
                    description = "Cash unchanged",
                    behaviourTag = ScenarioBehaviourTag.Neutral,
                    financialXpDelta = 5f,
                    maliReactionLine = "Trust matters a lot with these, {0}. Better to sit this one out than regret it."
                }
            };

            return scenario;
        }

        static ScenarioDefinition BuildCreditBnplScenario()
        {
            var scenario = ScriptableObject.CreateInstance<ScenarioDefinition>();
            scenario.scenarioId = CreditBnplId;
            scenario.title = "Pay Now or Pay Later?";
            scenario.description = "A shop offers to let you take an item home today and pay it off over the next few months.";
            scenario.locationHint = "Local_Commercial_Hub";
            scenario.introDialogue = "This one's got an easy-payment option, {0}. Take it now and pay a bit each month, or pay it off in full today.";

            scenario.choices = new[]
            {
                new ScenarioChoice
                {
                    choiceId = "pay_in_full",
                    label = "Pay in full now (R400)",
                    description = "Cash -R400",
                    cashDelta = -400f,
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "Done and done, {0}. Nothing hanging over you next month."
                },
                new ScenarioChoice
                {
                    choiceId = "buy_now_pay_later",
                    label = "Take it home, pay later (R50 deposit)",
                    description = "Cash -R50 now, then R120 a week for 3 weeks",
                    cashDelta = -50f,
                    financialStressDelta = 3f,
                    behaviourTag = ScenarioBehaviourTag.Discretionary,
                    financialXpDelta = 5f,
                    instalmentCount = 3,
                    instalmentAmount = 120f,
                    instalmentIntervalDays = 7,
                    instalmentLabel = "Pay-later instalment",
                    maliReactionLine = "It's yours today, {0} - just remember the next few weeks already have a piece spoken for."
                },
                new ScenarioChoice
                {
                    choiceId = "skip_the_item",
                    label = "Skip it - don't need it that badly",
                    description = "Cash unchanged",
                    behaviourTag = ScenarioBehaviourTag.Frugal,
                    financialXpDelta = 5f,
                    maliReactionLine = "Sometimes walking away is the whole trick, {0}."
                }
            };

            return scenario;
        }
    }
}
