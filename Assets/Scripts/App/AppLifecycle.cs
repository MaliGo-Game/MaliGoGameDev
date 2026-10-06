using UnityEngine;

namespace MaliGo.App
{
    /// <summary>
    /// App-wide runtime settings (DESIGN_SPEC §0 "Frame rate", §7.13): 60 fps and the screen never dims while the
    /// game is open. <see cref="Apply"/> is called by the bootstrap in both scenes (WP9) and is safe to call any
    /// number of times. It also keeps one small DontDestroyOnLoad host that re-applies both values when the app
    /// comes back from the background (some Android builds reset the sleep timeout on resume).
    ///
    /// Saving on pause/quit is <c>PlayerDataManager.OnApplicationPause(true)</c> / <c>OnApplicationQuit</c>
    /// (§2.6, §7.15); this class does not write the save a second time.
    /// </summary>
    public static class AppLifecycle
    {
        public const int TargetFrameRate = 60;

        static Host host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            host = null;
        }

        /// <summary><c>Application.targetFrameRate = 60</c>, <c>Screen.sleepTimeout = SleepTimeout.NeverSleep</c>.</summary>
        public static void Apply()
        {
            ApplySettings();
            EnsureHost();
        }

        static void ApplySettings()
        {
            Application.targetFrameRate = TargetFrameRate;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        static void EnsureHost()
        {
            if (host != null)
            {
                return;
            }

            var go = new GameObject("AppLifecycle");
            Object.DontDestroyOnLoad(go);
            host = go.AddComponent<Host>();
        }

        sealed class Host : MonoBehaviour
        {
            void OnApplicationPause(bool paused)
            {
                if (!paused)
                {
                    ApplySettings();
                }
            }

            void OnApplicationFocus(bool focused)
            {
                if (focused)
                {
                    ApplySettings();
                }
            }

            void OnDestroy()
            {
                if (host == this)
                {
                    host = null;
                }
            }
        }
    }
}
