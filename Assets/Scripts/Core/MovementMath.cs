using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>
    /// The pure numbers behind walking the town (no Unity objects, so the logic tests can check them): the walk
    /// speed, how a step is kept inside the walkable area, and how fast the run clip plays for a given speed.
    /// </summary>
    public static class MovementMath
    {
        /// <summary>
        /// Walking speed in world units per second. The world is built at about 0.27 units per metre and the
        /// player is about 0.47 units tall, so 1.0 u/s is roughly 3.7 m/s: a brisk jog of about two body heights a
        /// second. (The old 4.5 u/s dated from when the character was imported about 100x too big: ~16 m/s.)
        /// </summary>
        public const float WalkSpeed = 1.0f;

        /// <summary>Seconds from standing to full speed, and from full speed to standing (the same feel the
        /// old 4.5 / 25 and 4.5 / 30 rates gave).</summary>
        public const float AccelerateSeconds = 0.18f;
        public const float DecelerateSeconds = 0.15f;

        /// <summary>
        /// The ground speed (u/s) at which Kenney's "Root|Run" clip (16 frames), on a 0.12-scale character, plants
        /// its feet without sliding. Estimated from the clip length and a jogging stride of ~0.8 body heights per
        /// step; tune on a phone if the legs look too fast or too slow.
        /// </summary>
        public const float RunClipGroundSpeed = 1.25f;

        public const float MinRunPlayback = 0.5f;
        public const float MaxRunPlayback = 1.2f;

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

        /// <summary>Animator playback rate for the run clip at <paramref name="groundSpeed"/> u/s, so the feet
        /// roughly match the ground (clamped so a light joystick tilt does not freeze the legs).</summary>
        public static float RunPlaybackRate(float groundSpeed)
        {
            if (groundSpeed <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(groundSpeed / RunClipGroundSpeed, MinRunPlayback, MaxRunPlayback);
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
