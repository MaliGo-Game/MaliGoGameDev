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

        /// <summary>Off (the beta default): onboarding has no "Connect your bank" step, so the two profile taps run
        /// exactly as before. On: an optional step before the taps (consent, then, with no live provider registered,
        /// a clearly labelled sample person) that pre-fills the taps from a habit summary (docs/BANK_FEED.md).
        /// Needs <see cref="ProfileTaps"/>.</summary>
        public static bool BankFeedOnboarding = false;

        /// <summary>On (the beta default): the car parked in the player's driveway can be driven ("Drive" when beside
        /// it, "Get out" to park), and the streets around the town are built for it. Anyone can drive in the beta;
        /// owning, fuelling and insuring a car are future money hooks (the travel profile's "car" choice), not charged.
        /// Off: the car stays a parked prop.</summary>
        public static bool Driving = true;

        /// <summary>On: the ring road, the streets joining it and the houses and shops along them are built around the
        /// original town at runtime (TownExpansion). Off: the town is exactly the scene's.</summary>
        public static bool TownExpansion = true;

        /// <summary>Developer toggle: the sample persona the bank step starts with ("" = the first one; ids in
        /// MaliGo.BankFeed.SampleTransactionSource). The summary screen can also cycle through them.</summary>
        public static string BankFeedSamplePersona = "";
    }
}
