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

    /// <summary>
    /// The highest step the capsule climbs (u, ~26 cm: a kerb or a door sill). The spawner sets 0.066, but the
    /// editor's prefab setup used 0.25, more than half a car's height; capped here so no controller ever climbs a
    /// car, a planter or a fence.
    /// </summary>
    const float MaxStepOffset = 0.07f;

    /// <summary>The ground snap only lands on surfaces at most this steep (cos 45 degrees): floors, not walls.</summary>
    const float MinGroundNormalY = 0.7f;

    /// <summary>How far one unstuck nudge slides the player along what they are stuck on (u).</summary>
    const float UnstickSlideDistance = 0.05f;

    /// <summary>Extra reach (u) when looking for what the capsule is stuck in or against.</summary>
    const float UnstickProbe = 0.03f;

    /// <summary>A collider wider than this (u) is never pushed out of (the lawn, the boundary walls): only slid along.</summary>
    const float MaxPushOutSize = 4f;

    /// <summary>A side contact (wall, car) is remembered for the unstuck slide for this many frames.</summary>
    const int WallContactFrames = 10;

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

    // Unstuck guard: asking to move but not moving (see StuckDetector), the last wall touched, and a car stood on.
    private StuckDetector stuckDetector;
    private Vector3 lastWallNormal;
    private int lastWallFrame = int.MinValue;
    private Collider vehicleUnderfoot;
    private readonly Collider[] stuckOverlaps = new Collider[8];

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
        if (characterController != null && characterController.stepOffset > MaxStepOffset)
        {
            characterController.stepOffset = MaxStepOffset;
        }

        if (spriteController == null)
            spriteController = GetComponentInChildren<CharacterSpriteController>();

        if (visualController == null)
            visualController = GetComponentInChildren<PlayerCharacterVisualController>();

        // Start facing the way the model already faces (prefab or spawner rotation), not snapping to +Z.
        facingYaw = visualController != null ? visualController.transform.eulerAngles.y : transform.eulerAngles.y;

        // Measured once per scene (cached in WalkableArea); false outside the town scene, then nothing is clamped.
        // Re-read when the active area switches between the town and a room.
        RefreshWalkableArea();
    }

    void OnEnable()
    {
        WalkableArea.Changed += RefreshWalkableArea;
        // Switched back on after driving: the area may have changed while this listened to nothing.
        RefreshWalkableArea();
        stuckDetector.Reset();
        vehicleUnderfoot = null;
    }

    void OnDisable()
    {
        WalkableArea.Changed -= RefreshWalkableArea;
    }

    private void RefreshWalkableArea()
    {
        hasWalkableArea = WalkableArea.TryGet(out walkableArea);
    }

    /// <summary>Turns the body to <paramref name="yaw"/> degrees at once (after a door or on waking), so the next
    /// step goes that way instead of the old heading.</summary>
    public void SetFacing(float yaw)
    {
        facingYaw = yaw;
        currentSpeed = 0f;
        currentHorizontalVelocity = Vector3.zero;
        if (visualController != null)
        {
            visualController.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
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
            vehicleUnderfoot = null;
            characterController.Move(finalMoveVector * dt);
            SnapDownToGround();
            wasGrounded = characterController.isGrounded;

            if (vehicleUnderfoot != null)
            {
                // Never stand on a car (teleported or pushed up there somehow): step straight off its nearest side.
                StepOffVehicle(vehicleUnderfoot);
                stuckDetector.Reset();
            }
            else
            {
                Vector3 step = transform.position - positionBefore;
                step.y = 0f;
                float asked = new Vector3(currentHorizontalVelocity.x, 0f, currentHorizontalVelocity.z).magnitude * dt;
                if (stuckDetector.Update(asked, step.magnitude, dt))
                {
                    TryUnstick();
                }
            }
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
    /// Only ground counts: cars (the Vehicle layer) are left out of the cast, and a hit must be floor-flat
    /// (<see cref="MinGroundNormalY"/>), so the snap never pulls the player onto a car's bonnet or the edge of a prop.
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
                VehicleColliders.AllButVehicles, QueryTriggerInteraction.Ignore)
            && hit.collider != characterController
            && hit.normal.y >= MinGroundNormalY)
        {
            characterController.Move(Vector3.down * hit.distance);
            verticalVelocity = -GroundStickSpeed;
        }
    }

    /// <summary>
    /// Remembers what the capsule touched this Move: a car under its feet (it must step off) and the last wall
    /// or car side it pressed against (the unstuck slide follows it).
    /// </summary>
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.collider == null)
        {
            return;
        }

        if (hit.normal.y >= 0.5f)
        {
            if (hit.collider.gameObject.layer == VehicleColliders.Layer)
            {
                vehicleUnderfoot = hit.collider;
            }

            return;
        }

        lastWallNormal = new Vector3(hit.normal.x, 0f, hit.normal.z);
        lastWallFrame = Time.frameCount;
    }

    /// <summary>
    /// Puts the player back on the ground beside <paramref name="vehicle"/>: out through the nearest side of its box,
    /// clear of it, at ground height. The controller is switched off for the move (it would fight a teleport).
    /// </summary>
    private void StepOffVehicle(Collider vehicle)
    {
        Vector3 push = PushOutOf(vehicle, transform.position, characterController.radius + characterController.skinWidth * 2f);
        Vector3 target = transform.position + push;
        target.y = Mathf.Min(target.y, vehicle.bounds.min.y + characterController.skinWidth);
        Teleport(target);
        verticalVelocity = -GroundStickSpeed;
    }

    /// <summary>
    /// The unstuck guard (<see cref="StuckDetector"/>: the stick held for 0.4 s and under 10 % of the asked distance
    /// covered). First, anything the capsule is inside or overlapping (a car's box, a building it was put inside) is
    /// left by the shortest way out; failing that, the player slides a little along the last wall they pressed into,
    /// toward where they face. The lawn and the boundary walls are never pushed out of.
    /// </summary>
    private void TryUnstick()
    {
        float radius = characterController.radius;
        Vector3 feet = transform.position;
        Vector3 bottom = feet + Vector3.up * (radius + characterController.skinWidth);
        Vector3 top = feet + Vector3.up * Mathf.Max(radius + characterController.skinWidth, characterController.height - radius);
        int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius + UnstickProbe, stuckOverlaps,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

        Vector3 push = Vector3.zero;
        for (int i = 0; i < count; i++)
        {
            Collider other = stuckOverlaps[i];
            if (other == null || other == characterController || other is CharacterController)
            {
                continue;
            }

            Bounds bounds = other.bounds;
            if (bounds.max.y <= feet.y + characterController.stepOffset)
            {
                continue; // the ground, a kerb, a doormat
            }

            if (bounds.size.x > MaxPushOutSize || bounds.size.z > MaxPushOutSize)
            {
                continue;
            }

            if (!(other is BoxCollider) && !IsInsideShell(other, feet))
            {
                continue; // only touching a wall or a tree: the slide below handles it
            }

            push += PushOutOf(other, feet + push, radius + characterController.skinWidth * 2f);
        }

        if (push.sqrMagnitude <= 1e-6f)
        {
            // Inside a hollow mesh shell the capsule overlaps no triangle at all; looking down from above finds the
            // roof over the player instead.
            Vector3 from = feet + Vector3.up * 2.5f;
            if (Physics.Raycast(from, Vector3.down, out RaycastHit overhead, 2.5f, VehicleColliders.AllButVehicles,
                    QueryTriggerInteraction.Ignore)
                && overhead.collider != characterController
                && overhead.point.y > feet.y + characterController.stepOffset)
            {
                Bounds bounds = overhead.collider.bounds;
                if (bounds.size.x <= MaxPushOutSize && bounds.size.z <= MaxPushOutSize)
                {
                    push = PushOutOf(overhead.collider, feet, radius + characterController.skinWidth * 2f);
                }
            }
        }

        if (push.sqrMagnitude > 1e-6f)
        {
            Teleport(transform.position + push);
            if (push.magnitude > UnstickSlideDistance * 0.5f)
            {
                Debug.Log($"[MaliGoPlayerController] Unstuck: pushed {push.magnitude:0.000} u out of what held the player.");
                return;
            }

            // Only a hair: merely pressed against it, so slide along it as well.
        }

        if (Time.frameCount - lastWallFrame > WallContactFrames)
        {
            return;
        }

        Vector3 facing = Quaternion.Euler(0f, facingYaw, 0f) * Vector3.forward;
        float length = UnstuckMath.Slide(facing.x, facing.z, lastWallNormal.x, lastWallNormal.z, out float slideX, out float slideZ);
        if (length <= 0.2f)
        {
            return; // pressing straight into a wall on purpose: nothing to free, the legs already stand still
        }

        Vector3 slide = new Vector3(slideX, 0f, slideZ) / length;
        characterController.Move(slide * UnstickSlideDistance + Vector3.down * characterController.skinWidth);
    }

    /// <summary>
    /// The shortest flat push that takes a capsule of <paramref name="radius"/> at <paramref name="feet"/> out of
    /// <paramref name="other"/>: through the box's own faces for a BoxCollider (a car), through the world bounds
    /// otherwise (the town's buildings are mesh shells turned in steps of 90 degrees, so their bounds are their
    /// footprints). Zero when they do not overlap.
    /// </summary>
    private static Vector3 PushOutOf(Collider other, Vector3 feet, float radius)
    {
        if (other is BoxCollider box)
        {
            Transform t = box.transform;
            Vector3 local = t.InverseTransformPoint(feet) - box.center;
            float scale = Mathf.Max(0.0001f, Mathf.Abs(t.lossyScale.x));
            if (UnstuckMath.PushOutOfBox(local.x, local.z, box.size.x * 0.5f, box.size.z * 0.5f, radius / scale,
                    out float localPushX, out float localPushZ))
            {
                Vector3 world = t.TransformVector(new Vector3(localPushX, 0f, localPushZ));
                world.y = 0f;
                return world;
            }

            return Vector3.zero;
        }

        Bounds bounds = other.bounds;
        if (UnstuckMath.PushOutOfBox(feet.x - bounds.center.x, feet.z - bounds.center.z, bounds.extents.x, bounds.extents.z,
                radius, out float pushX, out float pushZ))
        {
            return new Vector3(pushX, 0f, pushZ);
        }

        return Vector3.zero;
    }

    /// <summary>True when <paramref name="feet"/> is inside <paramref name="other"/>'s footprint with its roof
    /// overhead (seen looking down from above): the player is inside a building's mesh shell, not beside it.</summary>
    private bool IsInsideShell(Collider other, Vector3 feet)
    {
        Bounds bounds = other.bounds;
        if (Mathf.Abs(feet.x - bounds.center.x) >= bounds.extents.x || Mathf.Abs(feet.z - bounds.center.z) >= bounds.extents.z)
        {
            return false;
        }

        Vector3 from = new Vector3(feet.x, bounds.max.y + 0.5f, feet.z);
        float length = from.y - feet.y;
        return other.Raycast(new Ray(from, Vector3.down), out RaycastHit hit, length)
               && hit.point.y > feet.y + characterController.stepOffset;
    }

    /// <summary>Moves the player to <paramref name="position"/> with the controller off for the move.</summary>
    private void Teleport(Vector3 position)
    {
        bool wasEnabled = characterController.enabled;
        characterController.enabled = false;
        transform.position = position;
        characterController.enabled = wasEnabled;
        stuckDetector.Reset();
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
