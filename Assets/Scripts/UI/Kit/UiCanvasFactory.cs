using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Builds the one-canvas-per-layer setup every runtime view uses (DESIGN_SPEC §5.1 sort orders, §7.14):
    /// Screen Space Overlay, <c>CanvasScaler.ScaleWithScreenSize</c> at 1920 x 1080 with
    /// <c>matchWidthOrHeight = 1</c> (the canvas is always 1080 u tall), a <c>GraphicRaycaster</c>, and a
    /// stretched "SafeArea" child carrying a <see cref="SafeAreaFitter"/>. A <see cref="CanvasOrientationScaler"/>
    /// swaps the reference to 1080 x 1920 matching width while the screen is portrait (portrait onboarding), so the
    /// canvas is then always 1080 u wide. Full-bleed backdrops go directly under
    /// the canvas (before the safe root); everything else goes under <c>safeRoot</c>.
    /// </summary>
    public static class UiCanvasFactory
    {
        /// <summary>Creates the canvas GameObject <paramref name="name"/> (under <paramref name="parent"/> if given)
        /// and returns its Canvas; <paramref name="safeRoot"/> is the safe-area content root.</summary>
        public static Canvas Create(string name, int sortOrder, Transform parent, out RectTransform safeRoot)
        {
            MaliGo.UI.EventSystemUtility.EnsureEventSystem();

            var canvasObject = new GameObject(string.IsNullOrEmpty(name) ? "Canvas" : name, typeof(RectTransform));
            canvasObject.layer = LayerUi();
            if (parent != null)
            {
                canvasObject.transform.SetParent(parent, false);
            }

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            canvas.pixelPerfect = false;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(UiTheme.ReferenceWidth, UiTheme.ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f;
            canvasObject.AddComponent<CanvasOrientationScaler>().Apply(true);

            canvasObject.AddComponent<GraphicRaycaster>();

            var safeObject = new GameObject("SafeArea", typeof(RectTransform));
            safeObject.layer = canvasObject.layer;
            safeRoot = (RectTransform)safeObject.transform;
            safeRoot.SetParent(canvasObject.transform, false);
            safeRoot.anchorMin = Vector2.zero;
            safeRoot.anchorMax = Vector2.one;
            safeRoot.offsetMin = Vector2.zero;
            safeRoot.offsetMax = Vector2.zero;
            safeRoot.pivot = new Vector2(0.5f, 0.5f);
            safeObject.AddComponent<SafeAreaFitter>().Apply(true);

            return canvas;
        }

        /// <summary>Adds a full-bleed child (outside the safe area) directly under <paramref name="canvas"/>, placed
        /// behind the safe root: for scrims and backdrops.</summary>
        public static Image FullBleed(Canvas canvas, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = canvas.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvas.transform, false);
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.sprite = UiKit.White;
            image.color = color;
            return image;
        }

        /// <summary>True while the screen is taller than it is wide.</summary>
        public static bool ScreenIsPortrait => Screen.height > Screen.width;

        /// <summary>Canvas units per screen pixel for a factory canvas: the short side is always 1080 u (see
        /// <see cref="CanvasOrientationScaler"/>).</summary>
        public static float UnitsPerPixel => UiTheme.ReferenceHeight / Mathf.Max(1, Mathf.Min(Screen.width, Screen.height));

        /// <summary>The size in u of <c>Screen.safeArea</c> on a factory canvas, i.e. the size of its safe root.</summary>
        public static Vector2 SafeAreaSize()
        {
            Rect safe = Screen.safeArea;
            if (safe.width <= 0f || safe.height <= 0f)
            {
                safe = new Rect(0f, 0f, Screen.width, Screen.height);
            }
            return safe.size * UnitsPerPixel;
        }

        static int LayerUi()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : 0;
        }
    }
}
