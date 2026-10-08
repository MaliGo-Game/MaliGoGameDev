using System.Collections.Generic;
using MaliGo.Core;
using MaliGo.UI;
using MaliGo.UI.Kit;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// The player's own car, parked in their driveway (the scene's "Vehicle_PlayerCar", a Kenney sedan), which they can
    /// get into and drive (<see cref="MaliGoFeatures.Driving"/>).
    ///
    /// Getting in: standing beside it, the one prompt and the HUD action button offer "Drive" (<see cref="IInteractable"/>,
    /// through the <see cref="InteractionArbiter"/>). The player's controller and capsule are switched off, their
    /// model's renderers hidden, and from then on the player object rides at the car's position each frame (not
    /// parented: the car root is scaled 0.31, which a child would inherit). While driving, the car's "Get out" is the
    /// arbiter's <see cref="InteractionArbiter.Exclusive"/> interactable, so no door or scenario spot can fire from the
    /// road, and <see cref="BuildingInteriors"/> ignores doorways.
    ///
    /// Driving: the same joystick (or WASD) as walking, camera-relative: push the way you want to go. The numbers
    /// are in <see cref="VehicleMath"/> (0 to 3.5 u/s, ~47 km/h, in about 3 s; brakes; reverse; coasting; tighter turns
    /// at low speed). The car is a kinematic body moved by swept box casts, not a dynamic Rigidbody: every frame a
    /// box the car's size is cast along the step, so at any speed or frame rate it stops at a wall, a house, a tree or
    /// another car instead of tunnelling, and it slides along what it grazes (losing speed by how head-on the hit
    /// was). It cannot flip or bounce, there is nothing to tune per device, and it costs a few physics queries a frame,
    /// the robust choice on a low-end Android phone. It is held inside the road network's rectangle
    /// (<see cref="WalkableArea.TryGetDriveArea"/>). The wheels roll with the distance covered and the front wheels
    /// turn with the steering.
    ///
    /// Getting out: the same button, now "Get out", parks the car and puts the player beside the driver's door (the
    /// right: South African cars are right-hand drive), or the other side, behind or in front, whichever is clear
    /// ground in the walkable town (<see cref="PlayerPlacement"/>). If none is, the player stays in and is told to
    /// find more space.
    ///
    /// Money: driving costs nothing in the beta. Fuel (and the car's running costs for the travel profile's "car"
    /// choice) is a future money hook, deliberately not charged here; energy, time and money rules are untouched.
    /// </summary>
    public class DrivableCar : MonoBehaviour, IInteractable
    {
        public const string CarObjectName = "Vehicle_PlayerCar";

        /// <summary>How much wider the camera's view is while driving.</summary>
        public const float DrivingViewScale = 1.35f;

        /// <summary>The "Drive" prompt is offered within this distance of the car's centre (u): the car is ~0.8 u long.</summary>
        const float EnterRadius = 0.62f;

        /// <summary>Gap kept between the car's box and what it drives into (u).</summary>
        const float CastSkin = 0.01f;

        /// <summary>The swept box starts this far above the ground (u), so the lawn and road tiles are never hit.</summary>
        const float GroundClearance = 0.03f;

        /// <summary>The largest push out of something the car ended up overlapping, per frame (u).</summary>
        const float MaxDepenetration = 0.08f;

        /// <summary>The car stops dead when it hits something at least this head-on (cosine of the angle).</summary>
        const float HeadOnHit = 0.7f;

        /// <summary>Gap between the car and the player getting out (u).</summary>
        const float ExitGap = 0.04f;

        const string NoRoomMessage = "No room to get out here. Drive somewhere with a bit more space.";

        static DrivableCar instance;
        static float drivingEndedAt = -100f;

        readonly Collider[] overlaps = new Collider[8];
        readonly List<Renderer> hiddenRenderers = new List<Renderer>();

        BoxCollider box;
        Vector3 boxCentreWorld;
        Vector3 halfExtents;
        float wheelRadius = 0.09f;
        readonly List<Transform> frontWheels = new List<Transform>();
        readonly List<Transform> rearWheels = new List<Transform>();
        float wheelRoll;

        bool driving;
        float speed;
        float steer;
        float yaw;
        bool reversing;

        Transform player;
        CharacterController playerController;
        MaliGoPlayerController mover;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            drivingEndedAt = -100f;
        }

        /// <summary>True while the player is driving.</summary>
        public static bool IsDriving => instance != null && instance.driving;

        /// <summary>The camera's view scale: <see cref="DrivingViewScale"/> while driving, 1 on foot.</summary>
        public static float ViewScale => IsDriving ? DrivingViewScale : 1f;

        /// <summary>Seconds (unscaled) since the player last got out of the car.</summary>
        public static float SecondsSinceDriving => Time.unscaledTime - drivingEndedAt;

        /// <summary>Where the camera should look ahead of the car (world offset): along its heading by
        /// <see cref="CameraLeadSeconds"/> of travel, so more of the road in front is on screen. Zero on foot.</summary>
        public static Vector3 CameraLead
        {
            get
            {
                if (!IsDriving)
                {
                    return Vector3.zero;
                }

                return Quaternion.Euler(0f, instance.yaw, 0f) * Vector3.forward * (instance.speed * CameraLeadSeconds);
            }
        }

        /// <summary>Seconds of travel the camera looks ahead while driving (at top speed ~1 u).</summary>
        const float CameraLeadSeconds = 0.3f;

        /// <summary>Makes the player's car drivable (once per world scene). Null when driving is switched off or the
        /// scene has no player car.</summary>
        public static DrivableCar Ensure()
        {
            if (!MaliGoFeatures.Driving)
            {
                return null;
            }

            if (instance != null)
            {
                return instance;
            }

            GameObject car = GameObject.Find(CarObjectName);
            if (car == null)
            {
                Debug.Log("[DrivableCar] No player car in this scene; driving is not offered.");
                return null;
            }

            DrivableCar drivable = car.GetComponent<DrivableCar>();
            return drivable != null ? drivable : car.AddComponent<DrivableCar>();
        }

        /// <summary>
        /// Takes the player out of the car at once (before something else moves them, e.g. the morning's wake-up by the
        /// bed): beside the car where there is room. A no-op when nobody is driving.
        /// </summary>
        public static void EjectDriver()
        {
            if (instance == null || !instance.driving)
            {
                return;
            }

            if (!instance.TryFindExitSpot(out Vector3 spot))
            {
                VehicleMath.ExitSpot(instance.transform.position.x, instance.transform.position.z, instance.yaw,
                    instance.halfExtents.x, instance.halfExtents.z, PlayerPlacement.PlayerRadius, ExitGap, 0,
                    out float x, out float z);
                spot = new Vector3(x, instance.transform.position.y, z);
            }

            instance.StopDriving(spot);
        }

        // ================================================================ lifecycle

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            yaw = transform.eulerAngles.y;
            box = VehicleColliders.Fit(gameObject);
            MeasureBox();
            FindWheels();

            // A moving collider belongs on a kinematic body (PhysX moves it cheaply instead of rebuilding statics).
            Rigidbody body = GetComponent<Rigidbody>();
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        void OnEnable()
        {
            InteractionArbiter.Register(this);
        }

        void OnDisable()
        {
            InteractionArbiter.Unregister(this);
            if (driving && gameObject.scene.isLoaded)
            {
                EjectDriver();
            }
        }

        void OnDestroy()
        {
            if (ReferenceEquals(InteractionArbiter.Exclusive, this))
            {
                InteractionArbiter.Exclusive = null;
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        void MeasureBox()
        {
            Vector3 scale = transform.lossyScale;
            if (box != null)
            {
                halfExtents = new Vector3(Mathf.Abs(box.size.x * scale.x), Mathf.Abs(box.size.y * scale.y),
                    Mathf.Abs(box.size.z * scale.z)) * 0.5f;
                boxCentreWorld = Vector3.Scale(box.center, scale);
            }
            else
            {
                // The sedan at the scene's 0.31 scale.
                halfExtents = new Vector3(0.24f, 0.21f, 0.4f);
                boxCentreWorld = new Vector3(0f, 0.21f, 0f);
            }
        }

        void FindWheels()
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (child.name.StartsWith("wheel-front", System.StringComparison.Ordinal))
                {
                    frontWheels.Add(child);
                }
                else if (child.name.StartsWith("wheel-back", System.StringComparison.Ordinal))
                {
                    rearWheels.Add(child);
                }
            }

            Transform any = frontWheels.Count > 0 ? frontWheels[0] : rearWheels.Count > 0 ? rearWheels[0] : null;
            MeshFilter filter = any != null ? any.GetComponent<MeshFilter>() : null;
            if (filter != null && filter.sharedMesh != null)
            {
                wheelRadius = Mathf.Max(0.01f, filter.sharedMesh.bounds.extents.y * Mathf.Abs(any.lossyScale.y));
            }
        }

        // ================================================================ IInteractable

        public Vector3 InteractPosition => driving && player != null ? player.position : transform.position;

        public float InteractRadius => driving ? 1000f : EnterRadius;

        public InteractPriority Priority => InteractPriority.World;

        public bool IsAvailable => isActiveAndEnabled && MaliGoFeatures.Driving && (driving || !WalkableArea.IsInterior);

        public bool IsEnabled => true;

        public string PromptText => "Your car";

        public string ActionVerb => driving ? "Get out" : "Drive";

        public string PromptIcon => "car";

        public void Interact()
        {
            if (driving)
            {
                TryGetOut();
            }
            else
            {
                GetIn();
            }
        }

        public void OnDisabledTap()
        {
        }

        // ================================================================ in and out

        void GetIn()
        {
            GameObject found = GameObject.FindWithTag("Player");
            if (found == null)
            {
                return;
            }

            player = found.transform;
            playerController = found.GetComponent<CharacterController>();
            mover = found.GetComponent<MaliGoPlayerController>();

            if (mover != null)
            {
                mover.enabled = false;
            }

            if (playerController != null)
            {
                playerController.enabled = false;
            }

            hiddenRenderers.Clear();
            foreach (Renderer renderer in found.GetComponentsInChildren<Renderer>())
            {
                if (renderer.enabled)
                {
                    renderer.enabled = false;
                    hiddenRenderers.Add(renderer);
                }
            }

            driving = true;
            speed = 0f;
            steer = 0f;
            reversing = false;
            yaw = transform.eulerAngles.y;
            InteractionArbiter.Exclusive = this;
            RidePlayer();
        }

        void TryGetOut()
        {
            if (!TryFindExitSpot(out Vector3 spot))
            {
                NoticeBanner.Show(NoRoomMessage);
                return;
            }

            StopDriving(spot);
        }

        /// <summary>The first clear spot of: the driver's side, the passenger's side, behind, in front; then the nearest
        /// clear ground around the driver's side.</summary>
        bool TryFindExitSpot(out Vector3 spot)
        {
            Physics.SyncTransforms();
            Vector3 position = transform.position;
            for (int i = 0; i < VehicleMath.ExitSides.Length; i++)
            {
                VehicleMath.ExitSpot(position.x, position.z, yaw, halfExtents.x, halfExtents.z,
                    PlayerPlacement.PlayerRadius, ExitGap, VehicleMath.ExitSides[i], out float x, out float z);
                Vector3 candidate = new Vector3(x, position.y, z);
                if (PlayerPlacement.IsClear(candidate))
                {
                    spot = candidate;
                    return true;
                }
            }

            VehicleMath.ExitSpot(position.x, position.z, yaw, halfExtents.x, halfExtents.z,
                PlayerPlacement.PlayerRadius, ExitGap, 0, out float driverX, out float driverZ);
            return PlayerPlacement.TryFindClearSpot(new Vector3(driverX, position.y, driverZ), out spot);
        }

        /// <summary>Parks the car and puts the player back on their feet at <paramref name="spot"/>, facing the way the
        /// car faces.</summary>
        void StopDriving(Vector3 spot)
        {
            driving = false;
            speed = 0f;
            steer = 0f;
            reversing = false;
            drivingEndedAt = Time.unscaledTime;
            if (ReferenceEquals(InteractionArbiter.Exclusive, this))
            {
                InteractionArbiter.Exclusive = null;
            }

            UpdateWheels(0f);

            if (player != null)
            {
                player.position = spot + Vector3.up * 0.02f;
            }

            if (playerController != null)
            {
                playerController.enabled = true;
            }

            for (int i = 0; i < hiddenRenderers.Count; i++)
            {
                if (hiddenRenderers[i] != null)
                {
                    hiddenRenderers[i].enabled = true;
                }
            }

            hiddenRenderers.Clear();

            if (mover != null)
            {
                mover.enabled = true;
                mover.SetVirtualJoystickInput(Vector2.zero);
                mover.SetFacing(yaw);
            }

            Physics.SyncTransforms();
        }

        // ================================================================ driving

        void Update()
        {
            if (!driving)
            {
                return;
            }

            if (player == null)
            {
                // The player object went away (scene change): just park.
                driving = false;
                if (ReferenceEquals(InteractionArbiter.Exclusive, this))
                {
                    InteractionArbiter.Exclusive = null;
                }

                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                RidePlayer();
                return;
            }

            Vector2 input = mover != null && !UiModal.IsAnyOpen ? mover.GetMovementInput() : Vector2.zero;
            Vector3 stick = CameraRelative(input);

            VehicleMath.PedalsFromStick(stick.x, stick.z, yaw, speed, ref reversing, out float throttle, out float steerTarget);
            steer = VehicleMath.UpdateSteer(steer, steerTarget, dt);
            speed = VehicleMath.UpdateSpeed(speed, throttle, dt);

            // Turn first (only if the turned car fits where it is), then move along the new heading.
            float turnedYaw = yaw + VehicleMath.YawRate(speed, steer) * dt;
            if (!Mathf.Approximately(turnedYaw, yaw) && !Overlapping(transform.position, turnedYaw))
            {
                yaw = turnedYaw;
            }

            Vector3 position = transform.position;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 step = Sweep(position, forward * (speed * dt));
            step = KeepInsideDriveArea(position, step);
            position += step;
            position += Depenetration(position);

            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            UpdateWheels(Vector3.Dot(step, forward));
            RidePlayer();
        }

        /// <summary>The stick as a flat world direction, the same camera-relative way walking reads it.</summary>
        static Vector3 CameraRelative(Vector2 input)
        {
            Vector3 camForward = Vector3.forward;
            Vector3 camRight = Vector3.right;
            Camera view = Camera.main;
            if (view != null)
            {
                camForward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
                camRight = Vector3.ProjectOnPlane(view.transform.right, Vector3.up).normalized;
            }

            return camRight * input.x + camForward * input.y;
        }

        /// <summary>The swept box's centre and half sizes for the car at <paramref name="position"/> heading
        /// <paramref name="heading"/>: lifted clear of the ground and a skin smaller than the car.</summary>
        void CastShape(Vector3 position, float heading, out Vector3 centre, out Vector3 half, out Quaternion rotation)
        {
            rotation = Quaternion.Euler(0f, heading, 0f);
            float bottom = boxCentreWorld.y - halfExtents.y;
            float lift = Mathf.Max(0f, GroundClearance - bottom);
            centre = position + rotation * boxCentreWorld + Vector3.up * (lift * 0.5f);
            half = new Vector3(Mathf.Max(0.01f, halfExtents.x - CastSkin), Mathf.Max(0.01f, halfExtents.y - lift * 0.5f),
                Mathf.Max(0.01f, halfExtents.z - CastSkin));
        }

        /// <summary>
        /// How far the car really goes when asked to move by <paramref name="delta"/>: up to whatever the swept box
        /// meets first, then a slide along it with what is left. Speed is lost by how head-on the hit was (a wall met
        /// square on stops the car dead).
        /// </summary>
        Vector3 Sweep(Vector3 position, Vector3 delta)
        {
            float distance = delta.magnitude;
            if (distance < 1e-5f)
            {
                return Vector3.zero;
            }

            Vector3 direction = delta / distance;
            CastShape(position, yaw, out Vector3 centre, out Vector3 half, out Quaternion rotation);
            if (!Physics.BoxCast(centre, half, direction, out RaycastHit hit, rotation, distance + CastSkin,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) || hit.collider == box)
            {
                return delta;
            }

            float allowed = Mathf.Max(0f, hit.distance - CastSkin);
            Vector3 moved = direction * allowed;

            Vector3 normal = new Vector3(hit.normal.x, 0f, hit.normal.z);
            if (normal.sqrMagnitude < 1e-6f)
            {
                speed = 0f;
                return moved;
            }

            normal.Normalize();
            float headOn = Mathf.Clamp01(-Vector3.Dot(direction, normal));
            speed *= headOn >= HeadOnHit ? 0f : 1f - headOn;

            float remaining = distance - allowed;
            float slideLength = UnstuckMath.Slide(direction.x * remaining, direction.z * remaining, normal.x, normal.z,
                out float slideX, out float slideZ);
            if (headOn >= HeadOnHit || slideLength < 1e-5f)
            {
                return moved;
            }

            Vector3 slideDirection = new Vector3(slideX, 0f, slideZ) / slideLength;
            CastShape(position + moved, yaw, out centre, out half, out rotation);
            if (Physics.BoxCast(centre, half, slideDirection, out RaycastHit slideHit, rotation, slideLength + CastSkin,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && slideHit.collider != box)
            {
                return moved + slideDirection * Mathf.Max(0f, slideHit.distance - CastSkin);
            }

            return moved + slideDirection * slideLength;
        }

        /// <summary>True when the car turned to <paramref name="heading"/> at <paramref name="position"/> would overlap
        /// something solid (then it does not turn this frame).</summary>
        bool Overlapping(Vector3 position, float heading)
        {
            CastShape(position, heading, out Vector3 centre, out Vector3 half, out Quaternion rotation);
            int count = Physics.OverlapBoxNonAlloc(centre, half, overlaps, rotation, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (overlaps[i] != null && overlaps[i] != box && !(overlaps[i] is CharacterController))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A small push out of anything the car overlaps at <paramref name="position"/> (rounding, a turn at
        /// the edge of a fit), capped per frame.</summary>
        Vector3 Depenetration(Vector3 position)
        {
            if (box == null)
            {
                return Vector3.zero;
            }

            CastShape(position, yaw, out Vector3 centre, out Vector3 half, out Quaternion rotation);
            int count = Physics.OverlapBoxNonAlloc(centre, half, overlaps, rotation, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Vector3 push = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider other = overlaps[i];
                if (other == null || other == box || other is CharacterController)
                {
                    continue;
                }

                if (Physics.ComputePenetration(box, position, rotation, other, other.transform.position,
                        other.transform.rotation, out Vector3 direction, out float distance))
                {
                    direction.y = 0f;
                    push += direction * distance;
                }
            }

            return Vector3.ClampMagnitude(push, MaxDepenetration);
        }

        /// <summary>Cuts the step so the car's centre stays inside the road network's rectangle (shrunk by half the
        /// car's width), per axis, like walking at the town's edge.</summary>
        Vector3 KeepInsideDriveArea(Vector3 position, Vector3 step)
        {
            if (!WalkableArea.TryGetDriveArea(out Rect area))
            {
                return step;
            }

            float margin = halfExtents.x;
            float minX = area.xMin;
            float maxX = area.xMax;
            float minZ = area.yMin;
            float maxZ = area.yMax;
            MovementMath.Shrink(ref minX, ref maxX, margin);
            MovementMath.Shrink(ref minZ, ref maxZ, margin);
            step.x = MovementMath.ClampStep(position.x, step.x, minX, maxX);
            step.z = MovementMath.ClampStep(position.z, step.z, minZ, maxZ);
            return step;
        }

        /// <summary>Keeps the hidden player at the car (so the camera, the arbiter and anything that reads the player's
        /// position follow the car).</summary>
        void RidePlayer()
        {
            if (player != null)
            {
                player.position = transform.position;
            }
        }

        /// <summary>Rolls the wheels by <paramref name="distance"/> (u, signed) and turns the front ones with the
        /// steering.</summary>
        void UpdateWheels(float distance)
        {
            wheelRoll = Mathf.Repeat(wheelRoll + VehicleMath.WheelRollDegrees(distance, wheelRadius), 360f);
            Quaternion roll = Quaternion.Euler(wheelRoll, 0f, 0f);
            Quaternion turn = Quaternion.Euler(0f, steer * VehicleMath.WheelVisualLock, 0f);
            for (int i = 0; i < frontWheels.Count; i++)
            {
                frontWheels[i].localRotation = turn * roll;
            }

            for (int i = 0; i < rearWheels.Count; i++)
            {
                rearWheels[i].localRotation = roll;
            }
        }
    }
}
