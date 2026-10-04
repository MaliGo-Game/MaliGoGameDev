using System;
using UnityEngine;

namespace MaliGo.Settings
{
    /// <summary>
    /// Player settings in PlayerPrefs (DESIGN_SPEC §2.6). They survive a save reset: deleting the save
    /// never touches these keys. Every change is written at once (<see cref="Save"/>) and raises
    /// <see cref="Changed"/>, so sound and music apply live.
    ///
    /// | Key                    | Type        | Default | Meaning                                              |
    /// | maligo.sound           | int 0/1     | 1       | UI sounds, Mali cue, jingles, money sound, ambience  |
    /// | maligo.music           | int 0/1     | 1       | Music loop                                           |
    /// | maligo.textSpeed       | int 0/1/2   | 1       | Slow 25 cps / Normal 45 cps / Instant                |
    /// | maligo.reduceMotion    | int 0/1     | 0       | Motion becomes 150 ms crossfades, numbers jump       |
    /// | maligo.firstRunDone    | int 0/1     | 0       | All coach marks seen or skipped                      |
    /// | maligo.firstRunSteps   | int bitmask | 0       | bit 0..3 = coach mark 1..4 shown                     |
    /// </summary>
    public static class GameSettings
    {
        public const string KeySound = "maligo.sound";
        public const string KeyMusic = "maligo.music";
        public const string KeyTextSpeed = "maligo.textSpeed";
        public const string KeyReduceMotion = "maligo.reduceMotion";
        public const string KeyFirstRunDone = "maligo.firstRunDone";
        public const string KeyFirstRunSteps = "maligo.firstRunSteps";

        /// <summary><see cref="TextSpeed"/> values.</summary>
        public const int TextSpeedSlow = 0;
        public const int TextSpeedNormal = 1;
        public const int TextSpeedInstant = 2;

        public const int SlowCharsPerSecond = 25;
        public const int NormalCharsPerSecond = 45;

        /// <summary>Typewriter pause after <c>. ! ?</c> (seconds, DESIGN_SPEC §0).</summary>
        public const float PauseAfterSentence = 0.18f;
        /// <summary>Typewriter pause after a comma (seconds).</summary>
        public const float PauseAfterComma = 0.08f;

        /// <summary>Number of first-run coach marks (bits 0..3 of <see cref="FirstRunSteps"/>).</summary>
        public const int FirstRunStepCount = 4;

        /// <summary>Raised after any setting changes (already saved).</summary>
        public static event Action Changed;

        static bool loaded;
        static bool soundOn;
        static bool musicOn;
        static int textSpeed;
        static bool reduceMotion;
        static bool firstRunDone;
        static int firstRunSteps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            loaded = false;
            Changed = null;
        }

        /// <summary>UI sounds, Mali cue, jingles, money sound and ambience.</summary>
        public static bool SoundOn
        {
            get { EnsureLoaded(); return soundOn; }
            set { EnsureLoaded(); if (soundOn != value) { soundOn = value; Write(KeySound, value ? 1 : 0); } }
        }

        /// <summary>The music loop.</summary>
        public static bool MusicOn
        {
            get { EnsureLoaded(); return musicOn; }
            set { EnsureLoaded(); if (musicOn != value) { musicOn = value; Write(KeyMusic, value ? 1 : 0); } }
        }

        /// <summary>0 Slow, 1 Normal, 2 Instant (<see cref="TextSpeedSlow"/> etc.). Out-of-range values are clamped.</summary>
        public static int TextSpeed
        {
            get { EnsureLoaded(); return textSpeed; }
            set
            {
                EnsureLoaded();
                int clamped = Mathf.Clamp(value, TextSpeedSlow, TextSpeedInstant);
                if (textSpeed != clamped) { textSpeed = clamped; Write(KeyTextSpeed, clamped); }
            }
        }

        /// <summary>Motion becomes 150 ms crossfades and numbers jump.</summary>
        public static bool ReduceMotion
        {
            get { EnsureLoaded(); return reduceMotion; }
            set { EnsureLoaded(); if (reduceMotion != value) { reduceMotion = value; Write(KeyReduceMotion, value ? 1 : 0); } }
        }

        /// <summary>All coach marks seen or skipped.</summary>
        public static bool FirstRunDone
        {
            get { EnsureLoaded(); return firstRunDone; }
            set { EnsureLoaded(); if (firstRunDone != value) { firstRunDone = value; Write(KeyFirstRunDone, value ? 1 : 0); } }
        }

        /// <summary>Bitmask: bit 0..3 set once coach mark 1..4 has been shown.</summary>
        public static int FirstRunSteps
        {
            get { EnsureLoaded(); return firstRunSteps; }
            set { EnsureLoaded(); if (firstRunSteps != value) { firstRunSteps = value; Write(KeyFirstRunSteps, value); } }
        }

        /// <summary>Typewriter speed: 25 (Slow), 45 (Normal), or 0 for Instant (see <see cref="InstantText"/>).</summary>
        public static int CharsPerSecond
        {
            get
            {
                switch (TextSpeed)
                {
                    case TextSpeedSlow: return SlowCharsPerSecond;
                    case TextSpeedInstant: return 0;
                    default: return NormalCharsPerSecond;
                }
            }
        }

        /// <summary>True when text should appear at once (text speed Instant).</summary>
        public static bool InstantText => TextSpeed == TextSpeedInstant;

        /// <summary>True once coach mark <paramref name="step"/> (1..4) has been shown.</summary>
        public static bool HasFirstRunStep(int step)
        {
            return step >= 1 && step <= FirstRunStepCount && (FirstRunSteps & (1 << (step - 1))) != 0;
        }

        /// <summary>Records that coach mark <paramref name="step"/> (1..4) has been shown.</summary>
        public static void MarkFirstRunStep(int step)
        {
            if (step < 1 || step > FirstRunStepCount)
            {
                return;
            }
            FirstRunSteps = FirstRunSteps | (1 << (step - 1));
        }

        /// <summary>Writes PlayerPrefs to disk. Called after every change.</summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>Re-reads every key from PlayerPrefs (e.g. after another system wrote them).</summary>
        public static void Reload()
        {
            loaded = false;
            EnsureLoaded();
            Changed?.Invoke();
        }

        static void EnsureLoaded()
        {
            if (loaded)
            {
                return;
            }
            loaded = true;
            soundOn = PlayerPrefs.GetInt(KeySound, 1) != 0;
            musicOn = PlayerPrefs.GetInt(KeyMusic, 1) != 0;
            textSpeed = Mathf.Clamp(PlayerPrefs.GetInt(KeyTextSpeed, TextSpeedNormal), TextSpeedSlow, TextSpeedInstant);
            reduceMotion = PlayerPrefs.GetInt(KeyReduceMotion, 0) != 0;
            firstRunDone = PlayerPrefs.GetInt(KeyFirstRunDone, 0) != 0;
            firstRunSteps = Mathf.Max(0, PlayerPrefs.GetInt(KeyFirstRunSteps, 0));
        }

        static void Write(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Save();
            Changed?.Invoke();
        }
    }
}
