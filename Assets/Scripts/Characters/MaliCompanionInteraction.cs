using MaliGo.World;
using UnityEngine;

namespace MaliGo.Characters
{
    /// <summary>
    /// "Talk to Mali" (DESIGN_SPEC §7.1, §4.8). An <see cref="IInteractable"/> with Companion priority: the arbiter
    /// offers Mali only when nothing else is in range and the player has stood still for a moment. It never
    /// polls input or builds its own prompt.
    /// </summary>
    public class MaliCompanionInteraction : MonoBehaviour, IInteractable
    {
        [Header("Interaction")]
        [SerializeField] float interactionRadius = 0.74f;

        [Header("References")]
        [SerializeField] MaliDialogueController dialogueController;
        [SerializeField] MaliNpcController maliController;

        public float InteractionRadius => interactionRadius;

        void Awake()
        {
            if (dialogueController == null)
            {
                dialogueController = GetComponent<MaliDialogueController>();
            }

            if (maliController == null)
            {
                maliController = GetComponent<MaliNpcController>();
            }

            if (dialogueController == null)
            {
                dialogueController = gameObject.AddComponent<MaliDialogueController>();
            }
        }

        void OnEnable()
        {
            InteractionArbiter.Register(this);
        }

        void OnDisable()
        {
            InteractionArbiter.Unregister(this);
        }

        // ------------------------------------------------------------------ IInteractable

        public Vector3 InteractPosition => transform.position;

        public float InteractRadius => interactionRadius;

        public InteractPriority Priority => InteractPriority.Companion;

        public bool IsAvailable => isActiveAndEnabled && dialogueController != null;

        public bool IsEnabled => true;

        public string PromptText => "Talk to Mali";

        public string ActionVerb => "Talk";

        public string PromptIcon => "mali";

        public void Interact()
        {
            if (dialogueController == null)
            {
                return;
            }

            // A line still on screen is dismissed first (the passing box would otherwise stay up); the next tap
            // greets.
            if (dialogueController.IsShowingDialogue)
            {
                dialogueController.Hide();
                return;
            }

            dialogueController.ShowGreeting();
            maliController?.LookTowardPlayer();
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
