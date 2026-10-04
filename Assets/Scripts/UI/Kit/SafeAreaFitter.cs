using UnityEngine;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Keeps a canvas's content root inside <c>Screen.safeArea</c> (DESIGN_SPEC §7.14). In <c>Update</c>, when the
    /// safe area, screen size or orientation changed, the RectTransform's anchors are set to the safe rect
    /// normalised by the screen size (pixels, bottom-left origin) and its offsets to zero. Full-bleed backdrops
    /// (reveal, character creation, scrims) sit outside the fitter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        RectTransform rectTransform;
        Rect lastSafeArea;
        int lastWidth;
        int lastHeight;
        ScreenOrientation lastOrientation;
        bool applied;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
        }

        void OnEnable()
        {
            applied = false;
            Apply();
        }

        void Update()
        {
            Apply();
        }

        /// <summary>Applies the safe area now if anything changed (or always when <paramref name="force"/>).</summary>
        public void Apply(bool force = false)
        {
            if (rectTransform == null)
            {
                rectTransform = GetComponent<RectTransform>();
            }

            Rect safe = Screen.safeArea;
            int width = Screen.width;
            int height = Screen.height;
            ScreenOrientation orientation = Screen.orientation;
            if (!force && applied && safe == lastSafeArea && width == lastWidth && height == lastHeight &&
                orientation == lastOrientation)
            {
                return;
            }

            lastSafeArea = safe;
            lastWidth = width;
            lastHeight = height;
            lastOrientation = orientation;
            applied = true;

            if (width <= 0 || height <= 0 || safe.width <= 0f || safe.height <= 0f)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
            }
            else
            {
                rectTransform.anchorMin = new Vector2(Mathf.Clamp01(safe.xMin / width), Mathf.Clamp01(safe.yMin / height));
                rectTransform.anchorMax = new Vector2(Mathf.Clamp01(safe.xMax / width), Mathf.Clamp01(safe.yMax / height));
            }
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
}
