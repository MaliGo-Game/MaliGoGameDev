using System;
using System.Collections;
using System.Collections.Generic;
using MaliGo.Characters;
using MaliGo.Core;
using MaliGo.Data;
using MaliGo.PlayerIdentity;
using MaliGo.UI.Kit;
using UnityEngine;

namespace MaliGo.World
{
    /// <summary>
    /// Walk-in rooms for the player's home and the Bank (the town's buildings are closed Kenney shells, so each room
    /// is a separate set built at runtime by <see cref="InteriorRoomBuilder"/>, no scene edits and no shader tricks).
    ///
    /// Where: each room is built once, when the world is wired, high above the middle of the town (Home at y 60, Bank
    /// at y 80), inside the town's XZ footprint. It is never on screen from the street (the camera's far plane ends
    /// long before the town below, and a coloured surround fills the view around the room), and the arbiter only
    /// offers what is at the player's height (<see cref="InteriorMath.SameSpace"/>), so the bed is never offered from
    /// the street and a street spot never from inside. A room is active only while the player is in it (no draw
    /// calls, no shadows on the town otherwise).
    ///
    /// Going in: each building's door side is worked out from its model (Player_House faces +Z toward the road; the
    /// Bank's commercial building has its doors on its own -Z side, which its 90 degree turn points west, -X) and
    /// its rotation. Walking into that doorway, or tapping its "Go in" prompt, fades the screen to the backdrop
    /// (<see cref="InteriorFade"/>), moves the player to the room's door facing in, switches the active walkable area
    /// and camera framing (<see cref="WalkableArea.EnterInterior"/>) and fades back. Walking into the room's door (or
    /// its "Go out" prompt on the mat) does the reverse, leaving the player just outside the building's door facing
    /// the street.
    ///
    /// Mornings: the day starts with the player by the bed. At world load the player is placed there when a day is
    /// about to start or a reveal is pending (<see cref="InteriorMath.WakeInHome"/>); a player loading in the middle
    /// of a day they already started stays at the spawn point outside the home's front door. On every
    /// <see cref="GameEvents.DayStarted"/> (after the reveal) the player is put by the bed again, fading in from the
    /// street if they are somehow not home.
    ///
    /// The player is moved by switching their CharacterController off, setting the position and switching it back
    /// on; nothing in the movement code is touched.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class BuildingInteriors : MonoBehaviour
    {
        public const string HomeId = "Home";
        public const string BankId = "Bank";

        const float HomeRoomHeight = 60f;
        const float BankRoomHeight = 80f;

        /// <summary>Each half of the door fade (s, unscaled).</summary>
        const float FadeSeconds = 0.25f;

        /// <summary>Street side: the "Go in" prompt stands this far out from the door, offered within this radius;
        /// coming out, the player is put this far out (outside both the prompt and the doorway).</summary>
        const float StreetPromptOffset = 0.18f;
        const float StreetPromptRadius = 0.3f;
        const float StreetExitDistance = 0.55f;

        /// <summary>The doorway box in front of a street door: from a little inside the building's bounds (the door
        /// is set back from them) to this far out.</summary>
        const float StreetDoorwayBehind = 0.12f;
        const float StreetDoorwayDepth = 0.22f;

        /// <summary>The doorway box in front of a room's door, and the "Go out" prompt's radius.</summary>
        const float RoomDoorwayBehind = 0.05f;
        const float RoomDoorwayDepth = 0.2f;
        const float ExitPromptRadius = 0.2f;

        /// <summary>Spare room around a room when it is framed (1.12 = 12 %).</summary>
        const float ViewMargin = 1.12f;

        /// <summary>How far above the floor a moved player is set down (u).</summary>
        const float SetDownHeight = 0.02f;

        /// <summary>Frames to wait after the player appears before the first placement, so the movement code has
        /// started (and measured the town) before the active area is switched to a room.</summary>
        const int SettleFrames = 2;

        /// <summary>If no player appears in this long (s), the opening cover is lifted anyway.</summary>
        const float PlayerWaitSeconds = 3f;

        /// <summary>Frames between searches for the player until it exists.</summary>
        const int PlayerSearchInterval = 10;

        struct DoorSpec
        {
            public string RoomId;
            public string Anchor;
            public float LocalNormalZ;
            public float HalfWidth;
            public Vector3 FallbackDoor;
            public Vector3 FallbackNormal;
            public string Prompt;
            public string Icon;
        }

        static readonly DoorSpec[] Doors =
        {
            // building-type-a: its plain front door (to the path stones and the road) is on the model's +Z side.
            new DoorSpec
            {
                RoomId = HomeId, Anchor = "Player_House", LocalNormalZ = 1f, HalfWidth = 0.15f,
                FallbackDoor = new Vector3(2f, 0f, -1.69f), FallbackNormal = Vector3.forward, Prompt = "Home", Icon = "home"
            },
            // Commercial building-a: the glass doors and shop window are on the model's -Z side; with the scene's 90
            // degree turn they face west. Both openings are inside the doorway's width.
            new DoorSpec
            {
                RoomId = BankId, Anchor = "Local_Bank_Building", LocalNormalZ = -1f, HalfWidth = 0.3f,
                FallbackDoor = new Vector3(-2.47f, 0f, 4.4f), FallbackNormal = Vector3.left, Prompt = "Bank", Icon = "pouch"
            }
        };

        sealed class Entry
        {
            public InteriorRoom Room;
            public Vector3 StreetDoor;
            public Vector3 StreetNormal;
            public float StreetHalfWidth;
        }

        static BuildingInteriors instance;

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        readonly List<Entry> entryList = new List<Entry>();
        InteriorRoomBuilder builder;
        InteriorFade fade;
        Entry current;

        Transform player;
        CharacterController playerController;
        Transform playerVisual;
        MaliGoCameraController cameraController;
        int playerFoundFrame;
        int nextPlayerSearchFrame;
        float startTime;
        bool placedInitially;
        bool transitioning;
        bool armed = true;
        int placedForDay = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        /// <summary>Builds the rooms (once per world scene) and returns the controller.</summary>
        public static BuildingInteriors Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            BuildingInteriors existing = FindFirstObjectByType<BuildingInteriors>();
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            return new GameObject("BuildingInteriors").AddComponent<BuildingInteriors>();
        }

        /// <summary>
        /// Where room <paramref name="roomId"/>'s own location is used (Home: beside the bed; Bank: at the teller
        /// counter) and how near the player must be. False if the room was not built (then the location stays at
        /// the street, as before).
        /// </summary>
        public static bool TryGetInteractPoint(string roomId, out Vector3 point, out float radius)
        {
            point = default;
            radius = 0f;
            if (instance == null || roomId == null || !instance.entries.TryGetValue(roomId, out Entry entry))
            {
                return false;
            }

            point = entry.Room.InteractPoint;
            radius = entry.Room.InteractRadius;
            return true;
        }

        /// <summary>The room the player is in ("Home", "Bank"), or null in the town.</summary>
        public static string CurrentRoomId => instance != null && instance.current != null ? instance.current.Room.Id : null;

        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }

            instance = this;
            startTime = Time.unscaledTime;
            fade = InteriorFade.Create(transform);

            try
            {
                Build();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingInteriors] Could not build the rooms; Home and Bank stay at the street: {ex}");
                entries.Clear();
                entryList.Clear();
            }

            // Cover the opening frames when the day starts at home, so the street never flashes before the room.
            if (entries.ContainsKey(HomeId) && ShouldWakeInHome())
            {
                fade.SetAlpha(1f);
            }

            GameEvents.DayStarted += OnDayStarted;
        }

        void OnDestroy()
        {
            GameEvents.DayStarted -= OnDayStarted;
            if (instance != this)
            {
                return;
            }

            instance = null;
            if (current != null)
            {
                WalkableArea.ExitInterior();
                current = null;
            }

            builder?.Dispose();
            builder = null;
        }

        // ================================================================ building

        void Build()
        {
            Vector2 centre = Vector2.zero;
            if (WalkableArea.TryGetTown(out Rect town))
            {
                centre = town.center;
            }

            GameObject lawn = GameObject.Find("Ground_Lawn");
            Renderer lawnRenderer = lawn != null ? lawn.GetComponent<Renderer>() : null;
            Material template = lawnRenderer != null ? lawnRenderer.sharedMaterial : null;
            var catalog = Resources.Load<InteriorPieceCatalog>(InteriorPieceCatalog.ResourceName);
            builder = new InteriorRoomBuilder(catalog, template);

            Add(builder.BuildHome(new Vector3(centre.x, HomeRoomHeight, centre.y), transform), Doors[0]);
            Add(builder.BuildBank(new Vector3(centre.x, BankRoomHeight, centre.y), transform), Doors[1]);
        }

        void Add(InteriorRoom room, DoorSpec spec)
        {
            var entry = new Entry { Room = room, StreetHalfWidth = spec.HalfWidth };
            LocateStreetDoor(spec, out entry.StreetDoor, out entry.StreetNormal);

            // The street door's "Go in" prompt.
            var entrance = new GameObject("Door_" + room.Id);
            entrance.transform.SetParent(transform, false);
            entrance.transform.position = entry.StreetDoor + entry.StreetNormal * StreetPromptOffset;
            entrance.AddComponent<InteriorDoor>().Configure(spec.Prompt, "Go in", spec.Icon, StreetPromptRadius,
                () => !transitioning && current == null, () => GoIn(entry));

            // The room's "Go out" prompt on the door mat (a child of the room, so it exists only while the room does).
            var exit = new GameObject("Door_" + room.Id + "_Out");
            exit.transform.SetParent(room.Root.transform, false);
            exit.transform.position = room.ExitPromptPoint;
            exit.AddComponent<InteriorDoor>().Configure("Outside", "Go out", "arrowDown", ExitPromptRadius,
                () => !transitioning && current == entry, GoOut);

            room.Root.SetActive(false);
            entries[room.Id] = entry;
            entryList.Add(entry);
        }

        /// <summary>The middle of the building's door on the face of its bounds, and the outward normal, from the
        /// building's model rotation; the measured positions if the building is missing.</summary>
        static void LocateStreetDoor(DoorSpec spec, out Vector3 door, out Vector3 normal)
        {
            GameObject building = GameObject.Find(spec.Anchor);
            Renderer[] renderers = building != null ? building.GetComponentsInChildren<Renderer>() : null;
            if (renderers == null || renderers.Length == 0)
            {
                door = spec.FallbackDoor;
                normal = spec.FallbackNormal;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            InteriorMath.RotateY(0f, spec.LocalNormalZ, building.transform.eulerAngles.y, out float nx, out float nz);
            InteriorMath.DoorOnBounds(bounds.center.x, bounds.center.z, bounds.extents.x, bounds.extents.z, nx, nz,
                out float doorX, out float doorZ);
            door = new Vector3(doorX, bounds.min.y, doorZ);
            normal = new Vector3(nx, 0f, nz);
        }

        // ================================================================ frame

        void Update()
        {
            if (!FindPlayer())
            {
                if (!placedInitially && Time.unscaledTime - startTime > PlayerWaitSeconds)
                {
                    placedInitially = true;
                    StartCoroutine(fade.FadeTo(0f, FadeSeconds));
                }

                return;
            }

            if (!placedInitially)
            {
                if (Time.frameCount - playerFoundFrame >= SettleFrames)
                {
                    PlaceInitially();
                }

                return;
            }

            if (transitioning || entryList.Count == 0)
            {
                return;
            }

            if (DrivableCar.IsDriving)
            {
                // Driving past a door never goes in; the doorway must be walked into again after getting out.
                armed = false;
                return;
            }

            if (current != null && player.position.y < current.Room.Origin.y - 1f)
            {
                // Never expected (walls and edges hold the player in), but a fall from a room must not be endless.
                MovePlayer(current.Room.EntrySpot, current.Room.EntryFacing);
            }

            if (UiModal.IsAnyOpen)
            {
                return;
            }

            WatchDoorways();
        }

        /// <summary>Walking into a doorway goes through it. A door only fires once the player has been seen outside
        /// every doorway since the last move, so arriving never bounces straight back.</summary>
        void WatchDoorways()
        {
            Vector3 p = player.position;
            if (current == null)
            {
                for (int i = 0; i < entryList.Count; i++)
                {
                    Entry entry = entryList[i];
                    if (InteriorMath.InDoorway(p.x, p.z, entry.StreetDoor.x, entry.StreetDoor.z, entry.StreetNormal.x,
                            entry.StreetNormal.z, StreetDoorwayBehind, StreetDoorwayDepth, entry.StreetHalfWidth)
                        && InteriorMath.SameSpace(p.y, entry.StreetDoor.y))
                    {
                        if (armed)
                        {
                            GoIn(entry);
                        }

                        return;
                    }
                }

                armed = true;
                return;
            }

            InteriorRoom room = current.Room;
            if (InteriorMath.InDoorway(p.x, p.z, room.DoorPoint.x, room.DoorPoint.z, room.DoorNormal.x, room.DoorNormal.z,
                    RoomDoorwayBehind, RoomDoorwayDepth, room.DoorHalfWidth))
            {
                if (armed)
                {
                    GoOut();
                }

                return;
            }

            armed = true;
        }

        bool FindPlayer()
        {
            if (player != null)
            {
                return true;
            }

            if (Time.frameCount < nextPlayerSearchFrame)
            {
                return false;
            }

            GameObject found = GameObject.FindWithTag("Player");
            if (found == null)
            {
                nextPlayerSearchFrame = Time.frameCount + PlayerSearchInterval;
                return false;
            }

            player = found.transform;
            playerController = found.GetComponent<CharacterController>();
            PlayerCharacterVisualController visual = found.GetComponentInChildren<PlayerCharacterVisualController>();
            playerVisual = visual != null ? visual.transform : null;
            playerFoundFrame = Time.frameCount;
            return true;
        }

        // ================================================================ mornings

        static bool ShouldWakeInHome()
        {
            PlayerData data = PlayerDataAccess.GetCurrentPlayer();
            return InteriorMath.WakeInHome(data != null, data?.chapter != null && data.chapter.complete,
                data != null ? data.revealPendingForDay : 0, data != null ? data.morningLineDay : 0,
                data != null ? data.currentDay : 0);
        }

        void PlaceInitially()
        {
            placedInitially = true;
            if (entries.TryGetValue(HomeId, out Entry home) && ShouldWakeInHome())
            {
                PlayerData data = PlayerDataAccess.GetCurrentPlayer();
                placedForDay = data != null ? data.currentDay : -1;
                MoveInto(home, home.Room.WakeSpot, home.Room.WakeFacing);
            }

            if (fade.Alpha > 0f)
            {
                StartCoroutine(fade.FadeTo(0f, FadeSeconds));
            }
        }

        void OnDayStarted(int day)
        {
            if (!entries.TryGetValue(HomeId, out Entry home) || player == null || !placedInitially || placedForDay == day)
            {
                return;
            }

            placedForDay = day;
            if (transitioning)
            {
                return;
            }

            if (current == home)
            {
                // Coming out of the reveal: back on your feet beside the bed.
                MovePlayer(home.Room.WakeSpot, home.Room.WakeFacing);
                armed = false;
                return;
            }

            StartCoroutine(Transition(() => MoveInto(home, home.Room.WakeSpot, home.Room.WakeFacing)));
        }

        // ================================================================ doors

        void GoIn(Entry entry)
        {
            if (transitioning || current != null || entry == null)
            {
                return;
            }

            StartCoroutine(Transition(() => MoveInto(entry, entry.Room.EntrySpot, entry.Room.EntryFacing)));
        }

        void GoOut()
        {
            if (transitioning || current == null)
            {
                return;
            }

            StartCoroutine(Transition(MoveOut));
        }

        IEnumerator Transition(Action move)
        {
            transitioning = true;
            float seconds = UiTween.ReduceMotion ? UiTheme.Motion.ReducedCrossfade : FadeSeconds;
            yield return fade.FadeTo(1f, seconds);

            try
            {
                move();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BuildingInteriors] Moving through the door failed: {ex}");
            }

            // One frame for the camera to jump to the new place behind the cover.
            yield return null;
            yield return fade.FadeTo(0f, seconds);
            transitioning = false;
        }

        void MoveInto(Entry entry, Vector3 spot, Vector3 facing)
        {
            if (current != null && current != entry)
            {
                current.Room.Root.SetActive(false);
            }

            current = entry;
            InteriorRoom room = entry.Room;
            room.Root.SetActive(true);

            Camera view = Camera.main;
            float aspect = view != null ? view.aspect : 16f / 9f;
            Vector3 angles = view != null ? view.transform.eulerAngles : new Vector3(30f, 45f, 0f);
            float size = InteriorMath.RoomViewSize(room.Width, room.Depth, InteriorRoomBuilder.WallHeight, aspect,
                angles.x, angles.y, ViewMargin, out float shiftX, out float shiftZ);
            WalkableArea.EnterInterior(room.Floor, new Vector2(room.Origin.x + shiftX, room.Origin.z + shiftZ), size);

            MovePlayer(spot, facing);
        }

        void MoveOut()
        {
            Entry leaving = current;
            current = null;
            WalkableArea.ExitInterior();
            if (leaving == null)
            {
                return;
            }

            leaving.Room.Root.SetActive(false);
            MovePlayer(StreetExitSpot(leaving), leaving.StreetNormal);
        }

        /// <summary>
        /// Where the player comes out of a building: <see cref="StreetExitDistance"/> out from its door, or, when that
        /// is taken (the delivery van used to be parked right there, so the Bank's exit put the player inside it), the
        /// first clear ground further out, then to either side, then anywhere near (<see cref="PlayerPlacement"/>).
        /// </summary>
        Vector3 StreetExitSpot(Entry leaving)
        {
            Vector3 door = leaving.StreetDoor;
            Vector3 normal = leaving.StreetNormal;
            Vector3 side = new Vector3(normal.z, 0f, -normal.x);
            Collider ignore = playerController;
            float[] outward = { StreetExitDistance, StreetExitDistance + 0.2f, StreetExitDistance + 0.4f };
            float[] across = { 0f, 0.3f, -0.3f, 0.55f, -0.55f };
            Physics.SyncTransforms();
            for (int a = 0; a < across.Length; a++)
            {
                for (int o = 0; o < outward.Length; o++)
                {
                    Vector3 candidate = door + normal * outward[o] + side * across[a];
                    candidate.y = door.y;
                    if (PlayerPlacement.IsClear(candidate, ignore))
                    {
                        return candidate;
                    }
                }
            }

            InteriorMath.InFrontOf(door.x, door.z, normal.x, normal.z, StreetExitDistance, out float x, out float z);
            PlayerPlacement.TryFindClearSpot(new Vector3(x, door.y, z), out Vector3 spot, ignore);
            return spot;
        }

        /// <summary>
        /// Sets the player down at <paramref name="spot"/> facing <paramref name="facing"/>: the CharacterController
        /// is switched off for the move (it would otherwise fight a teleport) and back on, and the camera jumps with
        /// them.
        /// </summary>
        void MovePlayer(Vector3 spot, Vector3 facing)
        {
            if (player == null)
            {
                return;
            }

            // Out of the car first, so the controller and the model are back before the player is moved.
            DrivableCar.EjectDriver();

            bool wasEnabled = playerController != null && playerController.enabled;
            if (playerController != null)
            {
                playerController.enabled = false;
            }

            player.position = spot + Vector3.up * SetDownHeight;

            if (playerController != null)
            {
                playerController.enabled = wasEnabled;
            }

            Vector3 flat = new Vector3(facing.x, 0f, facing.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(flat.normalized, Vector3.up);
                var mover = player.GetComponent<MaliGoPlayerController>();
                if (mover != null)
                {
                    mover.SetFacing(look.eulerAngles.y);
                }
                else if (playerVisual != null)
                {
                    playerVisual.rotation = look;
                }
            }

            armed = false;

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<MaliGoCameraController>();
            }

            if (cameraController != null)
            {
                cameraController.SnapToTarget();
            }
        }
    }
}
