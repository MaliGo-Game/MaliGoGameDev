using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>
    /// The pure numbers behind driving the player's car (no Unity objects, so the logic tests can check them): how
    /// the joystick becomes throttle, brake, reverse and steering, how the speed changes, how sharply the car can
    /// turn at a given speed, and where the driver gets out.
    ///
    /// Scale: the town is built at about 0.27 units per metre, a road tile is 1 u wide, and the parked sedan is
    /// about 0.47 x 0.79 u. <see cref="MaxForwardSpeed"/> is ~13 m/s (~47 km/h), a suburban street speed.
    ///
    /// Controls (camera-relative, like walking): push the stick the way you want to go. The car steers toward it
    /// and drives; pushing it behind the car brakes, and once nearly stopped, reverses (backing the rear toward the
    /// stick). Letting go coasts to a stop.
    /// </summary>
    public static class VehicleMath
    {
        // ---------------------------------------------------------------- speeds

        /// <summary>Top speed forward (u/s): ~13 m/s, ~47 km/h.</summary>
        public const float MaxForwardSpeed = 3.5f;

        /// <summary>Top speed in reverse (u/s): ~4.4 m/s.</summary>
        public const float MaxReverseSpeed = 1.2f;

        /// <summary>Forward acceleration at full throttle (u/s^2), before drag: 0 to top speed in about 3 s.</summary>
        public const float Acceleration = 1.45f;

        /// <summary>Reverse acceleration at full throttle (u/s^2).</summary>
        public const float ReverseAcceleration = 1.5f;

        /// <summary>Deceleration while braking at full pedal (u/s^2): from top speed to a stop in ~0.7 s.</summary>
        public const float BrakeDeceleration = 5f;

        /// <summary>Constant slowing while coasting (u/s^2): tyres and engine braking.</summary>
        public const float RollingResistance = 0.45f;

        /// <summary>Air drag: slowing of <c>DragCoefficient * speed^2</c> (u/s^2), always on.</summary>
        public const float DragCoefficient = 0.06f;

        /// <summary>Stick tilt (0..1) below which the pedals are released (thumb drift).</summary>
        public const float InputDeadZone = 0.15f;

        /// <summary>Below this speed (u/s) the car counts as stopped: the stick behind it now reverses instead of
        /// braking.</summary>
        public const float StoppedSpeed = 0.15f;

        // ---------------------------------------------------------------- steering

        /// <summary>The tightest turn (radius of the path, u) at walking pace: ~2.4 m, tight enough for a 1 u road
        /// tile's bend at a crawl.</summary>
        public const float MinTurnRadius = 0.65f;

        /// <summary>Sideways grip (u/s^2): above walking pace the turn widens so the car never corners harder than
        /// this. At top speed the tightest turn is ~4 u.</summary>
        public const float MaxLateralAcceleration = 3f;

        /// <summary>The angle (degrees) between the car's heading and the stick at which the steering is at full
        /// lock; smaller angles steer proportionally, so the car settles onto the stick's line without weaving.</summary>
        public const float FullLockAngle = 30f;

        /// <summary>How fast the steering wheel turns toward the asked lock and back (full lock per second).</summary>
        public const float SteerRate = 3.5f;
        public const float SteerReturnRate = 5f;

        /// <summary>While reversing, the stick has to come this far round toward the front (degrees off the car's
        /// heading) before the car drives forward again (hysteresis against flickering between gears).</summary>
        public const float ForwardAgainAngle = 70f;

        /// <summary>The stick has to be this far behind the car (degrees off its heading) to brake or reverse.</summary>
        public const float ReverseAngle = 120f;

        /// <summary>The front wheels' drawn angle at full lock (degrees).</summary>
        public const float WheelVisualLock = 28f;

        // ---------------------------------------------------------------- driving

        /// <summary>
        /// Turns the stick (a flat world direction <paramref name="stickX"/>, <paramref name="stickZ"/>, length 0..1)
        /// into pedals and steering for a car heading <paramref name="carYaw"/> (degrees, Unity's convention: 0 = +Z,
        /// 90 = +X) at signed <paramref name="speed"/> (u/s, negative in reverse). <paramref name="reversing"/> is the
        /// gear: read and updated here. <paramref name="throttle"/> is -1..1 (positive pushes forward; against the
        /// motion it brakes); <paramref name="steer"/> is -1..1 (positive turns right, clockwise from above).
        /// </summary>
        public static void PedalsFromStick(float stickX, float stickZ, float carYaw, float speed, ref bool reversing,
            out float throttle, out float steer)
        {
            float magnitude = (float)System.Math.Sqrt(stickX * stickX + stickZ * stickZ);
            if (magnitude < InputDeadZone)
            {
                throttle = 0f;
                steer = 0f;
                if (speed > StoppedSpeed)
                {
                    reversing = false;
                }

                return;
            }

            magnitude = Mathf.Min(1f, magnitude);
            float stickYaw = MovementMath.YawOf(stickX, stickZ);
            float offFront = MovementMath.DeltaAngle(carYaw, stickYaw);
            float absOffFront = Mathf.Abs(offFront);

            if (reversing)
            {
                if (absOffFront < ForwardAgainAngle || speed > StoppedSpeed)
                {
                    reversing = false;
                }
            }
            else if (absOffFront > ReverseAngle && speed <= StoppedSpeed)
            {
                reversing = true;
            }

            if (reversing)
            {
                // Back the rear toward the stick: reversing flips which way the steering turns the body.
                float offRear = MovementMath.DeltaAngle(carYaw + 180f, stickYaw);
                throttle = -magnitude * PedalForAngle(Mathf.Abs(offRear));
                steer = -Mathf.Clamp(offRear / FullLockAngle, -1f, 1f);
                return;
            }

            if (absOffFront > ReverseAngle)
            {
                // Rolling forward with the stick behind: brake in a straight line.
                throttle = -magnitude;
                steer = 0f;
                return;
            }

            throttle = magnitude * PedalForAngle(absOffFront);
            steer = Mathf.Clamp(offFront / FullLockAngle, -1f, 1f);
        }

        /// <summary>Less pedal while the stick is well off the heading, so a sharp turn is taken slower.</summary>
        static float PedalForAngle(float absAngle)
        {
            return Mathf.Lerp(1f, 0.4f, absAngle / ReverseAngle);
        }

        /// <summary>
        /// The new signed speed after <paramref name="deltaTime"/> with <paramref name="throttle"/> (-1..1): pushing
        /// the way the car rolls accelerates (less as drag builds), pushing against it brakes (stopping at zero, never
        /// flipping straight into the other direction in the same step), nothing coasts down.
        /// </summary>
        public static float UpdateSpeed(float speed, float throttle, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return speed;
            }

            float drag = DragCoefficient * speed * speed;
            const float Pedal = 0.01f;

            if (throttle > Pedal)
            {
                if (speed < 0f)
                {
                    return Mathf.Min(0f, speed + BrakeDeceleration * throttle * deltaTime);
                }

                float next = speed + (Acceleration * throttle - drag) * deltaTime;
                return Mathf.Clamp(next, 0f, MaxForwardSpeed);
            }

            if (throttle < -Pedal)
            {
                if (speed > 0f)
                {
                    return Mathf.Max(0f, speed + BrakeDeceleration * throttle * deltaTime);
                }

                float next = speed - (ReverseAcceleration * -throttle - drag) * deltaTime;
                return Mathf.Clamp(next, -MaxReverseSpeed, 0f);
            }

            float slowing = (RollingResistance + drag) * deltaTime;
            if (speed > 0f)
            {
                return Mathf.Max(0f, speed - slowing);
            }

            return Mathf.Min(0f, speed + slowing);
        }

        /// <summary>Moves the steering (-1..1) toward <paramref name="target"/> at a steering-wheel rate; back toward
        /// the middle a little faster.</summary>
        public static float UpdateSteer(float current, float target, float deltaTime)
        {
            float rate = Mathf.Abs(target) < Mathf.Abs(current) || target * current < 0f ? SteerReturnRate : SteerRate;
            float step = rate * Mathf.Max(0f, deltaTime);
            if (Mathf.Abs(target - current) <= step)
            {
                return target;
            }

            return current + Mathf.Sign(target - current) * step;
        }

        /// <summary>The tightest turn radius (u) at <paramref name="speed"/>: <see cref="MinTurnRadius"/> at a
        /// crawl, widening with speed^2 so the sideways grip never exceeds <see cref="MaxLateralAcceleration"/>.</summary>
        public static float TurnRadius(float speed)
        {
            return Mathf.Max(MinTurnRadius, speed * speed / MaxLateralAcceleration);
        }

        /// <summary>
        /// How fast the car turns (degrees per second, positive clockwise from above) at signed <paramref name="speed"/>
        /// with <paramref name="steer"/> (-1..1). A parked car does not turn; reversing turns the body the other way,
        /// as a real car does.
        /// </summary>
        public static float YawRate(float speed, float steer)
        {
            float radius = TurnRadius(speed);
            return Mathf.Clamp(steer, -1f, 1f) * speed / radius * (180f / Mathf.PI);
        }

        /// <summary>The wheels' roll (degrees) for a car that went <paramref name="distance"/> u on wheels of
        /// <paramref name="wheelRadius"/> u.</summary>
        public static float WheelRollDegrees(float distance, float wheelRadius)
        {
            return wheelRadius > 0f ? distance / wheelRadius * (180f / Mathf.PI) : 0f;
        }

        // ---------------------------------------------------------------- getting out

        /// <summary>The order the doors are tried in when getting out: the driver's (South African cars are
        /// right-hand drive, so the right side), the passenger's, then behind and in front.</summary>
        public static readonly int[] ExitSides = { 0, 1, 2, 3 };

        /// <summary>
        /// Where a person of <paramref name="personRadius"/> stands after getting out on side <paramref name="side"/>
        /// (0 right/driver, 1 left, 2 behind, 3 in front) of a car at (<paramref name="carX"/>, <paramref name="carZ"/>)
        /// heading <paramref name="carYaw"/> with half sizes <paramref name="halfWidth"/> x <paramref name="halfLength"/>,
        /// leaving <paramref name="gap"/> between them.
        /// </summary>
        public static void ExitSpot(float carX, float carZ, float carYaw, float halfWidth, float halfLength,
            float personRadius, float gap, int side, out float x, out float z)
        {
            float localX;
            float localZ;
            switch (side)
            {
                case 1:
                    localX = -(halfWidth + personRadius + gap);
                    localZ = 0f;
                    break;
                case 2:
                    localX = 0f;
                    localZ = -(halfLength + personRadius + gap);
                    break;
                case 3:
                    localX = 0f;
                    localZ = halfLength + personRadius + gap;
                    break;
                default:
                    // Beside the driver's seat, a little forward of the middle.
                    localX = halfWidth + personRadius + gap;
                    localZ = halfLength * 0.15f;
                    break;
            }

            InteriorMath.RotateY(localX, localZ, carYaw, out float rx, out float rz);
            x = carX + rx;
            z = carZ + rz;
        }
    }
}
