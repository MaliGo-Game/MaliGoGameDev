using System.Collections.Generic;

namespace MaliGo.Dialogue
{
    /// <summary>
    /// Mali's fixed lines (design spec 4.2.1). Only the first meeting is left: every other greeting
    /// is built from the player's day by MaliGreeting, so Mali never judges or lectures.
    /// Tokens such as {name} are filled by MaliText.Fill.
    /// </summary>
    public static class MaliDialogueLibrary
    {
        public const string FirstMeetingId = "first_meeting";

        static List<MaliDialogueEntry> entries;

        public static IReadOnlyList<MaliDialogueEntry> AllEntries
        {
            get
            {
                EnsureBuilt();
                return entries;
            }
        }

        public static MaliDialogueEntry FindById(string dialogueId)
        {
            EnsureBuilt();
            foreach (var entry in entries)
            {
                if (entry.dialogueId == dialogueId)
                {
                    return entry;
                }
            }

            return null;
        }

        static void EnsureBuilt()
        {
            if (entries != null)
            {
                return;
            }

            entries = new List<MaliDialogueEntry>
            {
                new MaliDialogueEntry
                {
                    dialogueId = FirstMeetingId,
                    triggerType = MaliDialogueTriggerType.FirstMeeting,
                    line = "Hi {name}, I'm Mali. I'll be with you all week, all the way to payday."
                }
            };
        }
    }
}
