using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Dialogue;
using MaliGo.PlayerIdentity;
using MaliGo.UI.Kit;
using MaliGo.World;
using UnityEngine;

namespace MaliGo.Characters
{
    /// <summary>
    /// "Talk to Mali" (DESIGN_SPEC §7.1, §4.8). Mali no longer stands in the world: she is docked in the HUD above
    /// the Talk button (<c>MobileControlsUI</c>), so talking to her never needs a walk. This component lives on the
    /// hidden "Mali" host object next to <see cref="MaliDialogueController"/> and is the one entry point the Talk
    /// button and the docked figure call (<see cref="Talk"/>). It is not an <see cref="IInteractable"/> any more, so
    /// the world prompt never shows a duplicate "Talk to Mali" chip.
    ///
    /// <see cref="HasSomethingToSay"/> drives the Talk button's expanded "Talk to Mali" pill: true when the greeting
    /// she would give now (<see cref="MaliGreeting.Build"/>) is one the player has not heard yet today (first meeting,
    /// a new day, or what is waiting changed since the last talk), and no line of hers is on screen. The greeting is
    /// re-built at most every <see cref="RecheckSeconds"/>.
    /// </summary>
    public class MaliCompanionInteraction : MonoBehaviour
    {
        const float RecheckSeconds = 0.5f;

        [Header("References")]
        [SerializeField] MaliDialogueController dialogueController;

        static MaliCompanionInteraction instance;

        string heardGreeting;
        string cachedGreeting;
        float nextCheckAt;

        /// <summary>The companion of the loaded world, or null (no Mali host in this scene).</summary>
        public static MaliCompanionInteraction Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindFirstObjectByType<MaliCompanionInteraction>();
                }

                return instance;
            }
        }

        /// <summary>True when Mali can be talked to right now.</summary>
        public bool IsAvailable => isActiveAndEnabled && dialogueController != null;

        /// <summary>True when Mali has a greeting the player has not heard yet (see the class summary).</summary>
        public bool HasSomethingToSay
        {
            get
            {
                if (!IsAvailable || dialogueController.IsShowingDialogue)
                {
                    return false;
                }

                RefreshGreeting(false);
                return !string.IsNullOrEmpty(cachedGreeting) && cachedGreeting != heardGreeting;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        void Awake()
        {
            if (dialogueController == null)
            {
                dialogueController = GetComponent<MaliDialogueController>();
            }

            if (dialogueController == null)
            {
                dialogueController = gameObject.AddComponent<MaliDialogueController>();
            }
        }

        void OnEnable()
        {
            instance = this;
            GameEvents.DayStarted += HandleDayStarted;
        }

        void OnDisable()
        {
            GameEvents.DayStarted -= HandleDayStarted;
            if (instance == this)
            {
                instance = null;
            }
        }

        void Update()
        {
            // Keyboard: E talks to Mali when nothing in the world is offered (the arbiter owns E otherwise).
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame || UiModal.IsAnyOpen)
            {
                return;
            }

            InteractionArbiter arbiter = InteractionArbiter.Instance;
            if (arbiter != null && arbiter.Current != null && (arbiter.Current as Object) != null)
            {
                return;
            }

            Talk();
        }

        void HandleDayStarted(int day)
        {
            heardGreeting = null;
            nextCheckAt = 0f;
        }

        /// <summary>
        /// Talk to Mali: a line of hers still on screen is dismissed first (the passing box would otherwise stay
        /// up); otherwise she greets. The greeting given is remembered as heard, so the Talk button collapses.
        /// </summary>
        public void Talk()
        {
            if (!IsAvailable)
            {
                return;
            }

            if (dialogueController.IsShowingDialogue)
            {
                dialogueController.Hide();
                return;
            }

            dialogueController.ShowGreeting();

            // Built after the greeting: the first meeting marks her as met, which changes what she would say next,
            // and the button should not re-expand straight after the introduction.
            RefreshGreeting(true);
            heardGreeting = cachedGreeting;
        }

        void RefreshGreeting(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && now < nextCheckAt)
            {
                return;
            }

            nextCheckAt = now + RecheckSeconds;
            PlayerData player = PlayerDataAccess.GetCurrentPlayer();
            if (player == null)
            {
                cachedGreeting = null;
                return;
            }

            try
            {
                cachedGreeting = MaliGreeting.Build(player, MaliGoFeatures.ChapterSchedule, out Dictionary<string, string> extra)
                                 + Join(extra);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[MaliCompanion] Could not build the greeting: {ex}");
                cachedGreeting = null;
            }
        }

        static string Join(Dictionary<string, string> extra)
        {
            if (extra == null || extra.Count == 0)
            {
                return "";
            }

            var keys = new List<string>(extra.Keys);
            keys.Sort(System.StringComparer.Ordinal);
            var builder = new System.Text.StringBuilder();
            foreach (string key in keys)
            {
                builder.Append('|').Append(key).Append('=').Append(extra[key]);
            }

            return builder.ToString();
        }
    }
}
