using System;
using MaliGo.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MaliGo.World
{
    /// <summary>
    /// The part of MaliGoWorld the player may walk on: the built town, not the empty lawn around it. Derived once
    /// per scene from the renderers under the generator's "Roads_Network" and "Residential_Area" groups (roads,
    /// houses, the bank, fences, paths and yard trees - see MaliGoWorldGenerator), shrunk by
    /// <see cref="EdgeMargin"/>. The scene's lawn is 35 x 35 units, while the town covers about 11 x 9 in its
    /// middle, so the old invisible walls (World_Boundaries at x = +/-8, z = -5 / 7) let the player run well out
    /// onto bare grass. Those walls are only the fallback here, if the two groups cannot be found.
    ///
    /// The rectangle is on the XZ plane: <c>Rect.x</c>/<c>width</c> are world X, <c>Rect.y</c>/<c>height</c> are
    /// world Z. Computed lazily and cached per scene, so no per-frame searches.
    ///
    /// Inside a building (<see cref="EnterInterior"/>, called by <c>BuildingInteriors</c>) the ACTIVE area switches to
    /// that room's floor, shrunk by <see cref="InteriorEdgeMargin"/>, and the camera bounds collapse to the room's
    /// framing point with <see cref="InteriorViewSize"/> as the view size; <see cref="ExitInterior"/> switches back to
    /// the town. <see cref="Changed"/> is raised on every switch. The rooms are built inside the town's XZ footprint
    /// (high above it), so code that read the town rectangle once still lets the player walk the whole room; the
    /// room's walls keep them in it.
    ///
    /// The streets added around the town at runtime (<c>TownExpansion</c>) are not under the two measured groups (their
    /// outer houses would let the player wander behind them); instead their road rectangle is added with
    /// <see cref="SetTownExtension"/>, which grows the town rectangle (and so the camera bounds) to include it, and
    /// <see cref="SetDriveArea"/> records the rectangle the player's car is kept in.
    /// </summary>
    public static class WalkableArea
    {
        /// <summary>How far inside the outermost town renderers the player's centre must stay (u, ~0.5 m).</summary>
        public const float EdgeMargin = 0.15f;

        /// <summary>Anything smaller than this on either axis is treated as "not found" (u).</summary>
        const float MinimumSize = 1f;

        static readonly string[] DevelopedGroups = { "Roads_Network", "Residential_Area" };

        const string WestWall = "Boundary_West";
        const string EastWall = "Boundary_East";
        const string SouthWall = "Boundary_South";
        const string NorthWall = "Boundary_North";

        /// <summary>How far inside a room's floor edge the player's centre must stay (u, ~0.4 m; the walls and
        /// furniture colliders do the rest).</summary>
        public const float InteriorEdgeMargin = 0.1f;

        static int cachedSceneHandle;
        static bool cachedValid;
        static bool cachedFound;
        static Rect cachedArea;

        static bool extensionSet;
        static int extensionSceneHandle;
        static Rect extension;

        static bool driveAreaSet;
        static int driveAreaSceneHandle;
        static Rect driveArea;

        static bool interiorActive;
        static int interiorSceneHandle;
        static Rect interiorFloor;
        static Vector2 interiorFocus;
        static float interiorViewSize;

        /// <summary>Raised whenever the active area switches between the town and a room.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cachedValid = false;
            cachedFound = false;
            cachedSceneHandle = 0;
            cachedArea = default;
            extensionSet = false;
            extensionSceneHandle = 0;
            extension = default;
            driveAreaSet = false;
            driveAreaSceneHandle = 0;
            driveArea = default;
            interiorActive = false;
            interiorSceneHandle = 0;
            interiorFloor = default;
            interiorFocus = default;
            interiorViewSize = 0f;
            Changed = null;
        }

        /// <summary>True while the player is inside a room of the active scene.</summary>
        public static bool IsInterior => interiorActive && interiorSceneHandle == SceneManager.GetActiveScene().handle;

        /// <summary>The orthographic size that frames the current room, or 0 outside (keep the town's size).</summary>
        public static float InteriorViewSize => IsInterior ? interiorViewSize : 0f;

        /// <summary>
        /// Makes the room whose floor is <paramref name="floor"/> (world XZ) the active area. The camera's follow
        /// point is held at <paramref name="focus"/> (world XZ) with <paramref name="viewSize"/> as its size.
        /// </summary>
        public static void EnterInterior(Rect floor, Vector2 focus, float viewSize)
        {
            interiorActive = true;
            interiorSceneHandle = SceneManager.GetActiveScene().handle;
            interiorFloor = floor;
            interiorFocus = focus;
            interiorViewSize = Mathf.Max(0f, viewSize);
            Changed?.Invoke();
        }

        /// <summary>Makes the town the active area again (a no-op outside).</summary>
        public static void ExitInterior()
        {
            if (!interiorActive)
            {
                return;
            }

            interiorActive = false;
            interiorViewSize = 0f;
            Changed?.Invoke();
        }

        /// <summary>
        /// Grows the town rectangle of the active scene to include <paramref name="area"/> (world XZ, shrunk by
        /// <see cref="EdgeMargin"/> like the measured town) and raises <see cref="Changed"/>, so the player and the
        /// camera pick it up at once.
        /// </summary>
        public static void SetTownExtension(Rect area)
        {
            float minX = area.xMin;
            float maxX = area.xMax;
            float minZ = area.yMin;
            float maxZ = area.yMax;
            MovementMath.Shrink(ref minX, ref maxX, EdgeMargin);
            MovementMath.Shrink(ref minZ, ref maxZ, EdgeMargin);
            extension = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            extensionSet = true;
            extensionSceneHandle = SceneManager.GetActiveScene().handle;
            Changed?.Invoke();
        }

        /// <summary>Records the rectangle (world XZ) the player's car is kept in for the active scene.</summary>
        public static void SetDriveArea(Rect area)
        {
            driveArea = area;
            driveAreaSet = true;
            driveAreaSceneHandle = SceneManager.GetActiveScene().handle;
        }

        /// <summary>The rectangle (world XZ) the player's car is kept in: the road network; false when the scene has
        /// none (then the car is held by the town rectangle, or nothing).</summary>
        public static bool TryGetDriveArea(out Rect area)
        {
            if (driveAreaSet && driveAreaSceneHandle == SceneManager.GetActiveScene().handle)
            {
                area = driveArea;
                return true;
            }

            return TryGetTown(out area);
        }

        /// <summary>
        /// The ACTIVE walkable rectangle (XZ): the current room's floor (shrunk by <see cref="InteriorEdgeMargin"/>)
        /// while inside, otherwise the town. False only outside, when the scene has no town to measure (e.g.
        /// character creation) - then nothing should be clamped.
        /// </summary>
        public static bool TryGet(out Rect area)
        {
            bool hasTown = TryGetTown(out Rect town);
            bool inside = IsInterior;
            InteriorMath.ActiveArea(inside, town.xMin, town.xMax, town.yMin, town.yMax,
                interiorFloor.xMin, interiorFloor.xMax, interiorFloor.yMin, interiorFloor.yMax, InteriorEdgeMargin,
                out float minX, out float maxX, out float minZ, out float maxZ);
            area = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return inside || hasTown;
        }

        /// <summary>
        /// The rectangle (XZ) the camera's follow point is kept in: the town outside, a single point (the room's
        /// framing point) inside. False when there is neither.
        /// </summary>
        public static bool TryGetCameraBounds(out Rect bounds)
        {
            bool hasTown = TryGetTown(out Rect town);
            bool inside = IsInterior;
            InteriorMath.CameraBounds(inside, town.xMin, town.xMax, town.yMin, town.yMax, interiorFocus.x, interiorFocus.y,
                out float minX, out float maxX, out float minZ, out float maxZ);
            bounds = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            return inside || hasTown;
        }

        /// <summary>The town's walkable rectangle (XZ), whatever the active area is; false when the scene has no
        /// town to measure.</summary>
        public static bool TryGetTown(out Rect area)
        {
            int handle = SceneManager.GetActiveScene().handle;
            if (!cachedValid || handle != cachedSceneHandle)
            {
                cachedSceneHandle = handle;
                cachedValid = true;
                cachedFound = Measure(out cachedArea);
            }

            area = cachedArea;
            bool found = cachedFound;
            if (extensionSet && extensionSceneHandle == handle)
            {
                area = found
                    ? Rect.MinMaxRect(Mathf.Min(area.xMin, extension.xMin), Mathf.Min(area.yMin, extension.yMin),
                        Mathf.Max(area.xMax, extension.xMax), Mathf.Max(area.yMax, extension.yMax))
                    : extension;
                found = true;
            }

            return found;
        }

        static bool Measure(out Rect area)
        {
            string source;
            if (TryRendererBounds(out float minX, out float maxX, out float minZ, out float maxZ))
            {
                source = "town renderers";
            }
            else if (TryBoundaryWalls(out minX, out maxX, out minZ, out maxZ))
            {
                source = "World_Boundaries";
            }
            else
            {
                area = default;
                return false;
            }

            MovementMath.Shrink(ref minX, ref maxX, EdgeMargin);
            MovementMath.Shrink(ref minZ, ref maxZ, EdgeMargin);
            area = Rect.MinMaxRect(minX, minZ, maxX, maxZ);
            Debug.Log($"[WalkableArea] x {minX:0.00}..{maxX:0.00}, z {minZ:0.00}..{maxZ:0.00} (from {source}, margin {EdgeMargin}).");
            return true;
        }

        static bool TryRendererBounds(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            bool any = false;
            Bounds bounds = default;
            foreach (string groupName in DevelopedGroups)
            {
                GameObject group = GameObject.Find(groupName);
                if (group == null)
                {
                    continue;
                }

                foreach (Renderer renderer in group.GetComponentsInChildren<Renderer>())
                {
                    if (!any)
                    {
                        bounds = renderer.bounds;
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }
            }

            minX = bounds.min.x;
            maxX = bounds.max.x;
            minZ = bounds.min.z;
            maxZ = bounds.max.z;
            return any && bounds.size.x >= MinimumSize && bounds.size.z >= MinimumSize;
        }

        /// <summary>The inner faces of the generator's four invisible walls.</summary>
        static bool TryBoundaryWalls(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = maxX = minZ = maxZ = 0f;
            if (!TryWall(WestWall, out Bounds west) || !TryWall(EastWall, out Bounds east)
                || !TryWall(SouthWall, out Bounds south) || !TryWall(NorthWall, out Bounds north))
            {
                return false;
            }

            minX = west.max.x;
            maxX = east.min.x;
            minZ = south.max.z;
            maxZ = north.min.z;
            return maxX - minX >= MinimumSize && maxZ - minZ >= MinimumSize;
        }

        static bool TryWall(string wallName, out Bounds bounds)
        {
            GameObject wall = GameObject.Find(wallName);
            Collider collider = wall != null ? wall.GetComponent<Collider>() : null;
            bounds = collider != null ? collider.bounds : default;
            return collider != null;
        }
    }
}
