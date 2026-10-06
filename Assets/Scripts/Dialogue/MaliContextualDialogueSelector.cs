using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.Data;

namespace MaliGo.Dialogue
{
    /// <summary>
    /// Kept for existing callers; pure. Every greeting now comes from MaliGreeting (design spec 4.2.1), which
    /// describes the player's day instead of judging their behaviour. The returned entry's line is a
    /// template: fill it with MaliText.Fill(line, data, extra) after formatting extra["owed"].
    /// </summary>
    public static class MaliContextualDialogueSelector
    {
        public static MaliDialogueEntry SelectLine(PlayerData data)
        {
            return SelectLine(data, out _);
        }

        public static MaliDialogueEntry SelectLine(PlayerData data, out Dictionary<string, string> extra)
        {
            string line = MaliGreeting.Build(data, MaliGoFeatures.ChapterSchedule, out extra);
            bool firstMeeting = data != null && !data.hasMetMali;
            return new MaliDialogueEntry
            {
                dialogueId = firstMeeting ? MaliDialogueLibrary.FirstMeetingId : "greeting",
                triggerType = firstMeeting ? MaliDialogueTriggerType.FirstMeeting : MaliDialogueTriggerType.DefaultGreeting,
                line = line
            };
        }
    }
}
