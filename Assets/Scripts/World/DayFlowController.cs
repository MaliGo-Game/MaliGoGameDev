using System.Collections;
using MaliGo.Copy;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Economy;
using MaliGo.PlayerIdentity;
using MaliGo.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaliGo.World
{
    /// <summary>
    /// Owns the night (DESIGN_SPEC §7.3): Sleep → <see cref="DayCycle.EndDay"/> → the reveal → the next day or the
    /// chapter end. Also shows a reveal that was pending when the app was killed (rebuilt from the save with
    /// <see cref="DayCycle.Rebuild"/>), the chapter end while <c>chapter.complete</c>, and starts each day once
    /// (<see cref="StartDay"/> is the only place <see cref="GameEvents.DayStarted"/> is raised).
    /// Created once per world scene by <see cref="Ensure"/> (wired by the bootstrap, WP9).
    /// </summary>
    public class DayFlowController : MonoBehaviour
    {
        public const string WorldSceneName = "MaliGoWorld";
        const float MorningDelay = 1.0f;

        static DayFlowController instance;

        EndOfDayRevealView reveal;
        ChapterEndView chapterEnd;
        bool busy;
        bool restarting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>Creates the controller (and its reveal and chapter-end views) once per scene.</summary>
        public static DayFlowController Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            DayFlowController existing = FindFirstObjectByType<DayFlowController>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return new GameObject("DayFlowController").AddComponent<DayFlowController>();
        }

        /// <summary>True from Sleep until the next day has started (or the chapter end is up).</summary>
        public bool IsBusy => busy;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            reveal = GetComponent<EndOfDayRevealView>();
            if (reveal == null)
            {
                reveal = gameObject.AddComponent<EndOfDayRevealView>();
            }

            chapterEnd = GetComponent<ChapterEndView>();
            if (chapterEnd == null)
            {
                chapterEnd = gameObject.AddComponent<ChapterEndView>();
            }
            GameEvents.SleepRequested += HandleSleepRequested;
        }

        void OnDestroy()
        {
            GameEvents.SleepRequested -= HandleSleepRequested;
            if (instance == this)
            {
                instance = null;
            }
        }

        // ================================================================ world load (§7.3 step 4)

        void Start()
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data == null)
            {
                return;
            }

            if (data.revealPendingForDay > 0)
            {
                NightResult night = DayCycle.Rebuild(data, data.revealPendingForDay);
                if (night != null)
                {
                    busy = true;
                    ShowReveal(data, night);
                    return;
                }

                Write(d => d.revealPendingForDay = 0);
                data = PlayerDataAccess.GetCurrentPlayer();
                if (data == null)
                {
                    return;
                }
            }

            if (data.chapter != null && data.chapter.complete)
            {
                ShowChapterEnd(data);
                return;
            }

            if (data.morningLineDay != data.currentDay)
            {
                int day = data.currentDay;
                MaliGo.UI.Kit.UiTween.Delay(this, MorningDelay, () => StartDay(day));
            }
        }

        // ================================================================ the night (§7.3 steps 1-3)

        void HandleSleepRequested()
        {
            if (busy || restarting)
            {
                return;
            }

            busy = true;
            // Never write the save from inside a GameEvents handler (§2.4): end the day on the next frame.
            StartCoroutine(EndDayNextFrame());
        }

        IEnumerator EndDayNextFrame()
        {
            yield return null;

            PlayerDataManager manager = PlayerDataManager.Instance;
            if (manager == null || manager.CurrentPlayer == null)
            {
                busy = false;
                yield break;
            }

            NightResult result = null;
            manager.UpdatePlayerData(d => result = DayCycle.EndDay(d), saveImmediately: true);
            if (result == null)
            {
                busy = false; // double trigger or a closed day: nothing happened
                yield break;
            }

            GameEvents.RaiseNightEnded(result);
            ShowReveal(manager.CurrentPlayer, result);
        }

        void ShowReveal(PlayerData data, NightResult night)
        {
            if (MaliGoFeatures.EndOfDayReveal)
            {
                reveal.Show(data, night, OnRevealDismissed);
            }
            else
            {
                reveal.ShowPlain(RevealLineBuilder.PlainLine(night.record), RevealLineBuilder.ButtonLabel(night.record), OnRevealDismissed);
            }
        }

        void OnRevealDismissed()
        {
            Write(d => d.revealPendingForDay = 0);

            // Read the save, never the NightResult: after a restart there is none (§7.3 step 3).
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data != null && data.chapter != null && data.chapter.complete)
            {
                GameEvents.RaiseChapterCompleted();
                ShowChapterEnd(data);
                busy = false;
                return;
            }

            if (data != null)
            {
                StartDay(data.currentDay);
            }

            busy = false;
        }

        // ================================================================ days (§7.3 step 5)

        /// <summary>
        /// Marks day <paramref name="n"/> as started (<c>morningLineDay = n</c>, saved) and raises
        /// <see cref="GameEvents.DayStarted"/>: the morning line, the replay's plan quote and the jingle. Does
        /// nothing if that day has already started, so DayStarted runs once per in-game day.
        /// </summary>
        public void StartDay(int n)
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            if (data == null || n <= 0 || data.morningLineDay == n || restarting)
            {
                return;
            }

            if (data.chapter != null && data.chapter.complete)
            {
                return;
            }

            Write(d => d.morningLineDay = n);
            GameEvents.RaiseDayStarted(n);
        }

        // ================================================================ chapter end (§7.3 step 6)

        void ShowChapterEnd(PlayerData data)
        {
            chapterEnd.Show(data, LiveTheWeekAgain);
        }

        void LiveTheWeekAgain()
        {
            if (restarting)
            {
                return;
            }

            restarting = true;
            Write(ChapterFlow.RestartChapter);
            Time.timeScale = 1f;
            SceneManager.LoadScene(WorldSceneName);
        }

        static void Write(System.Action<PlayerData> mutator)
        {
            PlayerDataManager.Instance?.UpdatePlayerData(mutator, saveImmediately: true);
        }
    }
}
