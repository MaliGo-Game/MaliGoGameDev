using System;
using System.Collections.Generic;
using UnityEngine;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// Named UI rects the first-run coach marks point at (DESIGN_SPEC §7.10, §4.7): e.g. <c>hud.cash</c>,
    /// <c>hud.savings</c>, <c>hud.today</c>, <c>hud.energy</c>, <c>hud.bill</c>, <c>hud.pause</c>,
    /// <c>mali.dialogue</c>. Views register when the rect is visible and unregister in <c>OnDisable</c>.
    /// Destroyed rects are treated as absent.
    /// </summary>
    public static class UiAnchors
    {
        static readonly Dictionary<string, RectTransform> anchors = new Dictionary<string, RectTransform>();

        /// <summary>Raised when an anchor is registered or unregistered.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            anchors.Clear();
            Changed = null;
        }

        /// <summary>Registers (or replaces) <paramref name="id"/>.</summary>
        public static void Register(string id, RectTransform rect)
        {
            if (string.IsNullOrEmpty(id) || rect == null)
            {
                return;
            }
            anchors[id] = rect;
            Changed?.Invoke();
        }

        /// <summary>Removes <paramref name="id"/>.</summary>
        public static void Unregister(string id)
        {
            if (!string.IsNullOrEmpty(id) && anchors.Remove(id))
            {
                Changed?.Invoke();
            }
        }

        /// <summary>Removes <paramref name="id"/> only if it still points at <paramref name="rect"/>, so an old
        /// view going away never removes the anchor a newer view registered.</summary>
        public static void Unregister(string id, RectTransform rect)
        {
            if (string.IsNullOrEmpty(id) || !anchors.TryGetValue(id, out RectTransform current))
            {
                return;
            }
            if (current == rect || current == null)
            {
                anchors.Remove(id);
                Changed?.Invoke();
            }
        }

        /// <summary>The rect registered as <paramref name="id"/>, if it still exists.</summary>
        public static bool TryGet(string id, out RectTransform rect)
        {
            rect = null;
            if (string.IsNullOrEmpty(id) || !anchors.TryGetValue(id, out RectTransform found))
            {
                return false;
            }
            if (found == null)
            {
                anchors.Remove(id);
                return false;
            }
            rect = found;
            return true;
        }
    }
}
