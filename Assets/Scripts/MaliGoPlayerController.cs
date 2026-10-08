using UnityEngine;
using MaliGo.Characters;
using MaliGo.Core;
using MaliGo.UI.Kit;
using MaliGo.World;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Rigidbody))]
public class MaliGoPlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    // Overwritten in Awake from MovementMath (a serialized value must not bring the old 4.5 u/s back).
    public float moveSpeed = MovementMath.JogSpeed;
    public float acceleration = MovementMath.Acceleration;
    public float deceleration = MovementMath.Deceleration;
    public float gravity = -18f;

    [Header("Input Source")]
    public bool useKeyboardInput = true;
    public Vector2 virtualJoystickInput = Vector2.zero;

    [Header("Visual")]
    public CharacterSpriteController spriteController;
    public PlayerCharacterVisualController visualController;

    /// <summary>Downward speed kept while grounded, so every Move presses into the ground and
    /// CharacterController.isGrounded stays true (with 0 it flips every other frame).</summary>
    const float GroundStickSpeed = 0.5f;
    const float MaxFallSpeed = 6f;

    private CharacterController characterController;
    private Vector3 currentHorizontalVelocity = Vector3.zero;
    private float verticalVelocity = 0f;
    private Vector2 lastNonZeroInput = Vector2.up;

    // The body's heading (yaw, degrees) and speed along it: the player only ever moves the way they face, and
    // turns toward the stick at a capped rate, so a sharp change of direction turns on the spot first.
    private float facingYaw;
    private float currentSpeed;
    private float groundSpeed;
    private float lastGroundSpeed;
    private bool wasGrounded;

    private bool hasWalkableArea;
    private Rect walkableArea;

    void Awake()
    {
        // Enforced here, like the camera's orthographic size, so a stale serialized 4.5 (from when the character
        // was ~100x too big) in a scene or prefab can't bring the 16 m/s sprint back. See MovementMath.JogSpeed.
        moveSpeed = MovementMath.JogSpeed;
        acceleration = MovementMath.Acceleration;
        deceleration = MovementMath.Deceleration;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        gameObject.tag = "Player";
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();

        if (spriteController == null)
            spriteController = GetComponentInChildren<CharacterSpriteController>();

        if (visualController == null)
            visualController = GetComponentInChildren<PlayerCharacterVisualController>();

        // Start facing the way the model already faces (prefab or spawner rotation), not snapping to +Z.
        facingYaw = visualController != null ? visualController.transform.eulerAngles.y : transform.eulerAngles.y;

        // Measured once per scene (cached in WalkableArea); false outside the town scene, then nothing is clamped.
        hasWalkableArea = WalkableArea.TryGet(out walkableArea);
    }

    void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        float dt = Time.deltaTime;
        Vector2 input = GetMovementInput();
        float inputMagnitude = input.magnitude;
        float askedSpeed = MovementMath.SpeedForInput(inputMagnitude);

        if (askedSpeed > 0f)
        {
            lastNonZeroInput = input / inputMagnitude;
        }

        // Screen-relative Isometric direction calculation
        // Project Camera.main's forward & right onto the horizontal XZ plane
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;

        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            camForward = Vector3.ProjectOnPlane(mainCam.transform.forward, Vector3.up).normalized;
            camRight = Vector3.ProjectOnPlane(mainCam.transform.right, Vector3.up).normalized;
        }

        // Isometric direction: W/Up moves toward upper screen, D/Right moves toward right screen.
        // Turn the body toward it at a capped rate; while it still faces well away, ask for less speed (down to
        // none), so the character turns on the spot instead of sliding sideways.
        float targetSpeed = 0f;
        if (askedSpeed > 0f)
        {
            Vector3 targetMoveDirection = camRight * input.x + camForward * input.y;
            float targetYaw = MovementMath.YawOf(targetMoveDirection.x, targetMoveDirection.z);
            facingYaw = MovementMath.TurnTowards(facingYaw, targetYaw, dt);
            targetSpeed = askedSpeed * MovementMath.TurnSpeedFactor(MovementMath.DeltaAngle(facingYaw, targetYaw));
        }

        // Believable starts and stops (~0.25 s up to a jog, ~0.3 s to stand), always along the facing.
        currentSpeed = MovementMath.ApproachSpeed(currentSpeed, Mathf.Min(targetSpeed, moveSpeed), dt);
        currentHorizontalVelocity = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward * currentSpeed;
        KeepInsideWalkableArea();

        // Grounding & Gravity
        Vector3 positionBefore = transform.position;
        if (characterController != null && characterController.enabled)
        {
            if (characterController.isGrounded && verticalVelocity <= 0f)
            {
                verticalVelocity = -GroundStickSpeed; // press into the ground every frame
            }
            else
            {
                verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, -MaxFallSpeed);
            }

            Vector3 finalMoveVector = currentHorizontalVelocity + (Vector3.up * verticalVelocity);
            characterController.Move(finalMoveVector * dt);
            SnapDownToGround();
            wasGrounded = characterController.isGrounded;
        }

        // The legs follow how far the body really went (a wall or the town edge stops them), smoothed a little so
        // a kerb or a frame hitch doesn't stutter them; never faster than asked.
        Vector3 moved = transform.position - positionBefore;
        moved.y = 0f;
        float measured = dt > 0f ? Mathf.Min(moved.magnitude / dt, currentHorizontalVelocity.magnitude) : 0f;
        lastGroundSpeed = groundSpeed;
        groundSpeed = Mathf.Lerp(groundSpeed, measured, 1f - Mathf.Exp(-dt / 0.05f));
        float forwardAcceleration = dt > 0f ? (groundSpeed - lastGroundSpeed) / dt : 0f;

        // Visual feedback for human player or legacy sprite characters.
        if (visualController != null)
        {
            visualController.UpdateLocomotion(facingYaw, groundSpeed, forwardAcceleration);
        }
        else if (spriteController != null)
        {
            spriteController.UpdateAnimation(input, groundSpeed > MovementMath.StartMovingSpeed && askedSpeed > 0f);
        }
    }

    /// <summary>
    /// Walking off a kerb or down a slope: if the capsule was on the ground last frame, is not now, and is not
    /// jumping, and there is ground within a step's height below, put it straight back down instead of letting it
    /// float down under gravity (which reads as gliding off every step).
    /// </summary>
    private void SnapDownToGround()
    {
        if (!wasGrounded || characterController.isGrounded || verticalVelocity > 0f)
        {
            return;
        }

        float reach = characterController.stepOffset + characterController.skinWidth * 2f;
        float radius = characterController.radius * 0.9f;
        Vector3 bottomSphere = transform.TransformPoint(characterController.center)
            + Vector3.down * (characterController.height * 0.5f - characterController.radius);
        if (Physics.SphereCast(bottomSphere, radius, Vector3.down, out RaycastHit hit, reach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider != characterController)
        {
            characterController.Move(Vector3.down * hit.distance);
            verticalVelocity = -GroundStickSpeed;
        }
    }

    /// <summary>
    /// Stops the horizontal velocity at the edge of the built town (<see cref="WalkableArea"/>): per axis, a step
    /// that would cross the edge is cut so it ends exactly on it, so walking diagonally into the edge slides along
    /// it and walking straight into it stops (and the legs go idle) - no wall to bounce off, no jitter. Done on
    /// the velocity before CharacterController.Move, never by setting transform.position, which the controller
    /// would fight.
    /// </summary>
    private void KeepInsideWalkableArea()
    {
        float dt = Time.deltaTime;
        if (!hasWalkableArea || dt <= 0f)
        {
            return;
        }

        Vector3 position = transform.position;
        float stepX = MovementMath.ClampStep(position.x, currentHorizontalVelocity.x * dt, walkableArea.xMin, walkableArea.xMax);
        float stepZ = MovementMath.ClampStep(position.z, currentHorizontalVelocity.z * dt, walkableArea.yMin, walkableArea.yMax);
        currentHorizontalVelocity.x = stepX / dt;
        currentHorizontalVelocity.z = stepZ / dt;
    }

    public Vector2 GetMovementInput()
    {
        Vector2 input = Vector2.zero;

        // No walking while a sheet, Mali's blocking box, the reveal or pause is open (DESIGN_SPEC 7.2).
        if (UiModal.IsAnyOpen)
        {
            return input;
        }

        if (useKeyboardInput)
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) input.y -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) input.x -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) input.x += 1f;
            }
#endif
        }

        // Add virtual mobile joystick input if provided
        input += virtualJoystickInput;

        return Vector2.ClampMagnitude(input, 1f);
    }

    /// <summary>
    /// External entrypoint for mobile on-screen joystick or UI touch pad
    /// </summary>
    public void SetVirtualJoystickInput(Vector2 joystickVector)
    {
        virtualJoystickInput = Vector2.ClampMagnitude(joystickVector, 1f);
    }

    public void InteractWithBuilding(string buildingType)
    {
        Debug.Log($"Interacting with {buildingType}");
    }
}
