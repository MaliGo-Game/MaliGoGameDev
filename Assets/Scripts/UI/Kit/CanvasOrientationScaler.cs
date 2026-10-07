using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Keeps a canvas's <c>CanvasScaler</c> keyed to the screen's short side, so a unit is the same physical size in
    /// both orientations. Landscape (the game): reference 1920 x 1080, <c>matchWidthOrHeight = 1</c>, the canvas is
    /// always 1080 u tall (unchanged from before). Portrait (onboarding, <c>MaliGoFeatures.PortraitOnboarding</c>):
    /// reference 1080 x 1920, <c>matchWidthOrHeight = 0</c>, the canvas is always 1080 u wide; without this a
    /// portrait canvas would be 1080 u tall and under 500 u wide. Checked every frame (cheap), like
    /// <see cref="SafeAreaFitter"/>, and before the scaler's own update so a rotation never shows a wrong frame.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class CanvasOrientationScaler : MonoBehaviour
    {
        CanvasScaler scaler;
        bool applied;
        bool lastPortrait;

        void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
        }

        void OnEnable()
        {
            Apply(true);
        }

        void Update()
        {
            Apply(false);
        }

        /// <summary>Applies the reference for the current screen shape if it changed (or always when
        /// <paramref name="force"/>).</summary>
        public void Apply(bool force)
        {
            if (scaler == null)
            {
                scaler = GetComponent<CanvasScaler>();
            }

            bool portrait = UiCanvasFactory.ScreenIsPortrait;
            if (!force && applied && portrait == lastPortrait)
            {
                return;
            }

            applied = true;
            lastPortrait = portrait;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            if (portrait)
            {
                scaler.referenceResolution = new Vector2(UiTheme.ReferenceHeight, UiTheme.ReferenceWidth);
                scaler.matchWidthOrHeight = 0f;
            }
            else
            {
                scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
                scaler.matchWidthOrHeight = 1f;
            }
        }
    }
}
