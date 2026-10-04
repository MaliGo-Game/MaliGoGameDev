using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaliGo.UI.Kit
{
    /// <summary>
    /// The modal stack, the back button and the movement gate (DESIGN_SPEC §7.2).
    ///
    /// Modals: blocking Mali box, choice sheet, Home/Bank, sleep confirm, reveal, chapter end, pause, reset
    /// confirm. Not modal: compact Mali box, coach marks, prompt, HUD. Back goes to the top modal's
    /// <c>onBack</c> (null = ignored for that modal); with no modal open it raises <see cref="BackWithNoModal"/>
    /// (pause opens on it).
    ///
    /// The stack never outlives a scene:
    /// 1. <see cref="Pop"/> removes that owner wherever it is in the stack (no-op if absent).
    /// 2. Every modal view calls <c>UiModal.Pop(this)</c> in both <c>OnDisable</c> and <c>OnDestroy</c>.
    /// 3. The router clears the stack (and sets <c>Time.timeScale = 1</c>) on every <c>sceneLoaded</c>, so no
    ///    view may push before <c>Start</c>.
    /// </summary>
    public static class UiModal
    {
        struct Entry
        {
            public object Owner;
            public Action OnBack;
        }

        static readonly List<Entry> stack = new List<Entry>();
        static Router router;

        /// <summary>Raised when back is pressed with no modal open.</summary>
        public static event Action BackWithNoModal;
        /// <summary>Raised whenever the stack changes.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            stack.Clear();
            router = null;
            BackWithNoModal = null;
            Changed = null;
        }

        // The back key must work (pause on BackWithNoModal) even before any modal has been pushed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void CreateRouterAtStartup()
        {
            EnsureRouter();
        }

        /// <summary>Pushes <paramref name="owner"/> on top (moving it there if it was already open).
        /// <paramref name="onBack"/> runs on the back button while it is on top; null = back is ignored.</summary>
        public static void Push(object owner, Action onBack)
        {
            if (owner == null)
            {
                return;
            }
            EnsureRouter();
            RemoveOwner(owner);
            stack.Add(new Entry { Owner = owner, OnBack = onBack });
            Changed?.Invoke();
        }

        /// <summary>Removes <paramref name="owner"/> wherever it is in the stack and raises <see cref="Changed"/>.
        /// A no-op if it is not open.</summary>
        public static void Pop(object owner)
        {
            if (owner == null)
            {
                return;
            }
            if (RemoveOwner(owner))
            {
                Changed?.Invoke();
            }
        }

        /// <summary>True while any modal is open (owners destroyed without popping are ignored).</summary>
        public static bool IsAnyOpen
        {
            get
            {
                PurgeDestroyed();
                return stack.Count > 0;
            }
        }

        /// <summary>The top modal's owner, or null.</summary>
        public static object Top
        {
            get
            {
                PurgeDestroyed();
                return stack.Count > 0 ? stack[stack.Count - 1].Owner : null;
            }
        }

        /// <summary>True if <paramref name="owner"/> is anywhere in the stack.</summary>
        public static bool Contains(object owner)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i].Owner, owner))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Empties the stack and raises <see cref="Changed"/>.</summary>
        public static void ClearAll()
        {
            stack.Clear();
            Changed?.Invoke();
        }

        /// <summary>Acts as if the back button was pressed: the top modal's back action, else
        /// <see cref="BackWithNoModal"/>. Called by the router; also usable by an on-screen back button.</summary>
        public static void Back()
        {
            PurgeDestroyed();
            if (stack.Count > 0)
            {
                Action onBack = stack[stack.Count - 1].OnBack;
                if (onBack != null)
                {
                    onBack();
                }
                return;
            }
            BackWithNoModal?.Invoke();
        }

        /// <summary>Creates (once) the DontDestroyOnLoad router that polls <c>Keyboard.current.escapeKey</c>
        /// (Android back arrives as Escape) and clears the stack on every scene load.</summary>
        public static void EnsureRouter()
        {
            if (router != null)
            {
                return;
            }
            var go = new GameObject("UiModalRouter");
            UnityEngine.Object.DontDestroyOnLoad(go);
            router = go.AddComponent<Router>();
        }

        static bool RemoveOwner(object owner)
        {
            bool removed = false;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(stack[i].Owner, owner))
                {
                    stack.RemoveAt(i);
                    removed = true;
                }
            }
            return removed;
        }

        // A view destroyed without popping (should not happen: rule 2) must not block input for good.
        static void PurgeDestroyed()
        {
            bool removed = false;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                if (stack[i].Owner is UnityEngine.Object unityObject && unityObject == null)
                {
                    stack.RemoveAt(i);
                    removed = true;
                }
            }
            if (removed)
            {
                Changed?.Invoke();
            }
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Time.timeScale = 1f;
            ClearAll();
        }

        sealed class Router : MonoBehaviour
        {
            void OnEnable()
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                SceneManager.sceneLoaded += OnSceneLoaded;
            }

            void OnDisable()
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            void OnDestroy()
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                if (router == this)
                {
                    router = null;
                }
            }

            void Update()
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    Back();
                }
            }
        }
    }
}
