using System;
using System.Collections;
using System.Collections.Generic;
using MaliGo.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Small tween helpers for runtime UI (DESIGN_SPEC §5.1 motion). Everything runs on
    /// <c>Time.unscaledDeltaTime</c>, so tweens keep running while the game is paused (<c>Time.timeScale = 0</c>).
    ///
    /// Reduce motion (<see cref="GameSettings.ReduceMotion"/>): fades become a 150 ms crossfade, moves and scales
    /// jump to their end, count-ups jump to the final number and bobbing stops. Delays are not motion and keep
    /// their length.
    ///
    /// Each tween belongs to a target object. Starting a tween of the same kind on the same target replaces
    /// the running one (a new count-up on a HUD value continues from wherever the caller says); a tween whose
    /// target has been destroyed stops silently. <see cref="Stop"/> cancels all tweens of a target.
    /// </summary>
    public static class UiTween
    {
        public enum Ease
        {
            Linear,
            /// <summary>Material "standard" (ease in and out).</summary>
            Standard,
            /// <summary>Fast start, slow end (sheets in, count-ups).</summary>
            Decelerate,
            /// <summary>Slow start, fast end (sheets out).</summary>
            Accelerate,
            /// <summary>Quadratic ease-out (press).</summary>
            EaseOut,
            /// <summary>Half a sine wave (bobbing).</summary>
            Sine
        }

        enum Channel
        {
            None,
            Fade,
            Move,
            Scale,
            Count,
            Bob,
            Delay
        }

        sealed class Tween
        {
            public UnityEngine.Object Target;
            public bool HasTarget;
            public Channel Channel;
            public bool Cancelled;
            public Vector2 Origin;

            // A tween with a target dies with it (Unity-null); one without a target lives until it ends.
            public bool Alive => !Cancelled && (!HasTarget || Target != null);
        }

        static readonly List<Tween> active = new List<Tween>();
        static Runner runner;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            runner = null;
        }

        /// <summary>True when reduce motion is on.</summary>
        public static bool ReduceMotion => GameSettings.ReduceMotion;

        // ================================================================ public API

        /// <summary>Fades a CanvasGroup's alpha to <paramref name="to"/>. Reduce motion: 150 ms crossfade.</summary>
        public static void Fade(CanvasGroup group, float to, float duration = UiTheme.Motion.Fade, Ease ease = Ease.Standard,
            Action onComplete = null)
        {
            if (group == null)
            {
                return;
            }
            float from = group.alpha;
            Start(group, Channel.Fade, FadeDuration(duration), ease, t => group.alpha = Mathf.LerpUnclamped(from, to, t),
                () => group.alpha = to, onComplete);
        }

        /// <summary>Fades a Graphic's colour alpha to <paramref name="to"/>. Reduce motion: 150 ms crossfade.</summary>
        public static void Fade(Graphic graphic, float to, float duration = UiTheme.Motion.Fade, Ease ease = Ease.Standard,
            Action onComplete = null)
        {
            if (graphic == null)
            {
                return;
            }
            float from = graphic.color.a;
            Start(graphic, Channel.Fade, FadeDuration(duration), ease, t =>
            {
                Color c = graphic.color;
                c.a = Mathf.LerpUnclamped(from, to, t);
                graphic.color = c;
            }, () =>
            {
                Color c = graphic.color;
                c.a = to;
                graphic.color = c;
            }, onComplete);
        }

        /// <summary>Moves a RectTransform's anchoredPosition to <paramref name="to"/>. Reduce motion: jumps.</summary>
        public static void Move(RectTransform rect, Vector2 to, float duration = UiTheme.Motion.SheetIn, Ease ease = Ease.Decelerate,
            Action onComplete = null)
        {
            if (rect == null)
            {
                return;
            }
            Vector2 from = rect.anchoredPosition;
            Start(rect, Channel.Move, ReduceMotion ? 0f : duration, ease,
                t => rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t), () => rect.anchoredPosition = to, onComplete);
        }

        /// <summary>Scales a Transform uniformly to <paramref name="to"/>. Reduce motion: jumps.</summary>
        public static void Scale(Transform transform, float to, float duration = UiTheme.Motion.Press, Ease ease = Ease.EaseOut,
            Action onComplete = null)
        {
            if (transform == null)
            {
                return;
            }
            Vector3 from = transform.localScale;
            var end = new Vector3(to, to, from.z);
            Start(transform, Channel.Scale, ReduceMotion ? 0f : duration, ease,
                t => transform.localScale = Vector3.LerpUnclamped(from, end, t), () => transform.localScale = end, onComplete);
        }

        /// <summary>Counts from <paramref name="from"/> to <paramref name="to"/> over <paramref name="duration"/>
        /// (capped at 1.2 s, decelerating), calling <paramref name="onValue"/> every frame and with the exact final
        /// value at the end. <paramref name="owner"/> is the lifetime and replacement key (one count per owner).
        /// Reduce motion: <paramref name="onValue"/>(to) at once.</summary>
        public static void CountUp(UnityEngine.Object owner, float from, float to, Action<float> onValue,
            float duration = UiTheme.Motion.Count, Action onComplete = null)
        {
            if (onValue == null)
            {
                return;
            }
            float length = ReduceMotion ? 0f : Mathf.Min(duration, UiTheme.Motion.CountMax);
            Start(owner, Channel.Count, length, Ease.Decelerate, t => onValue(Mathf.LerpUnclamped(from, to, t)),
                () => onValue(to), onComplete);
        }

        /// <summary>Counts a Text from <paramref name="from"/> to <paramref name="to"/>, formatting every frame with
        /// <paramref name="format"/> (e.g. <c>MoneyFormat.Rand</c>).</summary>
        public static void CountUp(Text text, float from, float to, Func<float, string> format,
            float duration = UiTheme.Motion.Count, Action onComplete = null)
        {
            if (text == null)
            {
                return;
            }
            Func<float, string> f = format ?? (v => Mathf.RoundToInt(v).ToString(System.Globalization.CultureInfo.InvariantCulture));
            // Explicitly the (Object, Action<float>) overload. "v => text.text = f(v)" is also a valid
            // Func<float, string> (an assignment has a value), so with a Text first argument C# picked this same
            // overload: endless self-recursion, which IL2CPP turned into a loop allocating ~270 MB/s until Android
            // killed the game on every money count-up (Sleep's reveal, cash/savings after choices and the Bank).
            Action<float> onValue = v => text.text = f(v);
            CountUp((UnityEngine.Object)text, from, to, onValue, duration, onComplete);
        }

        /// <summary>Runs <paramref name="action"/> after <paramref name="seconds"/> of unscaled time, unless
        /// <paramref name="owner"/> has been destroyed or stopped first (owner null = always runs).</summary>
        public static void Delay(UnityEngine.Object owner, float seconds, Action action)
        {
            if (action == null)
            {
                return;
            }
            var tween = Register(owner, Channel.Delay, replace: false);
            EnsureRunner().StartCoroutine(DelayRoutine(tween, seconds, action));
        }

        /// <summary>Bobs <paramref name="rect"/> up and down by <paramref name="distance"/> u around its current
        /// anchoredPosition every <paramref name="period"/> seconds until <see cref="Stop"/> (the dialogue continue
        /// cue). Does nothing with reduce motion on.</summary>
        public static void Bob(RectTransform rect, float distance = UiTheme.Motion.CueBobDistance, float period = UiTheme.Motion.CueBob)
        {
            if (rect == null || ReduceMotion || period <= 0f)
            {
                return;
            }
            // Restarting a bob keeps the original rest position, not wherever the cue is mid-bob.
            Vector2 origin = rect.anchoredPosition;
            foreach (Tween running in active)
            {
                if (running.Alive && running.Channel == Channel.Bob && ReferenceEquals(running.Target, rect))
                {
                    origin = running.Origin;
                }
            }
            var tween = Register(rect, Channel.Bob, replace: true);
            tween.Origin = origin;
            EnsureRunner().StartCoroutine(BobRoutine(tween, rect, origin, distance, period));
        }

        /// <summary>Cancels every running tween and delay of <paramref name="target"/> (values stay where they are).</summary>
        public static void Stop(UnityEngine.Object target)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(active[i].Target, target))
                {
                    active[i].Cancelled = true;
                    active.RemoveAt(i);
                }
            }
        }

        /// <summary>Evaluates an easing curve at <paramref name="t"/> in [0, 1].</summary>
        public static float Evaluate(Ease ease, float t)
        {
            t = Mathf.Clamp01(t);
            switch (ease)
            {
                case Ease.Standard:
                    return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
                case Ease.Decelerate:
                    return 1f - (1f - t) * (1f - t) * (1f - t);
                case Ease.Accelerate:
                    return t * t * t;
                case Ease.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case Ease.Sine:
                    return 0.5f - 0.5f * Mathf.Cos(t * Mathf.PI);
                default:
                    return t;
            }
        }

        // ================================================================ implementation

        static float FadeDuration(float duration)
        {
            return ReduceMotion ? UiTheme.Motion.ReducedCrossfade : duration;
        }

        static Tween Register(UnityEngine.Object target, Channel channel, bool replace)
        {
            for (int i = active.Count - 1; i >= 0; i--)
            {
                Tween other = active[i];
                if (!other.Alive)
                {
                    active.RemoveAt(i); // target destroyed while it ran
                }
                else if (replace && target != null && other.Channel == channel && ReferenceEquals(other.Target, target))
                {
                    other.Cancelled = true;
                    active.RemoveAt(i);
                }
            }
            var tween = new Tween { Target = target, HasTarget = target != null, Channel = channel };
            active.Add(tween);
            return tween;
        }

        static void Finish(Tween tween)
        {
            active.Remove(tween);
        }

        static void Start(UnityEngine.Object target, Channel channel, float duration, Ease ease, Action<float> apply,
            Action applyEnd, Action onComplete)
        {
            Tween tween = Register(target, channel, replace: true);
            if (duration <= 0f)
            {
                applyEnd();
                Finish(tween);
                onComplete?.Invoke();
                return;
            }
            EnsureRunner().StartCoroutine(Routine(tween, duration, ease, apply, applyEnd, onComplete));
        }

        static IEnumerator Routine(Tween tween, float duration, Ease ease, Action<float> apply, Action applyEnd, Action onComplete)
        {
            float elapsed = 0f;
            apply(Evaluate(ease, 0f));
            while (elapsed < duration)
            {
                yield return null;
                if (!tween.Alive)
                {
                    Finish(tween);
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
                apply(Evaluate(ease, elapsed / duration));
            }
            if (!tween.Alive)
            {
                Finish(tween);
                yield break;
            }
            applyEnd();
            Finish(tween);
            onComplete?.Invoke();
        }

        static IEnumerator DelayRoutine(Tween tween, float seconds, Action action)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return null;
                if (!tween.Alive)
                {
                    Finish(tween);
                    yield break;
                }
                elapsed += Time.unscaledDeltaTime;
            }
            if (!tween.Alive)
            {
                Finish(tween);
                yield break;
            }
            Finish(tween);
            action();
        }

        static IEnumerator BobRoutine(Tween tween, RectTransform rect, Vector2 origin, float distance, float period)
        {
            float elapsed = 0f;
            while (tween.Alive)
            {
                float phase = (elapsed % period) / period;
                float offset = Mathf.Sin(phase * Mathf.PI * 2f) * distance;
                rect.anchoredPosition = origin + new Vector2(0f, offset);
                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }
            Finish(tween);
            if (rect != null)
            {
                rect.anchoredPosition = origin;
            }
        }

        static Runner EnsureRunner()
        {
            if (runner == null)
            {
                var go = new GameObject("UiTweenRunner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                runner = go.AddComponent<Runner>();
            }
            return runner;
        }

        sealed class Runner : MonoBehaviour
        {
            void OnDestroy()
            {
                if (runner == this)
                {
                    runner = null;
                }
            }
        }
    }
}
