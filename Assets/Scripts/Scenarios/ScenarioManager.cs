using MaliGo.Characters;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// Presents ScenarioDefinitions to the player, applies choice consequences to
    /// PlayerData, and routes Mali's intro/reaction lines. One instance lives in
    /// MaliGo_Systems for the lifetime of the world scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class ScenarioManager : MonoBehaviour
    {
        public static ScenarioManager Instance { get; private set; }

        [SerializeField] ScenarioChoiceUI choiceUI;

        bool scenarioInProgress;

        public bool IsScenarioInProgress => scenarioInProgress;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (choiceUI == null)
            {
                choiceUI = GetComponentInChildren<ScenarioChoiceUI>();
            }

            if (choiceUI == null)
            {
                var uiObject = new GameObject("ScenarioChoiceUI");
                uiObject.transform.SetParent(transform, false);
                choiceUI = uiObject.AddComponent<ScenarioChoiceUI>();
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>Returns false if a scenario is already open, or this one isn't available yet.</summary>
        public bool TryBeginScenario(ScenarioDefinition scenario)
        {
            if (scenario == null || scenarioInProgress)
            {
                return false;
            }

            var player = PlayerDataAccess.GetCurrentPlayer();
            if (player != null && !scenario.IsAvailableForLifeChapter(player.currentLifeChapter))
            {
                return false;
            }

            // Each scenario is played once; replaying the windfall would hand out free money.
            if (player != null && player.IsScenarioCompleted(scenario.scenarioId))
            {
                return false;
            }

            scenarioInProgress = true;

            string characterName = PlayerDataAccess.GetCharacterName();
            MaliDialogueController maliDialogue = FindMaliDialogue();

            if (maliDialogue != null && !string.IsNullOrWhiteSpace(scenario.introDialogue))
            {
                maliDialogue.ShowFormatted(scenario.introDialogue, characterName);
            }

            choiceUI.Show(scenario, choice => ResolveChoice(scenario, choice, maliDialogue, characterName));
            return true;
        }

        void ResolveChoice(ScenarioDefinition scenario, ScenarioChoice choice, MaliDialogueController maliDialogue, string characterName)
        {
            ApplyConsequences(scenario, choice);

            if (maliDialogue != null)
            {
                string line = string.IsNullOrWhiteSpace(choice.maliReactionLine)
                    ? "Noted, {0}. Let's carry on."
                    : choice.maliReactionLine;
                maliDialogue.ShowFormatted(line, characterName);
            }

            scenarioInProgress = false;
        }

        static void ApplyConsequences(ScenarioDefinition scenario, ScenarioChoice choice)
        {
            if (PlayerDataManager.Instance == null)
            {
                return;
            }

            PlayerDataManager.Instance.UpdatePlayerData(data =>
            {
                FinancialStats stats = data.financialStats;
                stats.cash = Mathf.Max(0f, stats.cash + choice.cashDelta);
                stats.savings = Mathf.Max(0f, stats.savings + choice.savingsDelta);
                stats.financialStress = Mathf.Clamp(stats.financialStress + choice.financialStressDelta, 0f, 100f);
                stats.energy = Mathf.Clamp(stats.energy + choice.energyDelta, 0f, 100f);
                stats.financialXP += choice.financialXpDelta;

                ApplyBehaviourSignal(stats, choice.behaviourTag);

                data.financialProfile.spendingBehaviour = DescribeSpending(stats.spendingBehaviourScore);
                data.financialProfile.savingBehaviour = DescribeSaving(stats.savingBehaviourScore);

                MarkScenarioCompleted(data, scenario.scenarioId);
                AddRepayments(data, scenario, choice);
            }, saveImmediately: true);
        }

        /// <summary>
        /// Nudges the rolling behaviour scores toward this choice's signal rather than
        /// overwriting them, so the player's financial profile reflects a pattern of
        /// decisions instead of flipping on a single transaction.
        /// </summary>
        static void ApplyBehaviourSignal(FinancialStats stats, ScenarioBehaviourTag tag)
        {
            float spendSignal;
            float saveSignal;

            switch (tag)
            {
                case ScenarioBehaviourTag.Discretionary:
                    spendSignal = 0.85f;
                    saveSignal = 0.15f;
                    break;
                case ScenarioBehaviourTag.Frugal:
                    spendSignal = 0.15f;
                    saveSignal = 0.8f;
                    break;
                case ScenarioBehaviourTag.Deferred:
                    spendSignal = 0.05f;
                    saveSignal = 0.6f;
                    break;
                default:
                    spendSignal = 0.5f;
                    saveSignal = 0.5f;
                    break;
            }

            const float spendAlpha = 0.25f;
            const float saveAlpha = 0.2f;
            stats.spendingBehaviourScore = Mathf.Lerp(stats.spendingBehaviourScore, spendSignal, spendAlpha);
            stats.savingBehaviourScore = Mathf.Lerp(stats.savingBehaviourScore, saveSignal, saveAlpha);
        }

        static string DescribeSpending(float score)
        {
            if (score > 0.6f) return "impulsive";
            if (score < 0.35f) return "careful";
            return "balanced";
        }

        static string DescribeSaving(float score)
        {
            if (score > 0.6f) return "consistent";
            if (score < 0.35f) return "rarely";
            return "sometimes";
        }

        /// <summary>
        /// Turns a pay-later style choice into real repayments, so the cost keeps arriving
        /// after the choice instead of ending at the deposit.
        /// </summary>
        static void AddRepayments(PlayerData data, ScenarioDefinition scenario, ScenarioChoice choice)
        {
            if (choice.instalmentCount <= 0 || choice.instalmentAmount <= 0f)
            {
                return;
            }

            int interval = Mathf.Max(1, choice.instalmentIntervalDays);
            ObligationDefaults.AddObligation(data, new Obligation
            {
                obligationId = $"{scenario.scenarioId}_{choice.choiceId}",
                label = string.IsNullOrWhiteSpace(choice.instalmentLabel) ? choice.label : choice.instalmentLabel,
                amount = choice.instalmentAmount,
                intervalDays = interval,
                nextDueDay = data.currentDay + interval,
                paymentsRemaining = choice.instalmentCount
            });
        }

        static void MarkScenarioCompleted(PlayerData data, string scenarioId)
        {
            foreach (var id in data.completedScenarioIds)
            {
                if (id == scenarioId)
                {
                    return;
                }
            }

            var updated = new string[data.completedScenarioIds.Length + 1];
            data.completedScenarioIds.CopyTo(updated, 0);
            updated[updated.Length - 1] = scenarioId;
            data.completedScenarioIds = updated;
        }

        static MaliDialogueController FindMaliDialogue()
        {
            GameObject maliObject = GameObject.Find("Mali");
            return maliObject != null ? maliObject.GetComponent<MaliDialogueController>() : null;
        }
    }
}
