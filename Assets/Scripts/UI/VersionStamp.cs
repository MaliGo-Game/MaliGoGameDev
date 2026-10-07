using MaliGo.UI.Kit;
using UnityEngine;
using UnityEngine.UI;

namespace MaliGo.UI
{
    /// <summary>
    /// A small "v0.3.0-beta.1" label at the bottom centre of every screen, so testers' screenshots say which
    /// build they came from (README "Versioning"). <c>Application.version</c> is the Android versionName,
    /// set from the VERSION file at build time. Created once on boot and kept across scenes; it never takes
    /// touches.
    /// </summary>
    public class VersionStamp : MonoBehaviour
    {
        const int SortOrder = 95;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (FindAnyObjectByType<VersionStamp>() != null)
            {
                return;
            }

            var go = new GameObject("VersionStamp");
            DontDestroyOnLoad(go);
            go.AddComponent<VersionStamp>();
        }

        void Awake()
        {
            Canvas canvas = UiCanvasFactory.Create("Version_Canvas", SortOrder, transform, out RectTransform safeRoot);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            Text label = UiKit.Label(safeRoot, "Version", "v" + Application.version, UiTheme.Caption,
                UiTheme.WithAlpha(UiTheme.TextMuted, 0.7f), TextAnchor.LowerCenter);
            label.raycastTarget = false;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(420f, 40f);
            rect.anchoredPosition = new Vector2(0f, 6f);
        }
    }
}
