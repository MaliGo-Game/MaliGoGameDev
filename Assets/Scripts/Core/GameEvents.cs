using System;
using System.Collections.Generic;
using MaliGo.Data;
using MaliGo.Economy;
using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>
    /// Game-wide static events (design spec 7.3). Subscribers MUST unsubscribe in OnDestroy and MUST NOT
    /// call UpdatePlayerData/Save synchronously from a handler (defer to the next frame if a write is
    /// really needed).
    /// </summary>
    public static class GameEvents
    {
        static readonly List<MoneyEvent> pendingMoney = new List<MoneyEvent>();

        /// <summary>
        /// Raised by FlushMoneyChanged, once per queued event in order, after the mutator has finished
        /// and the save is written (design spec 2.4).
        /// </summary>
        public static event Action<MoneyEvent> MoneyChanged;

        /// <summary>Sleep confirm -&gt; DayFlowController.</summary>
        public static event Action SleepRequested;

        /// <summary>After DayCycle.EndDay, before the reveal (no sound hook).</summary>
        public static event Action<NightResult> NightEnded;

        /// <summary>Once per in-game day (raised only by DayFlowController.StartDay).</summary>
        public static event Action<int> DayStarted;

        public static event Action ChapterCompleted;

        /// <summary>HUD pause button.</summary>
        public static event Action PauseRequested;

        /// <summary>Mali starts any line (audio cue).</summary>
        public static event Action MaliSpoke;

        /// <summary>A view asks for a one-off clip by name.</summary>
        public static event Action<string> SoundRequested;

        /// <summary>Money events queued and not yet flushed.</summary>
        public static int PendingMoneyChangedCount => pendingMoney.Count;

        /// <summary>Called only by MoneyRecorder.Apply.</summary>
        public static void QueueMoneyChanged(MoneyEvent e)
        {
            if (e != null)
            {
                pendingMoney.Add(e);
            }
        }

        /// <summary>
        /// Raises MoneyChanged once per queued event, in order, and empties the queue. Called by
        /// PlayerDataManager.UpdatePlayerData (in a finally block) and by tests.
        /// </summary>
        public static void FlushMoneyChanged()
        {
            if (pendingMoney.Count == 0)
            {
                return;
            }

            MoneyEvent[] queued = pendingMoney.ToArray();
            pendingMoney.Clear();
            foreach (MoneyEvent e in queued)
            {
                Invoke(MoneyChanged, e);
            }
        }

        /// <summary>Drops queued money events without raising them (e.g. after a failed mutator in a test).</summary>
        public static void ClearPendingMoneyChanged()
        {
            pendingMoney.Clear();
        }

        public static void RaiseSleepRequested() => Invoke(SleepRequested);
        public static void RaiseNightEnded(NightResult result) => Invoke(NightEnded, result);
        public static void RaiseDayStarted(int day) => Invoke(DayStarted, day);
        public static void RaiseChapterCompleted() => Invoke(ChapterCompleted);
        public static void RaisePauseRequested() => Invoke(PauseRequested);
        public static void RaiseMaliSpoke() => Invoke(MaliSpoke);
        public static void RaiseSoundRequested(string clipName) => Invoke(SoundRequested, clipName);

        // One subscriber that throws must not stop the others.
        static void Invoke(Action handlers)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        static void Invoke<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(value);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
