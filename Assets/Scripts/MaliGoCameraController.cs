using UnityEngine;
using MaliGo.UI.Kit;
using MaliGo.World;

public class MaliGoCameraController : MonoBehaviour
{
    [Header("Target Tracking")]
    public Transform target;
    public Vector3 offset = new Vector3(-8f, 8f, -8f);
    public float smoothSpeed = 6f;
    public bool followTarget = true;

    [Header("Isometric Angle")]
    public Vector3 isometricRotation = new Vector3(30f, 45f, 0f);

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoom = 3f;
    public float maxZoom = 12f;

    [Header("Boundaries")]
    public Vector3 minBounds = new Vector3(-35f, 0f, -35f);
    public Vector3 maxBounds = new Vector3(35f, 35f, 35f);

    /// <summary>
    /// MaliGo is a 2.5D isometric game. The world camera in MaliGoWorld.unity had been switched to
    /// perspective (60 degrees, about 14 units out), so the town looked like a small aerial shot. The
    /// world is built at about 0.27 units per metre (player 0.48 u, houses 0.8-1.3 u), so an orthographic
    /// view a few units tall frames the player and the street around them. Enforced here rather than in
    /// the scene, so the scene file never has to be edited.
    /// </summary>
    const float OrthographicSize = 1.7f;
    const float OrthographicMinZoom = 1.2f;
    const float OrthographicMaxZoom = 3.2f;

    /// <summary>
    /// "Going in": the buildings are closed shells with no interior, so using a door (Home, Bank) steps the
    /// camera in toward it instead - the view tightens to this fraction of its size and drifts this far toward the
    /// door, held while the location's sheet is open, and eases back out when it closes. Reduce motion skips it.
    /// </summary>
    const float EntryZoomFactor = 0.8f;
    const float EntryFocusShift = 0.5f;
    const float EntryBlendSeconds = 0.35f;
    /// <summary>If no sheet opens within this long after the tap, the step-in is let go anyway.</summary>
    const float EntryWaitForModalSeconds = 1f;

    private Camera mainCamera;
    private Vector3 currentVelocity = Vector3.zero;
    private float baseOrthographicSize = OrthographicSize;

    private InteractionArbiter subscribedArbiter;
    private bool entryActive;
    private bool entrySawModal;
    private float entryStartTime;
    private float entryBlend;
    private Vector3 entryFocus;

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        transform.rotation = Quaternion.Euler(isometricRotation);

        if (mainCamera != null)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = OrthographicSize;
            baseOrthographicSize = OrthographicSize;
            minZoom = OrthographicMinZoom;
            maxZoom = OrthographicMaxZoom;
        }

        // Put the camera straight back along its own viewing direction, so the target lands in the centre of
        // the screen. The scene's (-8, 8, -8) offset doesn't match the 30/45 degree angle, which left the
        // player at the bottom edge on a phone, behind trees and fences and next to the joystick.
        float distance = Mathf.Max(offset.magnitude, 8f);
        offset = Quaternion.Euler(isometricRotation) * Vector3.back * distance;
    }

    void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }

        // The camera follows a point that never leaves the walkable town, the same area the player is kept in
        // (MaliGoPlayerController). The scene's +/-35 bounds were never applied and were far larger than the
        // 35 x 35 u lawn. The lawn reaches at least 11 u past every edge of the town, more than the view shows
        // from any point in it, so the screen never runs past the ground into the background.
        if (WalkableArea.TryGet(out Rect area))
        {
            minBounds = new Vector3(area.xMin, minBounds.y, area.yMin);
            maxBounds = new Vector3(area.xMax, maxBounds.y, area.yMax);
        }

        if (target != null && followTarget)
        {
            transform.position = Focus() + offset;
        }
    }

    void OnDestroy()
    {
        if (subscribedArbiter != null)
        {
            subscribedArbiter.Interacted -= OnInteracted;
            subscribedArbiter = null;
        }
    }

    void LateUpdate()
    {
        SubscribeToArbiter();
        UpdateEntry();

        float eased = Mathf.SmoothStep(0f, 1f, entryBlend);

        if (followTarget && target != null)
        {
            Vector3 focus = Focus();
            if (eased > 0f)
            {
                Vector3 doorway = new Vector3(entryFocus.x, focus.y, entryFocus.z);
                focus = Vector3.Lerp(focus, doorway, eased * EntryFocusShift);
            }

            Vector3 desiredPos = focus + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentVelocity, 1f / Mathf.Max(0.1f, smoothSpeed));
        }

        if (mainCamera != null)
        {
            mainCamera.orthographicSize = baseOrthographicSize * Mathf.Lerp(1f, EntryZoomFactor, eased);
        }

        // Keep isometric camera angle locked
        transform.rotation = Quaternion.Euler(isometricRotation);
    }

    void Update()
    {
        HandleZooming();
    }

    /// <summary>The target's position with X and Z kept inside <see cref="minBounds"/>/<see cref="maxBounds"/>.</summary>
    Vector3 Focus()
    {
        Vector3 position = target.position;
        position.x = Mathf.Clamp(position.x, minBounds.x, maxBounds.x);
        position.z = Mathf.Clamp(position.z, minBounds.z, maxBounds.z);
        return position;
    }

    /// <summary>The arbiter is created by the bootstrap, possibly after this camera starts: subscribe once it exists.</summary>
    void SubscribeToArbiter()
    {
        InteractionArbiter arbiter = InteractionArbiter.Instance;
        if (arbiter == subscribedArbiter)
        {
            return;
        }

        if (subscribedArbiter != null)
        {
            subscribedArbiter.Interacted -= OnInteracted;
        }

        subscribedArbiter = arbiter;
        if (subscribedArbiter != null)
        {
            subscribedArbiter.Interacted += OnInteracted;
        }
    }

    void OnInteracted(IInteractable interactable)
    {
        if (!(interactable is ProximityInteraction location) || location == null || !location.IsBuildingEntrance)
        {
            return;
        }

        if (UiTween.ReduceMotion)
        {
            return;
        }

        entryActive = true;
        entrySawModal = false;
        entryStartTime = Time.unscaledTime;
        entryFocus = location.InteractPosition;
    }

    /// <summary>Holds the step-in while the location's sheet (or what follows it, e.g. sleep and the reveal) is
    /// open, then lets go; the blend runs on unscaled time so a paused game still eases back.</summary>
    void UpdateEntry()
    {
        if (entryActive)
        {
            if (UiModal.IsAnyOpen)
            {
                entrySawModal = true;
            }
            else if (entrySawModal || Time.unscaledTime - entryStartTime > EntryWaitForModalSeconds)
            {
                entryActive = false;
            }
        }

        float goal = entryActive ? 1f : 0f;
        entryBlend = Mathf.MoveTowards(entryBlend, goal, Time.unscaledDeltaTime / EntryBlendSeconds);
    }

    void HandleZooming()
    {
        if (mainCamera == null) return;

        float scroll = 0f;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null)
        {
            scroll = UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y * 0.01f;
        }
#endif
        if (Mathf.Abs(scroll) < 0.001f)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            scroll = Input.GetAxis("Mouse ScrollWheel");
#endif
        }

        if (Mathf.Abs(scroll) > 0.001f)
        {
            baseOrthographicSize -= scroll * zoomSpeed;
            baseOrthographicSize = Mathf.Clamp(baseOrthographicSize, minZoom, maxZoom);
        }
    }
}
