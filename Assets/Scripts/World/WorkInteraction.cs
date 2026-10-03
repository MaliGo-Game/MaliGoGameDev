using MaliGo.Characters;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Work: go to work, receive income, Mali reacts. No panel - it's an action, not a
    /// decision with trade-offs. One shift per in-game day, and a shift needs energy, so
    /// income is limited and skipping meals or walking everywhere has a cost you can feel.
    /// </summary>
    public class WorkInteraction : ProximityInteraction
    {
        const float IncomeAmount = 150f;
        const float EnergyCost = 20f;

        const string ReadyPrompt = "Press E to work";
        // Short enough to fit the 420px prompt box at font size 18.
        const string DonePrompt = "Done for today - sleep at home";

        bool showingDonePrompt;

        protected override void OnAwake()
        {
            SetPrompt(ReadyPrompt);
        }

        protected override bool CanInteract()
        {
            // Stays interactable after the shift so the player is told why there's no more
            // work today, instead of the prompt silently disappearing.
            bool worked = HasWorkedToday(PlayerDataAccess.GetCurrentPlayer());
            if (worked != showingDonePrompt)
            {
                showingDonePrompt = worked;
                SetPrompt(worked ? DonePrompt : ReadyPrompt);
            }

            return true;
        }

        protected override void OnInteract()
        {
            PlayerData player = PlayerDataAccess.GetCurrentPlayer();
            if (PlayerDataManager.Instance == null || player?.financialStats == null)
            {
                return;
            }

            if (HasWorkedToday(player))
            {
                ShowMaliLine("That's today's shift done, {0}. Sleep at home and there's work again tomorrow.");
                return;
            }

            if (player.financialStats.energy < EnergyCost)
            {
                ShowMaliLine("Not enough energy left for a shift today, {0}. Food and sleep bring it back.");
                return;
            }

            PlayerDataManager.Instance.UpdatePlayerData(data =>
            {
                var stats = data.financialStats;
                stats.cash += IncomeAmount;
                stats.energy = Mathf.Clamp(stats.energy - EnergyCost, 0f, 100f);
                stats.financialXP += 5f;
                data.lastWorkedDay = data.currentDay;
            }, saveImmediately: true);

            ShowMaliLine($"R{IncomeAmount:0} for the day's work, {{0}}. What you do with it is up to you.");
        }

        static bool HasWorkedToday(PlayerData player)
        {
            return player != null && player.lastWorkedDay >= player.currentDay;
        }

        static void ShowMaliLine(string template)
        {
            GameObject maliObject = GameObject.Find("Mali");
            MaliDialogueController dialogue = maliObject != null ? maliObject.GetComponent<MaliDialogueController>() : null;
            dialogue?.ShowFormatted(template, PlayerDataAccess.GetCharacterName());
        }
    }
}
