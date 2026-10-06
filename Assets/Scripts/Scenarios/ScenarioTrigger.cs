using MaliGo.Core;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using MaliGo.World;
using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// One scenario spot in the world (DESIGN_SPEC §3.3, §7.5): an <see cref="IInteractable"/> that offers the head
    /// of its spot's queue, <c>ChapterSchedule.ActiveScenarioAtSpot</c> for the player's focus and follow-ups. The
    /// definition comes from <c>ScenarioLibrary.Get(id, focus, travel)</c>, so prompt, place and amounts follow the
    /// spending profile. Nothing active = unavailable (no prompt). It never polls input or builds its own prompt.
    ///
    /// <see cref="Configure"/> (a fixed scenario id) is kept for compatibility; the world uses
    /// <see cref="ConfigureSpot"/>.
    /// </summary>
    public class ScenarioTrigger : MonoBehaviour, IInteractable
    {
        const float RefreshSeconds = 0.5f;

        [Header("Scenario")]
        [SerializeField] string spotId = "";
        [SerializeField] string scenarioId = "";

        [Header("Interaction")]
        [SerializeField] float interactionRadius = 0.7f;
        [SerializeField] string promptText = "";

        ScenarioDefinition active;
        bool dirty = true;
        float nextRefreshTime;
        bool subscribed;

        public string SpotId => spotId;

        /// <summary>The definition this spot offers right now, or null.</summary>
        public ScenarioDefinition ActiveScenario
        {
            get
            {
                RefreshIfNeeded();
                return active;
            }
        }

        /// <summary>Legacy: a fixed scenario (no queue). Kept for compatibility.</summary>
        public void Configure(string newScenarioId, string newPromptText = null)
        {
            scenarioId = newScenarioId ?? "";
            if (!string.IsNullOrWhiteSpace(newPromptText))
            {
                promptText = newPromptText;
            }

            dirty = true;
        }

        /// <summary>Makes this trigger the spot <paramref name="newSpotId"/> (a ChapterSchedule spot id).</summary>
        public void ConfigureSpot(string newSpotId)
        {
            spotId = newSpotId ?? "";
            dirty = true;
        }

        void OnEnable()
        {
            dirty = true;
            TrySubscribe();
            InteractionArbiter.Register(this);
        }

        void OnDisable()
        {
            InteractionArbiter.Unregister(this);
            Unsubscribe();
        }

        void OnDestroy()
        {
            Unsubscribe();
        }

        void TrySubscribe()
        {
            if (subscribed || PlayerDataManager.Instance == null)
            {
                return;
            }

            PlayerDataManager.Instance.OnPlayerDataChanged += HandlePlayerDataChanged;
            subscribed = true;
        }

        void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.OnPlayerDataChanged -= HandlePlayerDataChanged;
            }

            subscribed = false;
        }

        void HandlePlayerDataChanged(PlayerData data)
        {
            dirty = true;
        }

        void RefreshIfNeeded()
        {
            if (!subscribed)
            {
                TrySubscribe();
            }

            if (!dirty && Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            dirty = false;
            nextRefreshTime = Time.unscaledTime + RefreshSeconds;
            active = Resolve(PlayerDataAccess.GetCurrentPlayer());
        }

        ScenarioDefinition Resolve(PlayerData data)
        {
            if (data == null)
            {
                return null;
            }

            string focus = data.spendingProfile?.focus;
            string travel = data.spendingProfile?.travel;

            if (!string.IsNullOrEmpty(spotId))
            {
                string id = ChapterSchedule.ActiveScenarioAtSpot(data, spotId, MaliGoFeatures.ChapterSchedule,
                    data.spendingProfile?.focus, data.followUps);
                return string.IsNullOrEmpty(id) ? null : ScenarioLibrary.Get(id, focus, travel);
            }

            if (!string.IsNullOrEmpty(scenarioId) && !data.IsScenarioCompleted(scenarioId))
            {
                return ScenarioLibrary.Get(scenarioId, focus, travel);
            }

            return null;
        }

        // ------------------------------------------------------------------ IInteractable

        public Vector3 InteractPosition => transform.position;

        public float InteractRadius => interactionRadius;

        public InteractPriority Priority => InteractPriority.World;

        public bool IsAvailable
        {
            get
            {
                if (!isActiveAndEnabled)
                {
                    return false;
                }

                bool busy = ScenarioManager.Instance != null && ScenarioManager.Instance.IsScenarioInProgress;
                return !busy && ActiveScenario != null;
            }
        }

        public bool IsEnabled => true;

        public string PromptText
        {
            get
            {
                ScenarioDefinition scenario = ActiveScenario;
                if (scenario == null)
                {
                    return promptText;
                }

                if (!string.IsNullOrWhiteSpace(scenario.promptText))
                {
                    return scenario.promptText;
                }

                return !string.IsNullOrWhiteSpace(scenario.title) ? scenario.title : promptText;
            }
        }

        public string ActionVerb => "Look";

        public string PromptIcon
        {
            get
            {
                ScenarioDefinition scenario = ActiveScenario;
                return scenario != null ? scenario.promptIcon : "";
            }
        }

        public void Interact()
        {
            dirty = true;
            ScenarioDefinition scenario = ActiveScenario;
            if (scenario == null)
            {
                return;
            }

            if (ScenarioManager.Instance == null)
            {
                Debug.LogWarning("[ScenarioTrigger] No ScenarioManager in scene.");
                return;
            }

            ScenarioManager.Instance.TryBeginScenario(scenario);
        }

        public void OnDisabledTap()
        {
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.875f, 0.643f, 0.392f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
