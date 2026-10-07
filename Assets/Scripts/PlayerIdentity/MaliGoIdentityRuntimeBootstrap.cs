using MaliGo.Characters;
using MaliGo.PlayerIdentity;
using MaliGo.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaliGo.PlayerIdentity
{
    /// <summary>
    /// Runtime wiring of every system into both scenes (DESIGN_SPEC §8 WP9), so the .unity files stay untouched.
    /// Each step is isolated by <see cref="Step"/> and is idempotent, so wiring the same scene twice is harmless.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MaliGoIdentityRuntimeBootstrap : MonoBehaviour
    {
        public const string WorldSceneName = "MaliGoWorld";
        public const string CharacterCreationSceneName = "CharacterCreation";

        // The scene instance wired last and the frame it was wired in: the boot scene can reach WireScene both
        // directly and through sceneLoaded in the same frame; it is wired once.
        static int lastWiredHandle;
        static int lastWiredFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lastWiredHandle = 0;
            lastWiredFrame = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootstrapAfterSceneLoad()
        {
            // AfterSceneLoad fires exactly once, for whichever scene the app boots into.
            // In a build that is CharacterCreation, so the world - which is reached later via
            // SceneManager.LoadScene - would never get wired: no player spawner, no mobile
            // controls, no scenario manager. Subscribing to sceneLoaded covers every scene
            // after the first. (In the Editor this was masked by pressing Play with
            // MaliGoWorld already open, which made the world the boot scene.)
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            WireScene(SceneManager.GetActiveScene());
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            WireScene(scene);
        }

        static void WireScene(Scene scene)
        {
            if (scene.handle == lastWiredHandle && Time.frameCount == lastWiredFrame)
            {
                return;
            }

            lastWiredHandle = scene.handle;
            lastWiredFrame = Time.frameCount;

            Step("player data", GameFlowController.EnsurePlayerDataManager);
            Step("event system", () => MaliGo.UI.EventSystemUtility.EnsureEventSystem());

            if (scene.name == WorldSceneName)
            {
                WireWorldScene();
            }
            else if (scene.name == CharacterCreationSceneName)
            {
                WireCharacterCreationScene();
            }
        }

        static void WireWorldScene()
        {
            // Order (DESIGN_SPEC §8 WP9). Each step is isolated: a throw in one never strands the player in a world
            // they can look at but not move in.
            Step("app lifecycle", MaliGo.App.AppLifecycle.Apply);

            // The world and everything over it (pause, reveal, chapter end) is landscape only.
            Step("orientation", MaliGo.App.OrientationLock.ForGame);

            Step("ui router", MaliGo.UI.Kit.UiModal.EnsureRouter);

            // The scene's old HUD canvas is replaced by HudView (§7.4).
            Step("hide scene HUD", () =>
            {
                GameObject sceneHud = GameObject.Find("MaliGo_Canvas");
                if (sceneHud != null)
                {
                    sceneHud.SetActive(false);
                }
            });

            Step("player spawner", () =>
            {
                if (Object.FindFirstObjectByType<PlayerCharacterSpawner>() == null)
                {
                    EnsureSystemsObject().AddComponent<PlayerCharacterSpawner>();
                }
            });

            Step("interaction", () =>
            {
                MaliGo.World.InteractionArbiter.Ensure(EnsureSystemsObject());
                MaliGo.UI.WorldPromptView.Ensure();
            });

            Step("mobile controls", () =>
            {
                if (Object.FindFirstObjectByType<MaliGo.UI.MobileControlsUI>() == null)
                {
                    new GameObject("MobileControls").AddComponent<MaliGo.UI.MobileControlsUI>();
                }
            });

            Step("game flow", () =>
            {
                if (Object.FindFirstObjectByType<GameFlowController>() == null)
                {
                    EnsureSystemsObject().AddComponent<GameFlowController>();
                }
            });

            Step("HUD", () => MaliGo.UI.HudView.Ensure());

            Step("scenarios", () =>
            {
                ScenarioWorldWiring.EnsureScenarioManager(EnsureSystemsObject());
                ScenarioWorldWiring.EnsureAllScenarioTriggers();
            });

            Step("world locations", MaliGo.World.WorldLocationWiring.EnsureLocations);

            Step("day flow", () => MaliGo.World.DayFlowController.Ensure());

            Step("pause", () => MaliGo.UI.PauseMenuView.Ensure());

            Step("audio", EnsureAudio);

            Step("first run", () =>
            {
                if (MaliGo.Core.MaliGoFeatures.FirstRunGuide && !MaliGo.Settings.GameSettings.FirstRunDone)
                {
                    MaliGo.UI.FirstRunGuide.Ensure();
                }
            });
        }

        static void WireCharacterCreationScene()
        {
            Step("app lifecycle", MaliGo.App.AppLifecycle.Apply);

            Step("ui router", MaliGo.UI.Kit.UiModal.EnsureRouter);

            Step("audio", EnsureAudio);

            Step("character creation", () =>
            {
                // A returning player with a valid save should never see character creation again -
                // without this, CharacterCreation as the boot scene would force it on every launch.
                if (PlayerDataManager.Instance != null && PlayerDataManager.Instance.IsCharacterCreated)
                {
                    // Normally already landscape from OrientationLock's boot lock; confirmed before the world loads.
                    MaliGo.App.OrientationLock.ForGame();
                    GameFlowController.LoadWorldScene();
                    return;
                }

                if (Object.FindFirstObjectByType<CharacterCreationUI>() == null)
                {
                    var bootstrap = new GameObject("CharacterCreationBootstrap");
                    bootstrap.AddComponent<CharacterCreationUI>();
                }
            });
        }

        static void EnsureAudio()
        {
            if (MaliGo.Core.MaliGoFeatures.Audio)
            {
                MaliGo.Sound.AudioManager.Ensure();
            }
        }

        static GameObject EnsureSystemsObject()
        {
            return GameObject.Find("MaliGo_Systems") ?? new GameObject("MaliGo_Systems");
        }

        /// <summary>
        /// Runs one wiring step, keeping a failure local. There is no console on a test
        /// device, so the failure is logged in a form that shows up under `adb logcat`
        /// rather than vanishing.
        /// </summary>
        static void Step(string label, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MaliGoBootstrap] Wiring step '{label}' failed, continuing with the rest: {ex}");
            }
        }
    }
}
