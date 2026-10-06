using UnityEngine;

namespace MaliGo.UI
{
    /// <summary>
    /// Kept for compatibility (DESIGN_SPEC §7.1): <see cref="RequestInteract"/> forwards to
    /// <see cref="World.InteractionArbiter.RequestInteract"/>, which owns the one interact request and clears it
    /// every frame. <see cref="ConsumeInteractRequest"/> still answers "was interact requested", but only for the
    /// frame of the request and the next one, so a stale tap never fires later.
    /// </summary>
    public static class MobileInputBridge
    {
        const int NoRequest = int.MinValue / 2;

        static int requestFrame = NoRequest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            requestFrame = NoRequest;
        }

        public static void RequestInteract()
        {
            requestFrame = Time.frameCount;
            World.InteractionArbiter.RequestInteract();
        }

        /// <summary>True at most once per request, and only in the request's frame or the next.</summary>
        public static bool ConsumeInteractRequest()
        {
            bool fresh = Time.frameCount - requestFrame <= 1;
            requestFrame = NoRequest;
            return fresh;
        }
    }
}
