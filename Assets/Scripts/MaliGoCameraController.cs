using UnityEngine;

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

    private Camera mainCamera;
    private Vector3 currentVelocity = Vector3.zero;

    void Awake()
    {
        mainCamera = GetComponent<Camera>();
        transform.rotation = Quaternion.Euler(isometricRotation);

        if (mainCamera != null)
        {
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = OrthographicSize;
            minZoom = OrthographicMinZoom;
            maxZoom = OrthographicMaxZoom;
        }
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

        if (target != null && followTarget)
        {
            transform.position = target.position + offset;
        }
    }

    void LateUpdate()
    {
        if (followTarget && target != null)
        {
            Vector3 desiredPos = target.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desiredPos, ref currentVelocity, 1f / Mathf.Max(0.1f, smoothSpeed));
        }

        // Keep isometric camera angle locked
        transform.rotation = Quaternion.Euler(isometricRotation);
    }

    void Update()
    {
        HandleZooming();
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
            mainCamera.orthographicSize -= scroll * zoomSpeed;
            mainCamera.orthographicSize = Mathf.Clamp(mainCamera.orthographicSize, minZoom, maxZoom);
        }
    }
}