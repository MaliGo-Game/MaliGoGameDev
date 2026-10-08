// The unstuck guard (StuckDetector: asking to move for 0.4 s but covering under 10 % of it) and the geometry behind
// it and safe placement (UnstuckMath: pushing the player's circle out of a box footprint, sliding along a surface).
using MaliGo.Core;

public static class UnstuckTests
{
    const float Dt = 1f / 60f;

    public static void TestWalkingFreelyIsNeverStuck()
    {
        var detector = new StuckDetector();
        for (int i = 0; i < 300; i++)
        {
            float asked = MovementMath.JogSpeed * Dt;
            Assert.True(!detector.Update(asked, asked * 0.95f, Dt), "free walking at frame " + i);
        }
    }

    public static void TestPushingIntoSomethingIsStuckAfterTheWindow()
    {
        var detector = new StuckDetector();
        float asked = MovementMath.JogSpeed * Dt;
        int frames = 0;
        bool stuck = false;
        while (!stuck && frames < 120)
        {
            stuck = detector.Update(asked, 0.001f * Dt, Dt);
            frames++;
        }

        Assert.True(stuck, "held against something");
        Assert.Equal(StuckDetector.WindowSeconds, frames * Dt, "after the window", Dt * 1.5f);

        // Once per window, not every frame after.
        Assert.True(!detector.Update(asked, 0f, Dt), "a fresh window starts");
    }

    public static void TestSlowProgressAlongAWallIsNotStuck()
    {
        var detector = new StuckDetector();
        float asked = MovementMath.JogSpeed * Dt;
        for (int i = 0; i < 120; i++)
        {
            // Sliding along a wall at a third of the speed.
            Assert.True(!detector.Update(asked, asked * 0.33f, Dt), "sliding is progress");
        }
    }

    public static void TestLettingGoOrTurningOnTheSpotResets()
    {
        var detector = new StuckDetector();
        float asked = MovementMath.JogSpeed * Dt;
        for (int i = 0; i < 20; i++)
        {
            detector.Update(asked, 0f, Dt);
        }

        // Let go for a frame: the window starts again.
        Assert.True(!detector.Update(0f, 0f, Dt), "no ask, no verdict");
        for (int i = 0; i < 20; i++)
        {
            Assert.True(!detector.Update(asked, 0f, Dt), "window restarted at " + i);
        }

        // Turning on the spot asks for (almost) no distance: never judged.
        var turning = new StuckDetector();
        for (int i = 0; i < 120; i++)
        {
            Assert.True(!turning.Update(0.0001f, 0f, Dt), "turning on the spot");
        }
    }

    public static void TestPushOutOfBoxFromInsideTakesTheNearestFace()
    {
        // A car-sized box (half 0.24 x 0.4); a circle of 0.09 near its +X side, inside.
        Assert.True(UnstuckMath.PushOutOfBox(0.2f, 0.1f, 0.24f, 0.4f, 0.09f, out float px, out float pz), "inside overlaps");
        Assert.Equal(0.24f - 0.2f + 0.09f, px, "out through +X", 0.0001f);
        Assert.Equal(0f, pz, "not along Z");

        // Near the -Z end.
        Assert.True(UnstuckMath.PushOutOfBox(0f, -0.35f, 0.24f, 0.4f, 0.09f, out px, out pz), "inside overlaps");
        Assert.Equal(0f, px, "not along X");
        Assert.Equal(-(0.4f - 0.35f + 0.09f), pz, "out through -Z", 0.0001f);

        // The van's old spot: the Bank's exit at (-3.02, 4.4) was inside the van (centre (-3.3, 4.4), turned 90
        // degrees so its 1.0 u length runs along X).
        Assert.True(UnstuckMath.PushOutOfBox(-3.02f + 3.3f, 0f, 0.505f, 0.233f, 0.09f, out px, out pz),
            "the old Bank exit was inside the van");
    }

    public static void TestPushOutOfBoxFromOutsideLeavesItTouching()
    {
        // Grazing the +X side from outside.
        Assert.True(UnstuckMath.PushOutOfBox(0.3f, 0f, 0.24f, 0.4f, 0.09f, out float px, out float pz), "overlaps the side");
        Assert.Equal(0.24f + 0.09f - 0.3f, px, "pushed to just touching", 0.0001f);
        Assert.Equal(0f, pz, "straight out");

        // Clear of it.
        Assert.True(!UnstuckMath.PushOutOfBox(0.4f, 0f, 0.24f, 0.4f, 0.09f, out px, out pz), "clear");
        Assert.Equal(0f, px, "no push");

        // Near a corner, diagonally.
        Assert.True(UnstuckMath.PushOutOfBox(0.28f, 0.44f, 0.24f, 0.4f, 0.09f, out px, out pz), "corner overlap");
        float dx = 0.28f + px - 0.24f;
        float dz = 0.44f + pz - 0.4f;
        Assert.Equal(0.09f, (float)System.Math.Sqrt(dx * dx + dz * dz), "ends exactly a radius from the corner", 0.0001f);
    }

    public static void TestSlideAlongASurface()
    {
        // Walking north-east into a wall facing west (normal -X): slide north.
        float length = UnstuckMath.Slide(0.707f, 0.707f, -1f, 0f, out float sx, out float sz);
        Assert.Equal(0f, sx, "into-the-wall part removed", 0.0001f);
        Assert.Equal(0.707f, sz, "along the wall kept", 0.0001f);
        Assert.Equal(0.707f, length, "length");

        // Straight into it: nothing left.
        length = UnstuckMath.Slide(1f, 0f, -1f, 0f, out sx, out sz);
        Assert.Equal(0f, length, "head on");

        // Moving away from it: unchanged.
        length = UnstuckMath.Slide(-1f, 0f, -1f, 0f, out sx, out sz);
        Assert.Equal(-1f, sx, "away from the wall is untouched");
        Assert.Equal(1f, length, "full length");
    }
}
