using System;
using MaliGo.Data;
using UnityEngine;

namespace MaliGo.Scenarios
{
    /// <summary>
    /// A single reusable, data-driven financial decision scenario. New scenarios are
    /// added by creating more instances of this type (via ScenarioLibrary, or later as
    /// [CreateAssetMenu] assets in the Editor) - never by writing a new scenario class.
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioDefinition", menuName = "MaliGo/Scenario Definition")]
    public class ScenarioDefinition : ScriptableObject
    {
        public string scenarioId = "";
        public string title = "";

        /// <summary>The situation text shown in the choice panel.</summary>
        public string description = "";
        public string locationHint = "";

        [Tooltip("Empty = available regardless of the player's current Life Chapter.")]
        public LifeChapter[] requiredLifeChapters = Array.Empty<LifeChapter>();

        [Tooltip("Mali's short line shown inside the choice panel. Tokens such as {name} are filled by MaliText.Fill.")]
        public string introDialogue = "";

        /// <summary>World spot this scenario appears at (ChapterSchedule spot id, e.g. "CORNER").</summary>
        public string spotId = "";

        /// <summary>World prompt text, e.g. "Lunch at the kota shop".</summary>
        public string promptText = "";

        /// <summary>Icon name (Resources/MaliGo/Icons) for the prompt and the choice sheet.</summary>
        public string promptIcon = "";

        /// <summary>Money category of the choices' money events (design spec 2.2).</summary>
        public string moneyCategory = "";

        /// <summary>Eyebrow caption of the choice sheet, e.g. "Kota shop".</summary>
        public string placeLabel = "";

        /// <summary>What the Work prompt waits for when this scenario is the day's first, e.g. "lunch".</summary>
        public string gateNoun = "";

        /// <summary>True = never scheduled; only set off by a choice (a follow-up).</summary>
        public bool isFollowUp;

        public ScenarioChoice[] choices = Array.Empty<ScenarioChoice>();

        public bool IsAvailableForLifeChapter(LifeChapter chapter)
        {
            if (requiredLifeChapters == null || requiredLifeChapters.Length == 0)
            {
                return true;
            }

            foreach (var required in requiredLifeChapters)
            {
                if (required == chapter)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
