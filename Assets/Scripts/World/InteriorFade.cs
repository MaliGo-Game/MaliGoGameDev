using System.Collections;
using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.World
{
    /// <summary>
    /// The quick cover used when the player goes through a door: the screen fades to the brand backdrop
    /// (<see cref="UiTheme.Inverse"/>), the player is moved, and it fades back. No text, no loading: a plain fade
    /// on unscaled time, over everything but notices (<see cref="UiTheme.Sort.DoorFade"/>). While it shows it takes
    /// taps, so nothing is pressed half-way through. The canvas is switched off whenever it is fully clear.
    /// </summary>
    public class InteriorFade : MonoBehaviour
    {
        Canvas canvas;
        Image cover;
        float alpha;

        /// <summary>Creates the fade under <paramref name="parent"/>, clear.</summary>
        public static InteriorFade Create(Transform parent)
        {
            var host = new GameObject("DoorFade");
            host.transform.SetParent(parent, false);
            InteriorFade fade = host.AddComponent<InteriorFade>();
            fade.Build();
            return fade;
        }

        public float Alpha => alpha;

        void Build()
        {
            canvas = UiCanvasFactory.Create("DoorFadeCanvas", UiTheme.Sort.DoorFade, transform, out _);
            cover = UiCanvasFactory.FullBleed(canvas, "Cover", UiTheme.Inverse);
            SetAlpha(0f);
        }

        /// <summary>Shows the cover at <paramref name="value"/> (0 clear .. 1 opaque) at once.</summary>
        public void SetAlpha(float value)
        {
            alpha = Mathf.Clamp01(value);
            if (cover == null || canvas == null)
            {
                return;
            }

            bool visible = alpha > 0.001f;
            cover.color = UiTheme.WithAlpha(UiTheme.Inverse, alpha);
            cover.raycastTarget = visible;
            canvas.enabled = visible;
        }

        /// <summary>Fades to <paramref name="target"/> over <paramref name="seconds"/> of unscaled time.</summary>
        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = alpha;
            if (seconds <= 0f)
            {
                SetAlpha(target);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / seconds);
                SetAlpha(Mathf.Lerp(from, target, UiTween.Evaluate(UiTween.Ease.Standard, t)));
                yield return null;
            }

            SetAlpha(target);
        }
    }
}
