using System;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// A door the player can use through the one prompt (<see cref="InteractionArbiter"/>): a building's street door
    /// ("Home · Go in") or a room's way out ("Outside · Go out"). Walking into the doorway works too
    /// (<see cref="BuildingInteriors"/> watches the doorways); the prompt is there for players who stop and tap. It
    /// only describes and forwards: <see cref="BuildingInteriors"/> decides when it is available and does the move.
    /// </summary>
    public class InteriorDoor : MonoBehaviour, IInteractable
    {
        string promptText = "";
        string actionVerb = "";
        string promptIcon = "";
        float radius = 0.3f;
        Func<bool> available;
        Action use;

        /// <summary>Sets what the prompt shows, how near the player must be, when it is offered and what it does.</summary>
        public void Configure(string prompt, string verb, string icon, float interactRadius, Func<bool> isAvailable, Action onUse)
        {
            promptText = prompt ?? "";
            actionVerb = verb ?? "";
            promptIcon = icon ?? "";
            radius = Mathf.Max(0.05f, interactRadius);
            available = isAvailable;
            use = onUse;
        }

        void OnEnable()
        {
            InteractionArbiter.Register(this);
        }

        void OnDisable()
        {
            InteractionArbiter.Unregister(this);
        }

        public Vector3 InteractPosition => transform.position;

        public float InteractRadius => radius;

        public InteractPriority Priority => InteractPriority.World;

        public bool IsAvailable => isActiveAndEnabled && use != null && (available == null || available());

        public bool IsEnabled => true;

        public string PromptText => promptText;

        public string ActionVerb => actionVerb;

        public string PromptIcon => promptIcon;

        public void Interact()
        {
            use?.Invoke();
        }

        public void OnDisabledTap()
        {
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.875f, 0.643f, 0.392f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
