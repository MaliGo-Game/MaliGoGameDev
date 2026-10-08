// Walk-in rooms: door placement from a building's rotation and bounds, the doorway trigger, the active walkable area
// and camera bounds inside and outside, room framing, space separation and where the day starts (InteriorMath).
using MaliGo.Core;

public static class InteriorTests
{
    public static void TestRotateYFollowsUnity()
    {
        InteriorMath.RotateY(0f, 1f, 0f, out float x, out float z);
        Assert.Equal(0f, x, "yaw 0 x");
        Assert.Equal(1f, z, "yaw 0 z");

        // Quaternion.Euler(0, 90, 0) * forward = right.
        InteriorMath.RotateY(0f, 1f, 90f, out x, out z);
        Assert.Equal(1f, x, "yaw 90 turns +Z to +X", 0.0001f);
        Assert.Equal(0f, z, "yaw 90 z", 0.0001f);

        InteriorMath.RotateY(0f, -1f, 90f, out x, out z);
        Assert.Equal(-1f, x, "yaw 90 turns -Z to -X", 0.0001f);
        Assert.Equal(0f, z, "yaw 90 -Z z", 0.0001f);

        InteriorMath.RotateY(1f, 0f, 180f, out x, out z);
        Assert.Equal(-1f, x, "yaw 180 x", 0.0001f);
        Assert.Equal(0f, z, "yaw 180 z", 0.0001f);
    }

    public static void TestHomeDoorIsOnTheStreetSide()
    {
        // Player_House: building-type-a at (2, -2.2), not turned, 1.3 x 1.03; its front door is on the model's +Z.
        InteriorMath.RotateY(0f, 1f, 0f, out float nx, out float nz);
        InteriorMath.DoorOnBounds(2f, -2.2f, 0.65f, 0.514f, nx, nz, out float doorX, out float doorZ);
        Assert.Equal(2f, doorX, "home door x", 0.0001f);
        Assert.Equal(-1.686f, doorZ, "home door z (front face, toward the road at z 0)", 0.0001f);
    }

    public static void TestBankDoorFacesWestAfterItsTurn()
    {
        // Local_Bank_Building: commercial building-a at (-2, 4.4) turned 90 degrees; its doors are on the model's -Z,
        // so they face -X. Turned, its world footprint is 0.94 (X) x 0.884 (Z).
        InteriorMath.RotateY(0f, -1f, 90f, out float nx, out float nz);
        InteriorMath.DoorOnBounds(-2f, 4.4f, 0.47f, 0.442f, nx, nz, out float doorX, out float doorZ);
        Assert.Equal(-2.47f, doorX, "bank door x (west face)", 0.0001f);
        Assert.Equal(4.4f, doorZ, "bank door z", 0.0001f);
    }

    public static void TestDoorwayBoxIsInFrontOfTheDoorOnly()
    {
        // Door at the origin facing +Z; box from 0.1 behind to 0.2 in front, 0.15 to each side.
        Assert.True(InteriorMath.InDoorway(0f, 0.1f, 0f, 0f, 0f, 1f, 0.1f, 0.2f, 0.15f), "just in front");
        Assert.True(InteriorMath.InDoorway(0.14f, 0.19f, 0f, 0f, 0f, 1f, 0.1f, 0.2f, 0.15f), "front corner");
        Assert.True(InteriorMath.InDoorway(0f, -0.05f, 0f, 0f, 0f, 1f, 0.1f, 0.2f, 0.15f), "a little inside the bounds");
        Assert.True(!InteriorMath.InDoorway(0f, 0.3f, 0f, 0f, 0f, 1f, 0.1f, 0.2f, 0.15f), "too far out");
        Assert.True(!InteriorMath.InDoorway(0.2f, 0.1f, 0f, 0f, 0f, 1f, 0.1f, 0.2f, 0.15f), "beside the door");
        Assert.True(!InteriorMath.InDoorway(0f, -0.2f, 0f, 0f, 0f, 1f, 0.1f, 0.2f, 0.15f), "deep inside the building");

        // The same door facing -X (the Bank): in front means smaller X.
        Assert.True(InteriorMath.InDoorway(-0.1f, 0.25f, 0f, 0f, -1f, 0f, 0.1f, 0.2f, 0.3f), "west door, in front");
        Assert.True(!InteriorMath.InDoorway(0.15f, 0f, 0f, 0f, -1f, 0f, 0.1f, 0.2f, 0.3f), "west door, behind");
    }

    public static void TestComingOutLandsOutsideTheDoorway()
    {
        // The exit spot must not be in the doorway box (or the player would go straight back in).
        InteriorMath.InFrontOf(2f, -1.686f, 0f, 1f, 0.55f, out float x, out float z);
        Assert.Equal(2f, x, "exit x", 0.0001f);
        Assert.Equal(-1.136f, z, "exit z", 0.0001f);
        Assert.True(!InteriorMath.InDoorway(x, z, 2f, -1.686f, 0f, 1f, 0.12f, 0.22f, 0.15f), "exit spot is clear of the doorway");
    }

    public static void TestActiveAreaSwitchesBetweenTownAndRoom()
    {
        InteriorMath.ActiveArea(false, -5f, 6f, -4f, 5f, 0f, 2f, 0f, 1.7f, 0.1f,
            out float minX, out float maxX, out float minZ, out float maxZ);
        Assert.Equal(-5f, minX, "town min x");
        Assert.Equal(6f, maxX, "town max x");
        Assert.Equal(-4f, minZ, "town min z");
        Assert.Equal(5f, maxZ, "town max z");

        InteriorMath.ActiveArea(true, -5f, 6f, -4f, 5f, 0f, 2f, 0f, 1.7f, 0.1f,
            out minX, out maxX, out minZ, out maxZ);
        Assert.Equal(0.1f, minX, "room min x (shrunk)", 0.0001f);
        Assert.Equal(1.9f, maxX, "room max x (shrunk)", 0.0001f);
        Assert.Equal(0.1f, minZ, "room min z (shrunk)", 0.0001f);
        Assert.Equal(1.6f, maxZ, "room max z (shrunk)", 0.0001f);
    }

    public static void TestCameraHoldsStillInsideAndFollowsOutside()
    {
        InteriorMath.CameraBounds(true, -5f, 6f, -4f, 5f, 1.3f, 0.4f,
            out float minX, out float maxX, out float minZ, out float maxZ);
        Assert.Equal(1.3f, minX, "inside: one point x");
        Assert.Equal(1.3f, maxX, "inside: one point x max");
        Assert.Equal(0.4f, minZ, "inside: one point z");
        Assert.Equal(0.4f, maxZ, "inside: one point z max");

        InteriorMath.CameraBounds(false, -5f, 6f, -4f, 5f, 1.3f, 0.4f, out minX, out maxX, out minZ, out maxZ);
        Assert.Equal(-5f, minX, "outside: town");
        Assert.Equal(5f, maxZ, "outside: town max z");
    }

    public static void TestRoomViewFitsTheRoomAndCentresIt()
    {
        // The home room (2.0 x 1.7, walls 0.62) at the game's 30/45 camera on a wide phone.
        float size = InteriorMath.RoomViewSize(2.0f, 1.7f, 0.62f, 2.1f, 30f, 45f, 1.12f, out float shiftX, out float shiftZ);
        Assert.True(size > 0.9f && size < 1.3f, "room view size " + size);
        Assert.True(size < 1.7f, "tighter than the street view (1.7)");

        // The walls make the room taller above its floor centre: look a little further in (toward +X +Z).
        Assert.True(shiftX > 0f && shiftZ > 0f, "look point shifts into the room " + shiftX + ", " + shiftZ);
        Assert.Equal(shiftX, shiftZ, "a 45 degree view shifts equally", 0.0001f);

        // A narrow screen needs a bigger size to fit the room's width.
        float narrow = InteriorMath.RoomViewSize(2.0f, 1.7f, 0.62f, 0.8f, 30f, 45f, 1.12f, out _, out _);
        Assert.True(narrow > size, "narrow screen " + narrow + " > wide " + size);
    }

    public static void TestSpacesAreSeparatedByHeight()
    {
        Assert.True(InteriorMath.SameSpace(0.05f, 0f), "street to street");
        Assert.True(InteriorMath.SameSpace(60.02f, 60f), "in the room");
        Assert.True(!InteriorMath.SameSpace(0.05f, 60f), "street to a room above it");
        Assert.True(!InteriorMath.SameSpace(60.02f, 0.05f), "room to the street below");
        Assert.True(!InteriorMath.SameSpace(60.02f, 80f), "one room to another");
    }

    public static void TestDayStartsAtHomeWhenAMorningIsDue()
    {
        Assert.True(InteriorMath.WakeInHome(true, false, 0, 0, 1), "first day after onboarding");
        Assert.True(InteriorMath.WakeInHome(true, false, 2, 2, 3), "reveal still to show");
        Assert.True(InteriorMath.WakeInHome(true, false, 0, 2, 3), "next morning not started yet");
        Assert.True(!InteriorMath.WakeInHome(true, false, 0, 3, 3), "mid-day load: outside the front door");
        Assert.True(!InteriorMath.WakeInHome(true, true, 0, 0, 5), "chapter end");
        Assert.True(!InteriorMath.WakeInHome(false, false, 0, 0, 1), "no save");
    }

    public static void TestEaseMovesTowardTheGoal()
    {
        float v = InteriorMath.Ease(1.7f, 1.0f, 12f, 0.25f);
        Assert.True(v < 1.1f && v > 1.0f, "most of the way in a quarter second: " + v);
        Assert.Equal(1.7f, InteriorMath.Ease(1.7f, 1.0f, 12f, 0f), "no time, no change");
        Assert.Equal(1.0f, InteriorMath.Ease(1.0f, 1.0f, 12f, 0.1f), "at the goal");
    }
}
