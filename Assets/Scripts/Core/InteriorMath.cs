using UnityEngine;

namespace MaliGo.Core
{
    /// <summary>
    /// The pure numbers behind walking into a building (no Unity objects, so the logic tests can check them): where a
    /// building's door is from its bounds and rotation, whether the player is standing in a doorway, which walkable
    /// rectangle and camera bounds apply inside and outside, how big the orthographic view must be to show a whole
    /// room, and whether an interactable is in the same space as the player.
    ///
    /// Rectangles are on the XZ plane as (minX, maxX, minZ, maxZ). Angles are in degrees. Rotations follow Unity's
    /// convention for a yaw about +Y: (x, z) turns to (x cos + z sin, -x sin + z cos).
    /// </summary>
    public static class InteriorMath
    {
        /// <summary>
        /// The rooms are built high above the town (see <c>BuildingInteriors</c>), so two things are in the same space
        /// when their heights differ by no more than this (u). The town is flat (y ~0); the rooms sit tens of units up.
        /// </summary>
        public const float SpaceHeightGap = 3f;

        /// <summary>True when something at height <paramref name="otherY"/> is in the player's space (town or one
        /// room), so the arbiter may offer it.</summary>
        public static bool SameSpace(float playerY, float otherY)
        {
            return Mathf.Abs(playerY - otherY) <= SpaceHeightGap;
        }

        /// <summary>Turns the XZ vector (<paramref name="x"/>, <paramref name="z"/>) by <paramref name="yawDegrees"/>
        /// about +Y, as <c>Quaternion.Euler(0, yaw, 0) * v</c> does.</summary>
        public static void RotateY(float x, float z, float yawDegrees, out float rx, out float rz)
        {
            double radians = yawDegrees * System.Math.PI / 180.0;
            float cos = (float)System.Math.Cos(radians);
            float sin = (float)System.Math.Sin(radians);
            rx = x * cos + z * sin;
            rz = -x * sin + z * cos;
            rx = Snap(rx);
            rz = Snap(rz);
        }

        /// <summary>
        /// The point in the middle of the face of an axis-aligned footprint (centre <paramref name="centreX"/>,
        /// <paramref name="centreZ"/>, half sizes <paramref name="extentX"/>, <paramref name="extentZ"/>) that the
        /// outward normal (<paramref name="normalX"/>, <paramref name="normalZ"/>) points through. The normal must be
        /// along X or Z (the buildings are turned in steps of 90 degrees).
        /// </summary>
        public static void DoorOnBounds(float centreX, float centreZ, float extentX, float extentZ,
            float normalX, float normalZ, out float doorX, out float doorZ)
        {
            float reach = Mathf.Abs(normalX) * extentX + Mathf.Abs(normalZ) * extentZ;
            doorX = centreX + normalX * reach;
            doorZ = centreZ + normalZ * reach;
        }

        /// <summary>
        /// True when the point (<paramref name="px"/>, <paramref name="pz"/>) is in the doorway box in front of the door
        /// at (<paramref name="doorX"/>, <paramref name="doorZ"/>): between <paramref name="behind"/> u behind the face
        /// and <paramref name="depth"/> u in front of it along the normal (which points toward where the player
        /// stands), and no more than <paramref name="halfWidth"/> to either side.
        /// </summary>
        public static bool InDoorway(float px, float pz, float doorX, float doorZ, float normalX, float normalZ,
            float behind, float depth, float halfWidth)
        {
            float dx = px - doorX;
            float dz = pz - doorZ;
            float along = dx * normalX + dz * normalZ;
            float side = -dx * normalZ + dz * normalX;
            return along >= -behind && along <= depth && Mathf.Abs(side) <= halfWidth;
        }

        /// <summary>The point <paramref name="distance"/> u out from the door along its normal.</summary>
        public static void InFrontOf(float doorX, float doorZ, float normalX, float normalZ, float distance,
            out float x, out float z)
        {
            x = doorX + normalX * distance;
            z = doorZ + normalZ * distance;
        }

        /// <summary>
        /// The rectangle the player may walk in: the room's floor shrunk by <paramref name="roomMargin"/> while
        /// <paramref name="inside"/>, otherwise the town rectangle unchanged.
        /// </summary>
        public static void ActiveArea(bool inside,
            float townMinX, float townMaxX, float townMinZ, float townMaxZ,
            float roomMinX, float roomMaxX, float roomMinZ, float roomMaxZ, float roomMargin,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            if (!inside)
            {
                minX = townMinX;
                maxX = townMaxX;
                minZ = townMinZ;
                maxZ = townMaxZ;
                return;
            }

            minX = roomMinX;
            maxX = roomMaxX;
            minZ = roomMinZ;
            maxZ = roomMaxZ;
            MovementMath.Shrink(ref minX, ref maxX, roomMargin);
            MovementMath.Shrink(ref minZ, ref maxZ, roomMargin);
        }

        /// <summary>
        /// The rectangle the camera's follow point is kept in: the town outside; inside, a single point (the room's
        /// framing point), so the camera holds the whole room still while the player walks around in it.
        /// </summary>
        public static void CameraBounds(bool inside,
            float townMinX, float townMaxX, float townMinZ, float townMaxZ, float focusX, float focusZ,
            out float minX, out float maxX, out float minZ, out float maxZ)
        {
            if (inside)
            {
                minX = maxX = focusX;
                minZ = maxZ = focusZ;
                return;
            }

            minX = townMinX;
            maxX = townMaxX;
            minZ = townMinZ;
            maxZ = townMaxZ;
        }

        /// <summary>
        /// Frames a room (floor <paramref name="width"/> x <paramref name="depth"/> centred on the origin, walls up to
        /// <paramref name="wallHeight"/>) for an orthographic camera at <paramref name="pitchDegrees"/> /
        /// <paramref name="yawDegrees"/>: returns the orthographic size that shows all of it on a screen of
        /// <paramref name="aspect"/> (width / height) with <paramref name="margin"/> (1.1 = 10 % spare), and the XZ
        /// shift from the floor centre the camera should look at so the room sits in the middle of the screen (the
        /// walls make the room taller on screen above the floor centre than below it).
        /// </summary>
        public static float RoomViewSize(float width, float depth, float wallHeight, float aspect,
            float pitchDegrees, float yawDegrees, float margin, out float focusShiftX, out float focusShiftZ)
        {
            double pitch = pitchDegrees * System.Math.PI / 180.0;
            double yaw = yawDegrees * System.Math.PI / 180.0;
            float sinPitch = (float)System.Math.Sin(pitch);
            float cosPitch = (float)System.Math.Cos(pitch);
            float sinYaw = (float)System.Math.Sin(yaw);
            float cosYaw = (float)System.Math.Cos(yaw);

            // The camera's up and right vectors (it looks down at the pitch, turned by the yaw).
            float upX = sinPitch * sinYaw, upY = cosPitch, upZ = sinPitch * cosYaw;
            float rightX = cosYaw, rightZ = -sinYaw;

            float minUp = float.MaxValue, maxUp = float.MinValue;
            float minRight = float.MaxValue, maxRight = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                float x = (i & 1) == 0 ? -width * 0.5f : width * 0.5f;
                float z = (i & 2) == 0 ? -depth * 0.5f : depth * 0.5f;
                float y = (i & 4) == 0 ? 0f : wallHeight;
                float up = x * upX + y * upY + z * upZ;
                float right = x * rightX + z * rightZ;
                minUp = Mathf.Min(minUp, up);
                maxUp = Mathf.Max(maxUp, up);
                minRight = Mathf.Min(minRight, right);
                maxRight = Mathf.Max(maxRight, right);
            }

            // Moving the look point along the ground in the camera's flat forward direction moves the picture up the
            // screen by sin(pitch) per unit: shift so the room's on-screen middle is the screen's middle.
            float centreUp = (minUp + maxUp) * 0.5f;
            float shift = sinPitch > 0.0001f ? centreUp / sinPitch : 0f;
            focusShiftX = Snap(sinYaw * shift);
            focusShiftZ = Snap(cosYaw * shift);

            float halfHeight = (maxUp - minUp) * 0.5f;
            float halfWidth = (maxRight - minRight) * 0.5f;
            float safeAspect = aspect > 0.01f ? aspect : 1f;
            return Mathf.Max(halfHeight, halfWidth / safeAspect) * Mathf.Max(1f, margin);
        }

        /// <summary>
        /// Whether the world should open with the player waking up inside their home: when a day is about to start
        /// (its morning has not run yet: the first day after onboarding, or the next day after a reveal that was left
        /// pending) or a reveal is still to be shown. A player loading in the middle of a day they already started
        /// begins outside, at the home's front door; so does one at the chapter end.
        /// </summary>
        public static bool WakeInHome(bool hasSave, bool chapterComplete, int revealPendingForDay, int morningLineDay, int currentDay)
        {
            if (!hasSave || chapterComplete)
            {
                return false;
            }

            return revealPendingForDay > 0 || morningLineDay != currentDay;
        }

        /// <summary>Moves <paramref name="current"/> toward <paramref name="target"/> by an exponential ease of
        /// <paramref name="sharpness"/> per second over <paramref name="deltaTime"/> (frame-rate independent).</summary>
        public static float Ease(float current, float target, float sharpness, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return current;
            }

            float t = 1f - (float)System.Math.Exp(-sharpness * deltaTime);
            return current + (target - current) * Mathf.Clamp01(t);
        }

        /// <summary>Cleans float noise from sin/cos (e.g. 6e-17 to 0) so exact comparisons on 90-degree turns hold.</summary>
        static float Snap(float value)
        {
            return Mathf.Abs(value) < 1e-5f ? 0f : value;
        }
    }
}
