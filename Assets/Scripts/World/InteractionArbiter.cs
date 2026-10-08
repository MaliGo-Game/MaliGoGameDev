using System;
using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.UI.Kit;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// One prompt, one tap target, the right thing happens (DESIGN_SPEC §7.1, D12).
    ///
    /// The registry is static so scene objects can register in <c>OnEnable</c> before the bootstrap creates
    /// the arbiter. Every frame the arbiter drops destroyed entries, then picks the nearest available World
    /// interactable within its radius of the player and in the player's space (the town or the room they are in,
    /// <see cref="InteriorMath.SameSpace"/>); a Companion only when nothing else is near and the player has
    /// stood still for 0.6 s (Mali is not registered: she is talked to from the HUD). Nothing is current while any modal is open. A tap (prompt or action button, via
    /// <see cref="RequestInteract"/>) or E calls the current one's <c>Interact</c>, or <c>OnDisabledTap</c> when it is
    /// greyed out. The request is cleared every frame whether or not anything used it.
    ///
    /// While an <see cref="Exclusive"/> interactable is set (the car's "Get out" while driving), it is the only one
    /// offered, wherever the player is; nothing else in the world (doors, spots) can be picked.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class InteractionArbiter : MonoBehaviour
    {
        const float StillSpeed = 0.05f;
        const float StillSeconds = 0.6f;
        const int NoRequest = int.MinValue / 2;

        static readonly List<IInteractable> registry = new List<IInteractable>();

        public static InteractionArbiter Instance { get; private set; }

        public event Action<IInteractable> CurrentChanged;

        /// <summary>Raised after the current interactable was used (Interact or OnDisabledTap), e.g. for the
        /// first-run guide's "prompt tapped".</summary>
        public event Action<IInteractable> Interacted;

        public IInteractable Current { get; private set; }

        /// <summary>When set, the only interactable offered (while it is available); null for the normal pick.</summary>
        public static IInteractable Exclusive { get; set; }

        int requestFrame = NoRequest;
        Transform player;
        Vector3 lastPlayerPosition;
        bool hasLastPosition;
        float stillTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            registry.Clear();
            Instance = null;
            Exclusive = null;
        }

        /// <summary>The arbiter on <paramref name="host"/> (created once; a null host gets a "MaliGo_Systems" object).</summary>
        public static InteractionArbiter Ensure(GameObject host)
        {
            if (Instance != null)
            {
                return Instance;
            }

            InteractionArbiter existing = FindFirstObjectByType<InteractionArbiter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            if (host == null)
            {
                host = GameObject.Find("MaliGo_Systems") ?? new GameObject("MaliGo_Systems");
            }

            return host.AddComponent<InteractionArbiter>();
        }

        /// <summary>Adds an interactable (duplicates ignored). Works before any arbiter exists.</summary>
        public static void Register(IInteractable interactable)
        {
            if (interactable == null || registry.Contains(interactable))
            {
                return;
            }

            registry.Add(interactable);
        }

        /// <summary>Removes an interactable (a no-op if it is not registered).</summary>
        public static void Unregister(IInteractable interactable)
        {
            if (interactable == null)
            {
                return;
            }

            registry.Remove(interactable);
            if (Instance != null && ReferenceEquals(Instance.Current, interactable))
            {
                Instance.SetCurrent(null);
            }
        }

        /// <summary>Touch: the action button or a prompt tap. Honoured this frame or the next, then cleared.</summary>
        public static void RequestInteract()
        {
            if (Instance != null)
            {
                Instance.requestFrame = Time.frameCount;
            }
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            requestFrame = NoRequest;
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        void Update()
        {
            PurgeDestroyed();
            RefreshPlayer();
            TrackStillness();

            if (UiModal.IsAnyOpen || player == null)
            {
                SetCurrent(null);
            }
            else
            {
                SetCurrent(Pick());
            }

            bool requested = Time.frameCount - requestFrame <= 1 || KeyPressed();
            requestFrame = NoRequest;

            IInteractable current = Current;
            if (!requested || current == null || (current as UnityEngine.Object) == null)
            {
                return;
            }

            try
            {
                if (current.IsEnabled)
                {
                    current.Interact();
                }
                else
                {
                    current.OnDisabledTap();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[InteractionArbiter] Interaction failed: {ex}");
            }

            Interacted?.Invoke(current);
        }

        static bool KeyPressed()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
        }

        static void PurgeDestroyed()
        {
            for (int i = registry.Count - 1; i >= 0; i--)
            {
                if ((registry[i] as UnityEngine.Object) == null)
                {
                    registry.RemoveAt(i);
                }
            }
        }

        void RefreshPlayer()
        {
            if (player != null)
            {
                return;
            }

            GameObject found = GameObject.FindWithTag("Player");
            player = found != null ? found.transform : null;
            hasLastPosition = false;
            stillTime = 0f;
        }

        void TrackStillness()
        {
            if (player == null)
            {
                hasLastPosition = false;
                stillTime = 0f;
                return;
            }

            Vector3 position = player.position;
            float dt = Time.deltaTime;
            if (!hasLastPosition)
            {
                lastPlayerPosition = position;
                hasLastPosition = true;
                stillTime = 0f;
                return;
            }

            Vector3 delta = position - lastPlayerPosition;
            delta.y = 0f;
            lastPlayerPosition = position;

            if (dt <= 0f)
            {
                return;
            }

            if (delta.magnitude / dt < StillSpeed)
            {
                stillTime += dt;
            }
            else
            {
                stillTime = 0f;
            }
        }

        IInteractable Pick()
        {
            IInteractable exclusive = Exclusive;
            if (exclusive != null)
            {
                if ((exclusive as UnityEngine.Object) == null)
                {
                    Exclusive = null;
                }
                else
                {
                    bool exclusiveAvailable;
                    try
                    {
                        exclusiveAvailable = exclusive.IsAvailable;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[InteractionArbiter] IsAvailable threw: {ex}");
                        exclusiveAvailable = false;
                    }

                    return exclusiveAvailable ? exclusive : null;
                }
            }

            Vector3 playerPosition = player.position;
            IInteractable bestWorld = null;
            float bestWorldDistance = float.MaxValue;
            IInteractable bestCompanion = null;
            float bestCompanionDistance = float.MaxValue;

            for (int i = 0; i < registry.Count; i++)
            {
                IInteractable candidate = registry[i];
                bool available;
                try
                {
                    available = candidate.IsAvailable;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[InteractionArbiter] IsAvailable threw: {ex}");
                    continue;
                }

                if (!available)
                {
                    continue;
                }

                Vector3 candidatePosition = candidate.InteractPosition;

                // Only what is in the player's space: the walk-in rooms are built high above the town
                // (BuildingInteriors), so a bed or a counter is never offered from the street below it and a street
                // spot is never offered from inside a room.
                if (!InteriorMath.SameSpace(playerPosition.y, candidatePosition.y))
                {
                    continue;
                }

                Vector3 delta = candidatePosition - playerPosition;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance > candidate.InteractRadius)
                {
                    continue;
                }

                if (candidate.Priority == InteractPriority.World)
                {
                    if (distance < bestWorldDistance)
                    {
                        bestWorld = candidate;
                        bestWorldDistance = distance;
                    }
                }
                else if (distance < bestCompanionDistance)
                {
                    bestCompanion = candidate;
                    bestCompanionDistance = distance;
                }
            }

            if (bestWorld != null)
            {
                return bestWorld;
            }

            return stillTime >= StillSeconds ? bestCompanion : null;
        }

        void SetCurrent(IInteractable next)
        {
            if (ReferenceEquals(Current, next))
            {
                return;
            }

            Current = next;
            CurrentChanged?.Invoke(next);
        }
    }
}
