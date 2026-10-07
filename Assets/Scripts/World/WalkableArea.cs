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

        static int cachedSceneHandle;
        static bool cachedValid;
        static bool cachedFound;
        static Rect cachedArea;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            cachedValid = false;
            cachedFound = false;
            cachedSceneHandle = 0;
            cachedArea = default;
        }

        /// <summary>The walkable rectangle of the active scene (XZ), or false when the scene has no town to
        /// measure (e.g. character creation) - then nothing should be clamped.</summary>
        public static bool TryGet(out Rect area)
        {
            int handle = SceneManager.GetActiveScene().handle;
            if (!cachedValid || handle != cachedSceneHandle)
            {
                cachedSceneHandle = handle;
                cachedValid = true;
                cachedFound = Measure(out cachedArea);
            }

            area = cachedArea;
            return cachedFound;
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
