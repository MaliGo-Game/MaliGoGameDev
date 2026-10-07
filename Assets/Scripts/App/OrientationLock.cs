using System;
using System.IO;
using MaliGo.Core;
using MaliGo.PlayerIdentity;
using UnityEngine;

namespace MaliGo.App
{
    /// <summary>
    /// Screen orientation per phase. The Android build allows portrait and both landscapes
    /// (<c>MaliGoAndroidSetup</c>: AutoRotation, portrait upside down off); this class narrows that at runtime:
    /// <list type="bullet">
    /// <item>Onboarding (<see cref="ForOnboarding"/>): portrait while <see cref="MaliGoFeatures.PortraitOnboarding"/> is
    ///   on, so the soft keyboard sits under the name field instead of over it; landscape when it is off.</item>
    /// <item>Everything else (<see cref="ForGame"/>): the world, pause, reveal, chapter end, and returning players
    ///   who never see onboarding. Landscape only, either way up (auto-rotation limited to the two landscapes).</item>
    /// </list>
    /// The first lock runs before the splash screen and before any scene (<see cref="LockOnBoot"/>), guessing the
    /// phase from whether a save file exists, so a returning player never sees the app flip. The bootstrap and
    /// character creation then confirm the phase once they know it for certain. In the Editor setting
    /// <c>Screen.orientation</c> has no effect; the layouts follow the Game view's shape instead.
    /// </summary>
    public static class OrientationLock
    {
        /// <summary>True when onboarding should be shown in portrait (the A/B switch).</summary>
        public static bool OnboardingIsPortrait => MaliGoFeatures.PortraitOnboarding;

        /// <summary>True while the screen is taller than it is wide (whatever orientation was asked for).</summary>
        public static bool ScreenIsPortrait => Screen.height > Screen.width;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        static void LockOnBoot()
        {
            // No save on disk means character creation is next. A save that turns out to be unusable (an old
            // version, or both copies unreadable) also leads to character creation; CharacterCreationUI asks for
            // portrait itself in that rare case, which costs one rotation.
            bool onboardingNext;
            try
            {
                string path = PlayerDataManager.GetSavePath();
                onboardingNext = !File.Exists(path) && !File.Exists(path + PlayerDataManager.BackupSuffix);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OrientationLock] Could not look for the save at boot, assuming a returning player: " +
                                 ex.Message);
                onboardingNext = false;
            }

            if (onboardingNext)
            {
                ForOnboarding();
            }
            else
            {
                ForGame();
            }
        }

        /// <summary>Character creation: portrait when the A/B switch is on, otherwise the game's landscape.</summary>
        public static void ForOnboarding()
        {
            if (!OnboardingIsPortrait)
            {
                ForGame();
                return;
            }

            Apply(() =>
            {
                Screen.autorotateToPortrait = true;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = false;
                Screen.autorotateToLandscapeRight = false;
                Screen.orientation = ScreenOrientation.Portrait;
            });
        }

        /// <summary>
        /// Everything after onboarding: landscape only. When the screen is already landscape, auto-rotation is
        /// limited to the two landscapes, so the phone can be turned either way up. When it is still portrait
        /// (just after onboarding, or at boot with the phone held upright), landscape left is forced first,
        /// because auto-rotation alone does not always turn a portrait screen; the next call (every world load
        /// makes one) releases it to landscape auto-rotation.
        /// </summary>
        public static void ForGame()
        {
            Apply(() =>
            {
                Screen.autorotateToPortrait = false;
                Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = true;
                Screen.autorotateToLandscapeRight = true;
                bool portraitNow = ScreenIsPortrait || Screen.orientation == ScreenOrientation.Portrait ||
                                   Screen.orientation == ScreenOrientation.PortraitUpsideDown;
                Screen.orientation = portraitNow ? ScreenOrientation.LandscapeLeft : ScreenOrientation.AutoRotation;
            });
        }

        static void Apply(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[OrientationLock] Could not set the screen orientation: " + ex.Message);
            }
        }
    }
}
