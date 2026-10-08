using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>
    /// Notices that the player is pushing the stick but not getting anywhere, over a short window: the step asked for
    /// (after the walkable-edge clamp, so walking into the town's edge on purpose is not "stuck") is compared with the
    /// distance really covered. Pure (no Unity objects), so the logic tests can drive it frame by frame.
    /// </summary>
    public struct StuckDetector
    {
        /// <summary>How long (s) the player must have been asking to move before a verdict.</summary>
        public const float WindowSeconds = 0.4f;

        /// <summary>Stuck when less than this share of the asked distance was covered over the window.</summary>
        public const float ProgressRatio = 0.1f;

        /// <summary>A window that asked for less than this (u) is not judged (turning on the spot, a feather touch).</summary>
        public const float MinimumAskedDistance = 0.04f;

        float askedSeconds;
        float askedDistance;
        float movedDistance;

        /// <summary>
        /// Adds one frame: <paramref name="asked"/> is the horizontal distance the movement code tried to cover this
        /// frame, <paramref name="moved"/> the distance it really covered. Returns true once per window when the
        /// player has been asking to move for <see cref="WindowSeconds"/> and covered under
        /// <see cref="ProgressRatio"/> of it. Any frame without asking resets the window.
        /// </summary>
        public bool Update(float asked, float moved, float deltaTime)
        {
            if (asked <= 0f || deltaTime <= 0f)
            {
                Reset();
                return false;
            }

            askedSeconds += deltaTime;
            askedDistance += asked;
            movedDistance += Mathf.Max(0f, moved);
            if (askedSeconds < WindowSeconds)
            {
                return false;
            }

            bool stuck = askedDistance >= MinimumAskedDistance && movedDistance < askedDistance * ProgressRatio;
            Reset();
            return stuck;
        }

        /// <summary>Starts a fresh window.</summary>
        public void Reset()
        {
            askedSeconds = 0f;
            askedDistance = 0f;
            movedDistance = 0f;
        }
    }

    /// <summary>
    /// The geometry the unstuck guard and safe placement use, on the flat XZ plane: pushing a circle (the player's
    /// capsule seen from above) out of a box footprint (a parked car, a building), and sliding along a surface.
    /// </summary>
    public static class UnstuckMath
    {
        /// <summary>
        /// For a circle of <paramref name="radius"/> at (<paramref name="localX"/>, <paramref name="localZ"/>) in a box's
        /// own frame (centre at the origin, half sizes <paramref name="halfX"/>, <paramref name="halfZ"/>): false when
        /// it does not overlap the box; otherwise the shortest push (<paramref name="pushX"/>, <paramref name="pushZ"/>)
        /// that leaves it just touching the box from outside. From inside the box this is out through the nearest face;
        /// from outside, straight away from the nearest point.
        /// </summary>
        public static bool PushOutOfBox(float localX, float localZ, float halfX, float halfZ, float radius,
            out float pushX, out float pushZ)
        {
            pushX = 0f;
            pushZ = 0f;
            float absX = Mathf.Abs(localX);
            float absZ = Mathf.Abs(localZ);
            bool inside = absX < halfX && absZ < halfZ;

            if (inside)
            {
                float outX = halfX - absX + radius;
                float outZ = halfZ - absZ + radius;
                if (outX <= outZ)
                {
                    pushX = Mathf.Sign(localX) * outX;
                }
                else
                {
                    pushZ = Mathf.Sign(localZ) * outZ;
                }

                return true;
            }

            float nearestX = Mathf.Clamp(localX, -halfX, halfX);
            float nearestZ = Mathf.Clamp(localZ, -halfZ, halfZ);
            float dx = localX - nearestX;
            float dz = localZ - nearestZ;
            float distance = (float)System.Math.Sqrt(dx * dx + dz * dz);
            if (distance >= radius)
            {
                return false;
            }

            if (distance < 1e-6f)
            {
                // Exactly on an edge: out along the face it lies on.
                if (absX >= halfX)
                {
                    pushX = Mathf.Sign(localX) * radius;
                }
                else
                {
                    pushZ = Mathf.Sign(localZ) * radius;
                }

                return true;
            }

            float scale = (radius - distance) / distance;
            pushX = dx * scale;
            pushZ = dz * scale;
            return true;
        }

        /// <summary>
        /// The part of the flat direction (<paramref name="dirX"/>, <paramref name="dirZ"/>) along a surface whose
        /// flat outward normal is (<paramref name="normalX"/>, <paramref name="normalZ"/>): the into-the-surface part
        /// is removed. Returns its length (0 when walking straight into the surface).
        /// </summary>
        public static float Slide(float dirX, float dirZ, float normalX, float normalZ, out float slideX, out float slideZ)
        {
            float nLength = (float)System.Math.Sqrt(normalX * normalX + normalZ * normalZ);
            if (nLength < 1e-6f)
            {
                slideX = dirX;
                slideZ = dirZ;
                return (float)System.Math.Sqrt(dirX * dirX + dirZ * dirZ);
            }

            float nx = normalX / nLength;
            float nz = normalZ / nLength;
            float into = dirX * nx + dirZ * nz;
            if (into >= 0f)
            {
                slideX = dirX;
                slideZ = dirZ;
            }
            else
            {
                slideX = dirX - into * nx;
                slideZ = dirZ - into * nz;
            }

            return (float)System.Math.Sqrt(slideX * slideX + slideZ * slideZ);
        }
    }
}
