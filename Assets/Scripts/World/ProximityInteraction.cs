using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Shared base for world locations that aren't a scenario (Home, Bank, Work). It is an
    /// <see cref="IInteractable"/>: it registers with the <see cref="InteractionArbiter"/> while enabled and never
    /// polls input or builds its own prompt (DESIGN_SPEC §7.1). <c>WorldPromptView</c> shows the prompt.
    /// </summary>
    public abstract class ProximityInteraction : MonoBehaviour, IInteractable
    {
        [SerializeField] float interactionRadius = 0.7f;
        [SerializeField] string promptText = "";
        [SerializeField] string actionVerb = "Open";
        [SerializeField] string promptIcon = "";

        protected abstract void OnInteract();

        /// <summary>Override to hide the prompt while e.g. this location's own panel is open.</summary>
        protected virtual bool CanInteract() => true;

        /// <summary>Sets the prompt text shown by the world prompt.</summary>
        protected void SetPrompt(string text)
        {
            promptText = text ?? "";
        }

        /// <summary>Sets the action button verb ("Open", "Work") and the prompt icon.</summary>
        protected void SetVerbAndIcon(string verb, string icon)
        {
            actionVerb = verb ?? "";
            promptIcon = icon ?? "";
        }

        void Awake()
        {
            OnAwake();
        }

        /// <summary>
        /// Override instead of Awake() - a subclass's own Awake() would hide this base Awake() (Unity calls only
        /// the most-derived one).
        /// </summary>
        protected virtual void OnAwake()
        {
        }

        protected virtual void OnEnable()
        {
            InteractionArbiter.Register(this);
        }

        protected virtual void OnDisable()
        {
            InteractionArbiter.Unregister(this);
        }

        // ------------------------------------------------------------------ IInteractable

        public virtual Vector3 InteractPosition => transform.position;

        public float InteractRadius => interactionRadius;

        public InteractPriority Priority => InteractPriority.World;

        public virtual bool IsAvailable => isActiveAndEnabled && CanInteract();

        public virtual bool IsEnabled => true;

        public virtual string PromptText => promptText;

        public virtual string ActionVerb => actionVerb;

        public virtual string PromptIcon => promptIcon;

        /// <summary>
        /// True for a location at a building's door (Home, Bank): using it reads as going inside, so the camera
        /// steps in toward the door while its sheet is open (<c>MaliGoCameraController</c>). The buildings are
        /// closed shells with no interior, so there is nothing to walk into.
        /// </summary>
        public virtual bool IsBuildingEntrance => false;

        public void Interact()
        {
            OnInteract();
        }

        /// <summary>A no-op unless the location can be greyed out (Work).</summary>
        public virtual void OnDisabledTap()
        {
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.259f, 0.520f, 0.780f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactionRadius);
        }
    }
}
