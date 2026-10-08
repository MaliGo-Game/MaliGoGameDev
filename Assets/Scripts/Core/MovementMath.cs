using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>
    /// The pure numbers behind walking the town (no Unity objects, so the logic tests can check them): how fast
    /// the player goes for a joystick tilt, how quickly they speed up, slow down and turn, how a step is kept
    /// inside the walkable area, and how the legs are animated so the feet stay planted on the ground.
    ///
    /// Scale: the world is built at about 0.27 units per metre and the player is about 0.475 units tall
    /// (Kenney's characterMedium at 0.12, see PlayerCharacterSpawner), so 1 m/s is about 0.27 u/s.
    /// </summary>
    public static class MovementMath
    {
        // ---------------------------------------------------------------- speeds and input

        /// <summary>
        /// Top speed, joystick pushed all the way (or a key held): an easy jog of about 3.1 m/s. The run clip
        /// plants its feet at <see cref="RunClipGroundSpeed"/> (0.93 u/s at 1x), so here it plays at ~0.91x, a
        /// cadence of ~165 steps a minute - what a real jogger does at this speed.
        /// </summary>
        public const float JogSpeed = 0.85f;

        /// <summary>
        /// Speed just past the joystick dead zone: a relaxed walk of about 1.2 m/s, with the legs on the
        /// shortest gait (<see cref="WalkGait"/>) at ~0.86x, ~120 steps a minute. Nothing slower is asked for
        /// while the stick is held, so the legs never play as a slow-motion run.
        /// </summary>
        public const float WalkSpeed = 0.32f;

        /// <summary>Joystick tilt (0..1) below which the player stands still (thumb drift on a phone).</summary>
        public const float InputDeadZone = 0.12f;

        /// <summary>Shapes the tilt between walk and jog: above 1, more of the stick's travel is spent walking.</summary>
        public const float InputCurveExponent = 1.6f;

        /// <summary>Seconds from standing to <see cref="JogSpeed"/>, and from it back to standing (a person
        /// needs a step or two to stop, so a little longer than starting).</summary>
        public const float AccelerateSeconds = 0.25f;
        public const float DecelerateSeconds = 0.32f;

        /// <summary>Speed change per second while speeding up and while slowing down.</summary>
        public const float Acceleration = JogSpeed / AccelerateSeconds;
        public const float Deceleration = JogSpeed / DecelerateSeconds;

        // ---------------------------------------------------------------- turning

        /// <summary>Fastest body turn, degrees per second: a full about-face takes ~0.3 s.</summary>
        public const float TurnDegreesPerSecond = 630f;

        /// <summary>
        /// While the body still faces more than <see cref="TurnFullSpeedDegrees"/> away from where the stick
        /// points, the speed asked for is cut, down to nothing at <see cref="TurnStandStillDegrees"/>: the
        /// character turns on the spot first instead of sliding sideways or backwards.
        /// </summary>
        public const float TurnFullSpeedDegrees = 35f;
        public const float TurnStandStillDegrees = 110f;

        // ---------------------------------------------------------------- legs (Animator)

        /// <summary>
        /// Ground distance a planted foot travels backward over one full cycle (two steps) of Kenney's
        /// Idle/Run blend, in the model's own units (the armature "Root" space; the mesh is 3.77 of them tall),
        /// for run weights 0.5, 0.6 ... 1.0 (index 0 = <see cref="WalkGait"/>).
        ///
        /// Measured, not guessed: the FBX files were read offline and the skeleton's forward kinematics were
        /// evaluated 64 times per cycle, blending Idle and Run per bone the way Mecanim does (translations
        /// lerped, rotations nlerped, both clips at the same normalized time). The planted foot is the toe
        /// joint while it is at its lowest; its backward speed relative to the hips is the ground speed at which
        /// that foot does not slide. Run alone: 5.18 units per 0.667 s cycle (16 frames at 24 fps; sampling only
        /// the 16 keyed frames gives 5.10, within 2%). A run step is then 0.69 body heights, a normal jog. The toe
        /// joint touches down at 0.013 units, the same height as in the idle and rest poses where the soles of
        /// the mesh sit at 0, so the model's pivot is exactly at its soles.
        /// Re-measure with tools/fbx_stride/blend.py if the clips change.
        /// </summary>
        static readonly float[] StridePerCycle = { 2.691f, 3.267f, 3.730f, 4.206f, 4.819f, 5.181f };

        /// <summary>Length of Kenney's "Root|Idle" (32 frames, idle.fbx is 30 fps) and "Root|Run" (16 frames,
        /// run.fbx is 24 fps), read from each file's take.</summary>
        public const float IdleClipSeconds = 32f / 30f;
        public const float RunClipSeconds = 16f / 24f;

        /// <summary>The player's model scale (PlayerCharacterSpawner.CharacterModelScale): model units to world.</summary>
        public const float DefaultModelScale = 0.12f;

        /// <summary>
        /// Ground speed (u/s) at which the run clip, played at 1x on the 0.12-scale character, plants its feet:
        /// 5.181 model units per cycle x 0.12 / 0.667 s = 0.93 u/s (~3.4 m/s). The old estimate (1.25) made the
        /// feet skate forward 27% at the old 1.0 u/s. If the feet look like they slip on a phone, tune
        /// <see cref="StrideTuning"/> rather than this.
        /// </summary>
        public static readonly float RunClipGroundSpeed = StridePerCycle[StridePerCycle.Length - 1] * DefaultModelScale / RunClipSeconds;

        /// <summary>
        /// One knob for on-device tuning of foot sliding: scales every measured stride. Raise it if the feet
        /// look like they slide backward (legs too fast for the ground), lower it if they skate forward.
        /// </summary>
        public const float StrideTuning = 1f;

        /// <summary>Run weight of the Idle/Run blend at <see cref="WalkSpeed"/> (shortest, walk-like steps:
        /// a third of the run's foot lift, no flight phase) and the smallest ever used while moving.</summary>
        public const float WalkGait = 0.5f;

        public const float MinPlayback = 0.5f;
        public const float MaxPlayback = 1.3f;

        /// <summary>Speed below which the legs show Idle; the Animator crossfades at this "Speed" value.</summary>
        public const float StartMovingSpeed = 0.06f;
        public const float StopMovingSpeed = 0.03f;

        // ---------------------------------------------------------------- body lean

        /// <summary>Forward lean (degrees) per u/s of steady speed: runners lean slightly into the run.</summary>
        public const float LeanDegreesPerSpeed = 2.5f;

        /// <summary>Extra lean (degrees) per u/s² of speeding up; slowing down leans back.</summary>
        public const float LeanDegreesPerAcceleration = 1.4f;

        public const float MaxLeanDegrees = 6f;

        // ---------------------------------------------------------------- input and speed

        /// <summary>
        /// The speed asked for by a stick tilt of <paramref name="magnitude"/> (0..1): nothing inside the dead
        /// zone, then from <see cref="WalkSpeed"/> just past it to <see cref="JogSpeed"/> at full tilt, along
        /// <see cref="InputCurveExponent"/>.
        /// </summary>
        public static float SpeedForInput(float magnitude)
        {
            if (magnitude <= InputDeadZone)
            {
                return 0f;
            }

            float t = Mathf.Clamp01((magnitude - InputDeadZone) / (1f - InputDeadZone));
            float shaped = (float)System.Math.Pow(t, InputCurveExponent);
            return WalkSpeed + (JogSpeed - WalkSpeed) * shaped;
        }

        /// <summary>Moves <paramref name="current"/> speed toward <paramref name="target"/>, at
        /// <see cref="Acceleration"/> when speeding up and <see cref="Deceleration"/> when slowing down.</summary>
        public static float ApproachSpeed(float current, float target, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return current;
            }

            if (target > current)
            {
                return Mathf.Min(target, current + Acceleration * deltaTime);
            }

            return Mathf.Max(target, current - Deceleration * deltaTime);
        }

        // ---------------------------------------------------------------- turning

        /// <summary>Signed shortest angle (degrees, -180..180) from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static float DeltaAngle(float from, float to)
        {
            float delta = (to - from) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            return delta;
        }

        /// <summary>Turns the yaw <paramref name="current"/> toward <paramref name="target"/> by at most
        /// <see cref="TurnDegreesPerSecond"/> x <paramref name="deltaTime"/>, the short way round.</summary>
        public static float TurnTowards(float current, float target, float deltaTime)
        {
            float delta = DeltaAngle(current, target);
            float maxStep = TurnDegreesPerSecond * Mathf.Max(0f, deltaTime);
            if (Mathf.Abs(delta) <= maxStep)
            {
                return Normalize(target);
            }

            return Normalize(current + Mathf.Sign(delta) * maxStep);
        }

        /// <summary>Share (0..1) of the asked-for speed allowed while the body faces
        /// <paramref name="facingErrorDegrees"/> away from the stick's direction.</summary>
        public static float TurnSpeedFactor(float facingErrorDegrees)
        {
            float error = Mathf.Abs(facingErrorDegrees);
            if (error <= TurnFullSpeedDegrees)
            {
                return 1f;
            }

            if (error >= TurnStandStillDegrees)
            {
                return 0f;
            }

            float t = (error - TurnFullSpeedDegrees) / (TurnStandStillDegrees - TurnFullSpeedDegrees);
            return 1f - t * t * (3f - 2f * t); // smoothstep down
        }

        /// <summary>Yaw in degrees (0 = +Z, 90 = +X, Unity's convention) of the flat direction (x, z).</summary>
        public static float YawOf(float x, float z)
        {
            return Normalize((float)(System.Math.Atan2(x, z) * 180.0 / System.Math.PI));
        }

        static float Normalize(float degrees)
        {
            degrees %= 360f;
            return degrees < 0f ? degrees + 360f : degrees;
        }

        // ---------------------------------------------------------------- legs

        /// <summary>
        /// Run weight (0.5..1) of the Idle/Run blend for <paramref name="groundSpeed"/>: short walk steps at
        /// <see cref="WalkSpeed"/> and below, the full run stride at <see cref="JogSpeed"/>, linear between, so
        /// a person going faster lengthens their stride as well as quickening it.
        /// </summary>
        public static float GaitForSpeed(float groundSpeed)
        {
            float t = Mathf.Clamp01((groundSpeed - WalkSpeed) / (JogSpeed - WalkSpeed));
            return WalkGait + (1f - WalkGait) * t;
        }

        /// <summary>Length (seconds at 1x) of one cycle of the Idle/Run blend at run weight
        /// <paramref name="gait"/>: Mecanim plays a blend at the weighted average of its clips' lengths.</summary>
        public static float GaitCycleSeconds(float gait)
        {
            float w = Mathf.Clamp01(gait);
            return IdleClipSeconds + (RunClipSeconds - IdleClipSeconds) * w;
        }

        /// <summary>Planted-foot travel per cycle, model units, at run weight <paramref name="gait"/>
        /// (interpolated in <see cref="StridePerCycle"/>; below <see cref="WalkGait"/> it falls to 0 at idle).</summary>
        public static float StrideAtGait(float gait)
        {
            float w = Mathf.Clamp01(gait);
            if (w <= WalkGait)
            {
                return StridePerCycle[0] * (w / WalkGait) * StrideTuning;
            }

            float position = (w - WalkGait) / 0.1f;
            int index = Mathf.Min(StridePerCycle.Length - 2, (int)position);
            float t = Mathf.Clamp01(position - index);
            float stride = StridePerCycle[index] + (StridePerCycle[index + 1] - StridePerCycle[index]) * t;
            return stride * StrideTuning;
        }

        /// <summary>Ground speed (u/s) at which the legs plant their feet when the blend at run weight
        /// <paramref name="gait"/> plays at 1x on a model scaled by <paramref name="modelScale"/>.</summary>
        public static float GroundSpeedAtGait(float gait, float modelScale)
        {
            return StrideAtGait(gait) * modelScale / GaitCycleSeconds(gait);
        }

        /// <summary>
        /// Animator playback rate that makes the feet match <paramref name="groundSpeed"/> at run weight
        /// <paramref name="gait"/>: ground speed / the blend's own foot speed. Clamped so a tilt during a
        /// start or stop never freezes or spins the legs.
        /// </summary>
        public static float PlaybackRate(float groundSpeed, float gait, float modelScale)
        {
            if (groundSpeed <= 0f)
            {
                return 1f;
            }

            float natural = GroundSpeedAtGait(gait, modelScale > 0f ? modelScale : DefaultModelScale);
            if (natural <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Clamp(groundSpeed / natural, MinPlayback, MaxPlayback);
        }

        /// <summary>Steps per minute of the blend at run weight <paramref name="gait"/> and
        /// <paramref name="playback"/> (two steps per cycle); for tests and tuning.</summary>
        public static float StepsPerMinute(float gait, float playback)
        {
            return 120f * playback / GaitCycleSeconds(gait);
        }

        /// <summary>Kept for the run clip alone (run weight 1) on the default 0.12-scale character.</summary>
        public static float RunPlaybackRate(float groundSpeed)
        {
            return PlaybackRate(groundSpeed, 1f, DefaultModelScale);
        }

        /// <summary>Whether the legs should be moving, with a little hysteresis so a stop does not flicker
        /// between Idle and moving.</summary>
        public static bool IsMoving(float groundSpeed, bool wasMoving)
        {
            return groundSpeed > (wasMoving ? StopMovingSpeed : StartMovingSpeed);
        }

        // ---------------------------------------------------------------- lean

        /// <summary>Forward lean (degrees, + forward) for <paramref name="speed"/> u/s and
        /// <paramref name="forwardAcceleration"/> u/s², capped at <see cref="MaxLeanDegrees"/> either way.</summary>
        public static float LeanDegrees(float speed, float forwardAcceleration)
        {
            float lean = speed * LeanDegreesPerSpeed + forwardAcceleration * LeanDegreesPerAcceleration;
            return Mathf.Clamp(lean, -MaxLeanDegrees, MaxLeanDegrees);
        }

        // ---------------------------------------------------------------- walkable area

        /// <summary>
        /// The part of a one-axis <paramref name="step"/> that may be taken from <paramref name="position"/> without
        /// leaving [<paramref name="min"/>, <paramref name="max"/>]. A step toward the edge stops exactly on it (so
        /// the player slides along the edge on the other axis instead of bouncing); a step back inside is always
        /// allowed, and someone already outside is never pulled in by a jump.
        /// </summary>
        public static float ClampStep(float position, float step, float min, float max)
        {
            if (step > 0f && position + step > max)
            {
                return Mathf.Max(0f, max - position);
            }

            if (step < 0f && position + step < min)
            {
                return Mathf.Min(0f, min - position);
            }

            return step;
        }

        /// <summary>
        /// Shrinks the range [<paramref name="min"/>, <paramref name="max"/>] by <paramref name="margin"/> on each
        /// side; a range too small to shrink collapses to its centre.
        /// </summary>
        public static void Shrink(ref float min, ref float max, float margin)
        {
            if (max - min <= margin * 2f)
            {
                float centre = (min + max) * 0.5f;
                min = centre;
                max = centre;
                return;
            }

            min += margin;
            max -= margin;
        }
    }
}
