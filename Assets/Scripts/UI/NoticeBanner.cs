using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// One-line notice (DESIGN_SPEC §5.4.12, sort 90): top-centre pill 96 u tall (wrapped and taller on a portrait
    /// screen), 24 u below the safe top,
    /// <c>Inverse</c> fill, Body text in <c>TextOnInverse</c> beside the 48 u <c>information</c> icon. Shows for 6 s or
    /// until tapped, then fades out and destroys itself. Not modal. Used for the old-save notice (§2.6).
    /// </summary>
    public class NoticeBanner : MonoBehaviour, IPointerClickHandler
    {
        public const float ShowSeconds = 6f;
        const float Height = 96f;
        const float TopGap = 24f;
        const float IconSize = 48f;
        const float Pad = 40f;
        const float Gap = 20f;

        static NoticeBanner current;

        Canvas canvas;
        CanvasGroup group;
        bool closing;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            current = null;
        }

        /// <summary>Shows <paramref name="message"/> (replacing a banner already up).</summary>
        public static NoticeBanner Show(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return null;
            }

            if (current != null)
            {
                Destroy(current.gameObject);
                current = null;
            }

            var go = new GameObject("NoticeBanner");
            var banner = go.AddComponent<NoticeBanner>();
            banner.Build(message);
            current = banner;
            return banner;
        }

        void Build(string message)
        {
            canvas = UiCanvasFactory.Create("NoticeBanner_Canvas", UiTheme.Sort.Notices, transform, out RectTransform safeRoot);
            group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = true;

            Image pill = UiKit.Panel(safeRoot, "Pill", UiTheme.Inverse, UiTheme.RadiusPill(Height), UiTheme.ShadowCard);
            pill.raycastTarget = true;
            RectTransform rect = pill.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -TopGap);
            pill.gameObject.AddComponent<ClickRelay>().owner = this;

            Text label = UiKit.Label(rect, "Text", message, UiTheme.Body, UiTheme.TextOnInverse, TextAnchor.MiddleLeft);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            float textWidth = Mathf.Ceil(UiTextLayout.MeasureWidth(label, message));
            float width = Pad + IconSize + Gap + textWidth + Pad;
            float height = Height;
            // On a narrow (portrait onboarding) canvas the one line would run off the screen: wrap it instead, in
            // a taller pill kept inside the screen margins.
            float maxWidth = UiCanvasFactory.SafeAreaSize().x - 2f * UiTheme.ScreenMargin;
            if (maxWidth > 0f && width > maxWidth)
            {
                width = maxWidth;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                int lines = Mathf.Max(1, UiTextLayout.CountLines(label, message, width - Pad - IconSize - Gap - Pad));
                height = Mathf.Max(Height, Mathf.Ceil(lines * UiTheme.Body.Size * UiTheme.Body.LineSpacing) + 2f * Gap);
            }

            rect.sizeDelta = new Vector2(width, height);
            label.rectTransform.offsetMin = new Vector2(Pad + IconSize + Gap, 0f);
            label.rectTransform.offsetMax = new Vector2(-Pad, 0f);

            Image icon = UiKit.IconImage(rect, "Icon", "information", IconSize, UiTheme.TextOnInverse);
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(Pad, 0f);

            group.alpha = 0f;
            UiTween.Fade(group, 1f);
            UiTween.Delay(this, ShowSeconds, Close);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            Close();
        }

        /// <summary>Fades out and destroys the banner.</summary>
        public void Close()
        {
            if (closing || group == null)
            {
                return;
            }

            closing = true;
            group.blocksRaycasts = false;
            UiTween.Fade(group, 0f, UiTheme.Motion.Fade, UiTween.Ease.Standard, () =>
            {
                if (this != null)
                {
                    Destroy(gameObject);
                }
            });
        }

        void OnDestroy()
        {
            UiTween.Stop(this);
            if (group != null)
            {
                UiTween.Stop(group);
            }

            if (current == this)
            {
                current = null;
            }
        }

        sealed class ClickRelay : MonoBehaviour, IPointerClickHandler
        {
            public NoticeBanner owner;

            public void OnPointerClick(PointerEventData eventData)
            {
                if (owner != null)
                {
                    owner.Close();
                }
            }
        }
    }
}
