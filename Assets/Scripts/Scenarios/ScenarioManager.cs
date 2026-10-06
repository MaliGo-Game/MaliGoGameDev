using System.Collections.Generic;
using MaliGo.Characters;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// Opens the choice sheet for a scenario and resolves the chosen option (design spec 7.6): the choice is
    /// applied by ScenarioOutcome inside UpdatePlayerData (saved at once), then Mali reacts in the blocking box
    /// with a chip row built from the deltas. Mali's intro is shown inside the sheet, not in the dialogue box.
    /// One instance lives in MaliGo_Systems for the lifetime of the world scene.
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

        /// <summary>
        /// Returns false if a scenario is already open, this one is completed or not for this life chapter, or no
        /// choice is affordable (cannot happen with the chapter's content, 3.4). Otherwise opens the choice sheet.
        /// </summary>
        public bool TryBeginScenario(ScenarioDefinition scenario)
        {
            if (scenario == null || scenarioInProgress)
            {
                return false;
            }

            PlayerData player = PlayerDataAccess.GetCurrentPlayer();
            scenario = ForProfile(scenario, player);

            if (player != null && !scenario.IsAvailableForLifeChapter(player.currentLifeChapter))
            {
                return false;
            }

            // Each scenario is played once; replaying the windfall would hand out free money.
            if (player != null && player.IsScenarioCompleted(scenario.scenarioId))
            {
                return false;
            }

            if (player != null && !ScenarioOutcome.AnyAvailable(player, scenario))
            {
                return false;
            }

            scenarioInProgress = true;
            ScenarioDefinition opened = scenario;
            choiceUI.Show(opened, choice => ResolveChoice(opened, choice), () => scenarioInProgress = false);
            return true;
        }

        /// <summary>
        /// The trigger passes the profile's definition (ScenarioLibrary.Get(id, focus, travel), 7.5). Until every
        /// caller does, the same id is looked up again for the player's profile; Get caches, so this returns the
        /// very instance the caller passed when it was already the right one.
        /// </summary>
        static ScenarioDefinition ForProfile(ScenarioDefinition scenario, PlayerData player)
        {
            if (player?.spendingProfile == null || string.IsNullOrEmpty(scenario.scenarioId))
            {
                return scenario;
            }

            ScenarioDefinition profiled = ScenarioLibrary.Get(scenario.scenarioId, player.spendingProfile.focus,
                                                              player.spendingProfile.travel);
            return profiled ?? scenario;
        }

        void ResolveChoice(ScenarioDefinition scenario, ScenarioChoice choice)
        {
            scenarioInProgress = false;
            if (choice == null || PlayerDataManager.Instance == null)
            {
                return;
            }

            bool applied = false;
            string reaction = null;
            Dictionary<string, string> extra = null;
            string[] chips = null;

            PlayerDataManager.Instance.UpdatePlayerData(data =>
            {
                // Everything that depends on the day is read before the choice changes anything.
                reaction = ScenarioOutcome.ReactionFor(data, choice);
                extra = ScenarioOutcome.ReactionExtra(data, choice);
                chips = ScenarioOutcome.Chips(data, choice);
                applied = ScenarioOutcome.Apply(data, scenario, choice);
            }, saveImmediately: true);

            if (!applied)
            {
                Debug.LogWarning($"[ScenarioManager] '{scenario.scenarioId}/{choice.choiceId}' could not be applied.");
                return;
            }

            MaliDialogueController maliDialogue = FindMaliDialogue();
            if (maliDialogue == null || string.IsNullOrWhiteSpace(reaction))
            {
                return;
            }

            // Tokens are filled after the change, so {cash} and {savings} show the new values.
            string line = MaliText.Fill(reaction, PlayerDataAccess.GetCurrentPlayer(), extra);
            maliDialogue.Say(line, MaliLineMode.Blocking, null, chips);
        }

        static MaliDialogueController FindMaliDialogue()
        {
            GameObject maliObject = GameObject.Find("Mali");
            return maliObject != null ? maliObject.GetComponent<MaliDialogueController>() : null;
        }
    }
}
