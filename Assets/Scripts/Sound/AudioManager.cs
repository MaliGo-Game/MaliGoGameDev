using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.Settings;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaliGo.Sound
{
    /// <summary>
    /// All of MaliGo's sound (DESIGN_SPEC §7.11, D20). A DontDestroyOnLoad singleton created by the bootstrap in
    /// both scenes when <c>MaliGoFeatures.Audio</c> is on (<see cref="Ensure"/>).
    ///
    /// Sources: one UI one-shot source, two music sources and two ambience sources. Each loop alternates between
    /// its pair: the next copy is scheduled on the other source with <c>AudioSource.PlayScheduled</c> at
    /// (dspTime at the current copy's start + clip length - 2.0 s), and the pair is crossfaded over those 2.0 s
    /// by reading <c>AudioSettings.dspTime</c> every frame. Fades of the whole loop (scene in/out, a setting
    /// switched) run on <c>Time.unscaledDeltaTime</c>. Nothing here uses <c>Time.time</c>, <c>Time.deltaTime</c>
    /// or <c>WaitForSeconds</c>, so Pause (<c>Time.timeScale = 0</c>) never freezes the scheduler; the audio keeps
    /// playing while paused.
    ///
    /// Levels: SFX 0.8, music 0.30, ambience 0.35, each x 1 or 0 from <c>GameSettings.SoundOn</c> /
    /// <c>MusicOn</c> (ambience follows Sound), applied live. Music plays in both scenes, ambience only in
    /// MaliGoWorld.
    ///
    /// Hooks: <c>UiKit.ButtonClicked</c> -> ui_click; <c>GameEvents.MaliSpoke</c> -> mali_cue;
    /// <c>GameEvents.MoneyChanged</c> -> money_moved (at least 250 ms apart); <c>GameEvents.SoundRequested(name)</c>
    /// -> that clip; <c>GameEvents.DayStarted</c> -> new_day (once per day). <c>NightEnded</c> has no hook (the
    /// reveal requests its own chime). Clips come from <c>Resources/MaliGo/Audio/&lt;name&gt;</c>; a missing clip
    /// is skipped silently.
    ///
    /// On creation and on every scene load, if the loaded scene (or DontDestroyOnLoad) has no enabled
    /// <c>AudioListener</c>, one is added to that scene's screen camera (MaliGoWorld ships without one). The
    /// outgoing scene's listener can still be found during <c>sceneLoaded</c>, so it never counts, and the check
    /// runs again <see cref="ListenerRecheckSeconds"/> after the load; at most one listener is left enabled.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public const string ResourceFolder = "MaliGo/Audio/";
        public const string WorldSceneName = "MaliGoWorld";

        public const string ClipUiClick = "ui_click";
        public const string ClipMaliCue = "mali_cue";
        public const string ClipMoneyMoved = "money_moved";
        public const string ClipDayEndChime = "day_end_chime";
        public const string ClipNewDay = "new_day";
        public const string ClipMusic = "music_calm";
        public const string ClipAmbience = "ambience_township";

        public const float SfxLevel = 0.8f;
        public const float MusicLevel = 0.30f;
        public const float AmbienceLevel = 0.35f;

        /// <summary>Crossfade between the two copies of a loop (seconds of dspTime).</summary>
        public const double LoopCrossfadeSeconds = 2.0;

        /// <summary>Minimum gap between two money sounds (seconds, unscaled).</summary>
        public const float MoneySoundGap = 0.25f;

        /// <summary>Fade of a whole loop when it starts, stops or a setting changes (seconds, unscaled).</summary>
        public const float LoopFadeSeconds = 1.2f;

        /// <summary>How long ahead of dspTime a loop's first copy is scheduled.</summary>
        const double ScheduleLead = 0.1;

        const float ListenerRetrySeconds = 1f;
        const float ListenerRecheckSeconds = 0.5f;

        public static AudioManager Instance { get; private set; }

        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly HashSet<string> missingClips = new HashSet<string>();

        AudioSource sfxSource;
        LoopPair music;
        LoopPair ambience;
        float realtime;
        float lastMoneySound = -10f;
        int lastNewDay = -1;
        bool listenerPending;
        float listenerRetryAt;
        float listenerRecheckAt = -1f;
        Scene listenerScene;
        bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }

        /// <summary>The one AudioManager (created once, DontDestroyOnLoad).</summary>
        public static AudioManager Ensure()
        {
            if (Instance != null)
            {
                return Instance;
            }

            AudioManager existing = FindFirstObjectByType<AudioManager>();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject("AudioManager");
            return go.AddComponent<AudioManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            sfxSource = NewSource("SFX");
            music = new LoopPair(NewSource("Music A"), NewSource("Music B"), MusicLevel);
            ambience = new LoopPair(NewSource("Ambience A"), NewSource("Ambience B"), AmbienceLevel);

            Subscribe();
            SetUpScene(SceneManager.GetActiveScene());
        }

        void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        AudioSource NewSource(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        // ================================================================ hooks

        void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            subscribed = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            UiKit.ButtonClicked += OnButtonClicked;
            GameEvents.MaliSpoke += OnMaliSpoke;
            GameEvents.MoneyChanged += OnMoneyChanged;
            GameEvents.SoundRequested += OnSoundRequested;
            GameEvents.DayStarted += OnDayStarted;
            GameSettings.Changed += OnSettingsChanged;
        }

        void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            subscribed = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UiKit.ButtonClicked -= OnButtonClicked;
            GameEvents.MaliSpoke -= OnMaliSpoke;
            GameEvents.MoneyChanged -= OnMoneyChanged;
            GameEvents.SoundRequested -= OnSoundRequested;
            GameEvents.DayStarted -= OnDayStarted;
            GameSettings.Changed -= OnSettingsChanged;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // A new scene starts a new run of days (Start over, Live the week again): Day 1's jingle plays again.
            lastNewDay = -1;
            SetUpScene(scene);
            listenerRecheckAt = realtime + ListenerRecheckSeconds;
        }

        void OnButtonClicked() => PlayClip(ClipUiClick);

        void OnMaliSpoke() => PlayClip(ClipMaliCue);

        void OnSoundRequested(string clipName) => PlayClip(clipName);

        void OnMoneyChanged(MoneyEvent e)
        {
            float now = Time.realtimeSinceStartup;
            if (now - lastMoneySound < MoneySoundGap)
            {
                return;
            }

            lastMoneySound = now;
            PlayClip(ClipMoneyMoved);
        }

        void OnDayStarted(int day)
        {
            if (day == lastNewDay)
            {
                return;
            }

            lastNewDay = day;
            PlayClip(ClipNewDay);
        }

        void OnSettingsChanged()
        {
            // Targets are read every frame in Update; nothing to rebuild.
        }

        // ================================================================ public API

        /// <summary>Plays a one-off clip by its Resources name (e.g. "day_end_chime") on the UI source, at the SFX
        /// level, when Sound is on. A missing clip is skipped silently.</summary>
        public void PlayClip(string clipName)
        {
            if (sfxSource == null || string.IsNullOrEmpty(clipName) || !GameSettings.SoundOn)
            {
                return;
            }

            AudioClip clip = Clip(clipName);
            if (clip != null)
            {
                sfxSource.PlayOneShot(clip, SfxLevel);
            }
        }

        AudioClip Clip(string clipName)
        {
            if (clips.TryGetValue(clipName, out AudioClip clip) && clip != null)
            {
                return clip;
            }

            if (missingClips.Contains(clipName))
            {
                return null;
            }

            clip = Resources.Load<AudioClip>(ResourceFolder + clipName);
            if (clip == null)
            {
                missingClips.Add(clipName);
                return null;
            }

            clips[clipName] = clip;
            return clip;
        }

        // ================================================================ scenes

        void SetUpScene(Scene scene)
        {
            listenerScene = scene;
            EnsureListener();

            music.Play(Clip(ClipMusic), AudioSettings.dspTime);
            if (scene.name == WorldSceneName)
            {
                ambience.Play(Clip(ClipAmbience), AudioSettings.dspTime);
            }
            else
            {
                ambience.FadeOutAndStop();
            }
        }

        void EnsureListener()
        {
            listenerPending = false;
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

            // Keep the first enabled listener that belongs to the current scene (or DontDestroyOnLoad) and switch
            // off any other one there, so there are never two.
            AudioListener keep = null;
            foreach (AudioListener listener in listeners)
            {
                if (listener == null || !listener.isActiveAndEnabled || !BelongsToCurrentScene(listener.gameObject))
                {
                    continue;
                }

                if (keep == null)
                {
                    keep = listener;
                }
                else
                {
                    listener.enabled = false;
                }
            }

            if (keep != null)
            {
                return;
            }

            Camera camera = ScreenCamera();
            if (camera == null)
            {
                listenerPending = true;
                listenerRetryAt = realtime + ListenerRetrySeconds;
                return;
            }

            // Whatever is still enabled belongs to the outgoing scene (about to be destroyed): switch it off first.
            foreach (AudioListener listener in listeners)
            {
                if (listener != null && listener.enabled)
                {
                    listener.enabled = false;
                }
            }

            AudioListener own = camera.GetComponent<AudioListener>();
            if (own == null)
            {
                camera.gameObject.AddComponent<AudioListener>();
            }
            else
            {
                own.enabled = true;
            }
        }

        bool BelongsToCurrentScene(GameObject go)
        {
            if (!listenerScene.IsValid())
            {
                return true;
            }

            Scene scene = go.scene;
            return scene == listenerScene || scene.name == "DontDestroyOnLoad";
        }

        /// <summary>The current scene's camera that draws to the screen (MainCamera first; never a render-texture
        /// camera such as the look preview).</summary>
        Camera ScreenCamera()
        {
            Camera main = Camera.main;
            if (main != null && main.isActiveAndEnabled && main.targetTexture == null && BelongsToCurrentScene(main.gameObject))
            {
                return main;
            }

            foreach (Camera candidate in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (candidate != null && candidate.isActiveAndEnabled && candidate.targetTexture == null
                    && BelongsToCurrentScene(candidate.gameObject))
                {
                    return candidate;
                }
            }

            return null;
        }

        // ================================================================ frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            realtime += dt;
            double dsp = AudioSettings.dspTime;

            if (listenerPending && realtime >= listenerRetryAt)
            {
                EnsureListener();
            }

            if (listenerRecheckAt >= 0f && realtime >= listenerRecheckAt)
            {
                listenerRecheckAt = -1f;
                EnsureListener();
            }

            music.Tick(dsp, dt, GameSettings.MusicOn);
            ambience.Tick(dsp, dt, GameSettings.SoundOn);
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused)
            {
                Resync();
            }
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused)
            {
                Resync();
            }
        }

        void Resync()
        {
            double dsp = AudioSettings.dspTime;
            music?.Resync(dsp);
            ambience?.Resync(dsp);
        }

        // ================================================================ loop pair

        /// <summary>One looping clip on two sources: the copy playing and the next copy, scheduled on dspTime and
        /// crossfaded over <see cref="LoopCrossfadeSeconds"/>.</summary>
        sealed class LoopPair
        {
            readonly AudioSource[] sources = new AudioSource[2];
            readonly float level;

            AudioClip clip;
            bool playing;
            bool singleSource;   // clip too short to crossfade: one source with loop = true
            bool stopWhenSilent;
            int current;
            double currentStart;
            double nextStart;
            float gain;
            float targetGain;

            public LoopPair(AudioSource a, AudioSource b, float level)
            {
                sources[0] = a;
                sources[1] = b;
                this.level = level;
            }

            /// <summary>Starts the loop (fading in), or keeps it going if this clip is already playing.</summary>
            public void Play(AudioClip loopClip, double dsp)
            {
                if (loopClip == null)
                {
                    return;
                }

                stopWhenSilent = false;
                targetGain = 1f;
                if (playing && clip == loopClip)
                {
                    return;
                }

                StopSources();
                clip = loopClip;
                gain = 0f;
                current = 0;
                currentStart = dsp + ScheduleLead;
                singleSource = clip.length <= LoopCrossfadeSeconds * 2.0 + 0.5;

                foreach (AudioSource source in sources)
                {
                    source.clip = clip;
                    source.loop = false;
                    source.volume = 0f;
                }

                sources[0].loop = singleSource;
                sources[0].PlayScheduled(currentStart);
                playing = true;
                if (!singleSource)
                {
                    ScheduleNext();
                }
            }

            /// <summary>Fades the loop out over <see cref="LoopFadeSeconds"/> (unscaled), then stops both sources.</summary>
            public void FadeOutAndStop()
            {
                if (!playing)
                {
                    return;
                }

                targetGain = 0f;
                stopWhenSilent = true;
            }

            void ScheduleNext()
            {
                nextStart = currentStart + clip.length - LoopCrossfadeSeconds;
                AudioSource next = sources[1 - current];
                next.Stop();
                next.volume = 0f;
                next.PlayScheduled(nextStart);
            }

            void StopSources()
            {
                foreach (AudioSource source in sources)
                {
                    if (source != null)
                    {
                        source.Stop();
                        source.volume = 0f;
                    }
                }

                playing = false;
            }

            public void Tick(double dsp, float unscaledDt, bool settingOn)
            {
                if (!playing)
                {
                    return;
                }

                float target = settingOn ? targetGain : 0f;
                gain = Mathf.MoveTowards(gain, target, unscaledDt / LoopFadeSeconds);
                if (stopWhenSilent && gain <= 0f)
                {
                    StopSources();
                    return;
                }

                float volume = level * gain;
                if (singleSource)
                {
                    sources[0].volume = volume;
                    return;
                }

                // The outgoing copy has ended: the incoming one becomes current and the next copy is scheduled.
                if (dsp >= nextStart + LoopCrossfadeSeconds)
                {
                    current = 1 - current;
                    currentStart = nextStart;
                    ScheduleNext();
                }

                float incoming = 0f;
                if (dsp >= nextStart)
                {
                    incoming = Mathf.Clamp01((float)((dsp - nextStart) / LoopCrossfadeSeconds));
                }

                sources[current].volume = volume * (1f - incoming);
                sources[1 - current].volume = volume * incoming;
            }

            /// <summary>After the app returns from the background: re-anchors the schedule on the copy that is
            /// actually playing, or restarts the loop if neither copy is.</summary>
            public void Resync(double dsp)
            {
                if (!playing || clip == null)
                {
                    return;
                }

                AudioSource now = sources[current];
                if (singleSource)
                {
                    if (!now.isPlaying)
                    {
                        AudioClip c = clip;
                        playing = false;
                        Play(c, dsp);
                    }

                    return;
                }

                if (dsp >= nextStart && dsp < nextStart + LoopCrossfadeSeconds)
                {
                    return; // mid-crossfade: both copies are already where they should be
                }

                if (now.isPlaying)
                {
                    currentStart = dsp - now.time;
                    ScheduleNext();
                    return;
                }

                AudioClip restart = clip;
                float keepGain = gain;
                playing = false;
                Play(restart, dsp);
                gain = keepGain;
            }
        }
    }
}
