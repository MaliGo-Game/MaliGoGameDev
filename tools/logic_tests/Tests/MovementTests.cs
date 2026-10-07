// Walking the town: the walk speed, keeping a step inside the walkable area, and the run clip's playback rate
// (MovementMath).
using MaliGo.Core;

public static class MovementTests
{
    public static void TestWalkSpeedIsABriskJogAtWorldScale()
    {
        // ~0.27 u per metre, player ~0.47 u tall: 0.9-1.1 u/s is ~3.3-4 m/s, about two body heights a second.
        Assert.True(MovementMath.WalkSpeed >= 0.9f && MovementMath.WalkSpeed <= 1.1f, "walk speed " + MovementMath.WalkSpeed);
        Assert.True(MovementMath.AccelerateSeconds > 0f && MovementMath.DecelerateSeconds > 0f, "ramp times must be positive");
    }

    public static void TestStepInsideIsUnchanged()
    {
        Assert.Equal(0.02f, MovementMath.ClampStep(0f, 0.02f, -5f, 5f), "inside +");
        Assert.Equal(-0.02f, MovementMath.ClampStep(0f, -0.02f, -5f, 5f), "inside -");
        Assert.Equal(0f, MovementMath.ClampStep(0f, 0f, -5f, 5f), "no step");
    }

    public static void TestStepAcrossTheEdgeStopsOnIt()
    {
        Assert.Equal(0.01f, MovementMath.ClampStep(4.99f, 0.05f, -5f, 5f), "max edge", 0.0001f);
        Assert.Equal(-0.01f, MovementMath.ClampStep(-4.99f, -0.05f, -5f, 5f), "min edge", 0.0001f);
        Assert.Equal(0f, MovementMath.ClampStep(5f, 0.05f, -5f, 5f), "on the max edge");
        Assert.Equal(0f, MovementMath.ClampStep(-5f, -0.05f, -5f, 5f), "on the min edge");
    }

    public static void TestOutsideIsNeverPulledInButMayWalkBack()
    {
        Assert.Equal(0f, MovementMath.ClampStep(6f, 0.05f, -5f, 5f), "outside, walking further out");
        Assert.Equal(-0.05f, MovementMath.ClampStep(6f, -0.05f, -5f, 5f), "outside, walking back in");
        Assert.Equal(0f, MovementMath.ClampStep(-6f, -0.05f, -5f, 5f), "outside min, walking further out");
        Assert.Equal(0.05f, MovementMath.ClampStep(-6f, 0.05f, -5f, 5f), "outside min, walking back in");
    }

    public static void TestRunPlaybackFollowsGroundSpeed()
    {
        float atWalk = MovementMath.RunPlaybackRate(MovementMath.WalkSpeed);
        Assert.True(atWalk > 0.6f && atWalk <= 1.0f, "playback at walk speed " + atWalk);
        Assert.Equal(MovementMath.MinRunPlayback, MovementMath.RunPlaybackRate(0.05f), "slow tilt clamps low");
        Assert.Equal(MovementMath.MaxRunPlayback, MovementMath.RunPlaybackRate(10f), "fast clamps high");
        Assert.Equal(1f, MovementMath.RunPlaybackRate(0f), "standing");
    }

    public static void TestShrinkKeepsAMarginOrCollapses()
    {
        float min = -5.5f, max = 5.5f;
        MovementMath.Shrink(ref min, ref max, 0.15f);
        Assert.Equal(-5.35f, min, "shrunk min");
        Assert.Equal(5.35f, max, "shrunk max");

        min = 1f;
        max = 1.2f;
        MovementMath.Shrink(ref min, ref max, 0.15f);
        Assert.Equal(1.1f, min, "collapsed min");
        Assert.Equal(1.1f, max, "collapsed max");
    }
}
