namespace MaliGo.Data
{
    /// <summary>
    /// Character-creation copy (design spec 4.6 and 4.8), kept here so its fit can be tested outside
    /// Unity. The card labels of the profile screen are in SpendingFocus.All and TravelMode.All; Mali's
    /// three paragraphs are not here.
    /// </summary>
    public static class OnboardingCopy
    {
        // Screen 1: the promise and the name.
        public const string PromiseLine1 = "Live the week before payday.";
        public const string PromiseLine2 = "See where your money goes.";
        public const string NameQuestion = "What should we call you?";
        public const string NamePlaceholder = "Your name";
        public const int NameMaxLength = 16;

        // Screen 2: the look.
        // The character cards' names are CharacterLooks.Outfits[i].label.
        public const string LookTitle = "Pick your look";

        // Screen 3: the spending profile (two taps).
        public const string SpendQuestion = "Where does most of your money go?";
        public const string TravelQuestion = "How do you usually get around?";
        public const string WeekBuiltLine = "We've built your week around where your money goes.";
        public const string WeekPlacesSeparator = " · ";

        // Screen 4: the goal.
        public const string GoalTitle = "What are you saving towards?";
        public const string GoalSavedCaption = "R400 saved so far";

        // Buttons.
        public const string NextButton = "Next";
        public const string BackButton = "Back";
        public const string LetsGoButton = "Let's go";

        /// <summary>Shown once on the first screen after an old save was discarded.</summary>
        public const string UpdatedNotice = "MaliGo has been updated — your story starts fresh.";
    }
}
