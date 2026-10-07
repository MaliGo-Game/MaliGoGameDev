using UnityEngine;

namespace MaliGo.World
{
    /// <summary>World things compete for the one prompt by priority: any World candidate in range beats a Companion.
    /// Mali herself is no longer an interactable (she is docked in the HUD, see <c>MaliCompanionInteraction</c>);
    /// Companion is kept for anything that follows the player in future.</summary>
    public enum InteractPriority
    {
        World = 0,
        Companion = 1
    }

    /// <summary>
    /// Something the player can walk up to and use (DESIGN_SPEC §7.1): Home, Bank, Work or a scenario spot.
    /// (Talking to Mali goes through the HUD Talk button instead, so it never needs a walk.)
    /// Implementations register with <see cref="InteractionArbiter.Register"/> in <c>OnEnable</c> and unregister in
    /// <c>OnDisable</c>. They never poll input or build prompt UI themselves: the arbiter picks one current
    /// interactable, <c>WorldPromptView</c> shows it and the arbiter calls <see cref="Interact"/> or
    /// <see cref="OnDisabledTap"/> when the player taps the prompt, the action button or E.
    /// </summary>
    public interface IInteractable
    {
        Vector3 InteractPosition { get; }

        /// <summary>0.7 world units.</summary>
        float InteractRadius { get; }

        InteractPriority Priority { get; }

        /// <summary>False hides it entirely (e.g. a spot with no active scenario, or its own panel is open).</summary>
        bool IsAvailable { get; }

        /// <summary>False = shown greyed with a reason (Work done / too tired / not open yet).</summary>
        bool IsEnabled { get; }

        string PromptText { get; }

        /// <summary>"Open", "Look", "Work", "Talk".</summary>
        string ActionVerb { get; }

        /// <summary>Icon name in Resources/MaliGo/Icons, or "mali" for Mali's portrait.</summary>
        string PromptIcon { get; }

        /// <summary>Called when the player asks to interact and <see cref="IsEnabled"/> is true.</summary>
        void Interact();

        /// <summary>Called instead of <see cref="Interact"/> when <see cref="IsEnabled"/> is false: shows the reason
        /// line (Work: §4.2.2). A no-op for everything else.</summary>
        void OnDisabledTap();
    }
}
