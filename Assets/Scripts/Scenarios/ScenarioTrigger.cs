using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// A physical world location the player walks up to and interacts with to receive
    /// a scenario. Interaction pattern mirrors MaliCompanionInteraction (press E in range)
    /// so scenario locations feel consistent with talking to Mali.
    /// </summary>
    public class ScenarioTrigger : MonoBehaviour
    {
        [Header("Scenario")]
        [SerializeField] string scenarioId = "";

        [Header("Interaction")]
        [SerializeField] float interactionRadius = 0.61f;
        [SerializeField] KeyCode interactKey = KeyCode.E;
        [SerializeField] string promptText = "Press E";

        Transform playerTransform;
        GameObject promptRoot;
        UnityEngine.UI.Text promptLabel;

        public void Configure(string newScenarioId, string newPromptText = null)
        {
            scenarioId = newScenarioId;
            if (!string.IsNullOrWhiteSpace(newPromptText))
            {
                promptText = newPromptText;
                if (promptLabel != null)
                {
                    promptLabel.text = promptText;
                }
            }
        }

        void Awake()
        {
            BuildPromptUi();
        }

        void Update()
        {
            // A finished scenario's location goes quiet for good, including after a reload,
            // because completion is read from the saved player data.
            if (IsCompleted())
            {
                if (promptRoot != null && promptRoot.activeSelf)
                {
                    promptRoot.SetActive(false);
                }
                return;
            }

            RefreshPlayerReference();
            bool inRange = IsPlayerInRange();
            bool scenarioBusy = ScenarioManager.Instance != null && ScenarioManager.Instance.IsScenarioInProgress;

            if (promptRoot != null)
            {
                promptRoot.SetActive(inRange && !scenarioBusy);
            }

            if (inRange && !scenarioBusy && WasInteractPressed())
            {
                TryTrigger();
            }
        }

        bool IsCompleted()
        {
            var player = PlayerIdentity.PlayerDataAccess.GetCurrentPlayer();
            return player != null && player.IsScenarioCompleted(scenarioId);
        }

        void RefreshPlayerReference()
        {
            if (playerTransform != null)
            {
                return;
            }

            GameObject player = GameObject.FindWithTag("Player");
            playerTransform = player != null ? player.transform : null;
        }

        bool IsPlayerInRange()
        {
            if (playerTransform == null)
            {
                return false;
            }

            Vector3 flatDelta = playerTransform.position - transform.position;
            flatDelta.y = 0f;
            return flatDelta.sqrMagnitude <= interactionRadius * interactionRadius;
        }

        void TryTrigger()
        {
            var scenario = ScenarioLibrary.GetById(scenarioId);
            if (scenario == null)
            {
                Debug.LogWarning($"[ScenarioTrigger] No scenario registered for id '{scenarioId}'.");
                return;
            }

            if (ScenarioManager.Instance == null)
            {
                Debug.LogWarning("[ScenarioTrigger] No ScenarioManager in scene.");
                return;
            }

            ScenarioManager.Instance.TryBeginScenario(scenario);
        }

        bool WasInteractPressed()
        {
            if (UI.MobileInputBridge.ConsumeInteractRequest())
            {
                return true;
            }

#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(interactKey);
#else
            return false;
#endif
        }

        void BuildPromptUi()
        {
            var canvasObject = new GameObject("ScenarioPrompt_Canvas");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 15;

            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            promptRoot = new GameObject("ScenarioPrompt");
            promptRoot.transform.SetParent(canvasObject.transform, false);

            var rect = promptRoot.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 210f);
            rect.sizeDelta = new Vector2(420f, 36f);

            var image = promptRoot.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0.059f, 0.369f, 0.180f, 0.88f);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                        ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var textObject = new GameObject("PromptText");
            textObject.transform.SetParent(promptRoot.transform, false);
            var textRect = textObject.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 4f);
            textRect.offsetMax = new Vector2(-12f, -4f);

            promptLabel = textObject.AddComponent<UnityEngine.UI.Text>();
            promptLabel.font = font;
            promptLabel.fontSize = 18;
            promptLabel.alignment = TextAnchor.MiddleCenter;
            promptLabel.color = new Color(0.976f, 1f, 0.965f);
            promptLabel.text = promptText;

            promptRoot.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.875f, 0.643f, 0.392f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
