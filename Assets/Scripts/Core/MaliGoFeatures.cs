namespace MaliGo.Core
{
    /// <summary>
    /// Feature switches (design spec 7.12), so a plain default experience can be compared against later.
    /// Static fields, not consts, so a later A/B layer can set them before the bootstrap runs.
    /// </summary>
    public static class MaliGoFeatures
    {
        /// <summary>Off: a plain Mali line instead of the end-of-day reveal.</summary>
        public static bool EndOfDayReveal = true;

        /// <summary>Off: text appears instantly.</summary>
        public static bool Typewriter = true;

        /// <summary>Off: AudioManager is not created.</summary>
        public static bool Audio = true;

        /// <summary>Off: every scenario is active from Day 1, and the shift has no gate.</summary>
        public static bool ChapterSchedule = true;

        /// <summary>Off: no coach marks.</summary>
        public static bool FirstRunGuide = true;

        /// <summary>Off: character creation skips the profile screen; the default profile (food + taxi) is kept.</summary>
        public static bool ProfileTaps = true;

        /// <summary>The A/B switch for onboarding orientation. On: character creation runs in portrait (the soft
        /// keyboard no longer covers the name field) and the app turns to landscape when the world opens. Off: the
        /// whole app, onboarding included, stays landscape as before.</summary>
        public static bool PortraitOnboarding = true;
    }
}
