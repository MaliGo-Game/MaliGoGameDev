using UnityEngine;
using MaliGo.Core;
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
    /// How quickly the view size eases between the town's size and a room's (<see cref="WalkableArea.InteriorViewSize"/>),
    /// per second on unscaled time: ~95 % of the way in a quarter of a second, mostly behind the door fade.
    /// </summary>
    const float ViewSizeSharpness = 12f;

    private Camera mainCamera;
    private Vector3 currentVelocity = Vector3.zero;
    private float baseOrthographicSize = OrthographicSize;
    private bool snapPending;

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

        RefreshBounds();

        if (target != null && followTarget)
        {
            transform.position = Focus() + offset;
        }
    }

    /// <summary>
    /// Jumps straight to the target on the next LateUpdate instead of gliding there. Called when the player is moved
    /// through a door (behind the fade), so the camera never flies from the street up to a room; the view size still
    /// eases to the new area's size, settling as the fade lifts.
    /// </summary>
    public void SnapToTarget()
    {
        snapPending = true;
    }

    void LateUpdate()
    {
        RefreshBounds();

        if (followTarget && target != null)
        {
            Vector3 desiredPos = Focus() + offset;
            if (snapPending)
            {
                transform.position = desiredPos;
                currentVelocity = Vector3.zero;
            }
            else
            {
                transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentVelocity, 1f / Mathf.Max(0.1f, smoothSpeed));
            }
        }

        if (mainCamera != null)
        {
            // Inside a room the view frames the whole (small) room; outside it is the town's size (and zoom).
            float interiorSize = WalkableArea.InteriorViewSize;
            float goal = interiorSize > 0f ? interiorSize : baseOrthographicSize;
            mainCamera.orthographicSize = InteriorMath.Ease(mainCamera.orthographicSize, goal, ViewSizeSharpness, Time.unscaledDeltaTime);
        }

        snapPending = false;

        // Keep isometric camera angle locked
        transform.rotation = Quaternion.Euler(isometricRotation);
    }

    void Update()
    {
        HandleZooming();
    }

    /// <summary>
    /// The follow point stays inside the active area's camera bounds (<see cref="WalkableArea.TryGetCameraBounds"/>):
    /// in the town, the walkable town, the same area the player is kept in (the scene's +/-35 bounds were never
    /// applied and were far larger than the 35 x 35 u lawn; the lawn reaches at least 11 u past every edge of the
    /// town, more than the view shows, so the screen never runs past the ground). Inside a room the bounds are the
    /// room's framing point, so the camera holds the whole room still. Read every frame: a cached static, no search.
    /// </summary>
    void RefreshBounds()
    {
        if (WalkableArea.TryGetCameraBounds(out Rect area))
        {
            minBounds = new Vector3(area.xMin, minBounds.y, area.yMin);
            maxBounds = new Vector3(area.xMax, maxBounds.y, area.yMax);
        }
    }

    /// <summary>The target's position with X and Z kept inside <see cref="minBounds"/>/<see cref="maxBounds"/>.</summary>
    Vector3 Focus()
    {
        Vector3 position = target.position;
        position.x = Mathf.Clamp(position.x, minBounds.x, maxBounds.x);
        position.z = Mathf.Clamp(position.z, minBounds.z, maxBounds.z);
        return position;
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
