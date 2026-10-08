// Walking the town: speeds for a stick tilt, speeding up / slowing down, turning, keeping a step inside the
// walkable area, and the leg animation's gait and playback rate so the feet stay planted (MovementMath).
using MaliGo.Core;

public static class MovementTests
{
    // ~0.27 u per metre (player ~0.475 u tall for 1.75 m).
    const float UnitsPerMetre = 0.271f;

    public static void TestSpeedsAreHumanAtWorldScale()
    {
        float walk = MovementMath.WalkSpeed / UnitsPerMetre;
        float jog = MovementMath.JogSpeed / UnitsPerMetre;
        Assert.True(walk >= 0.9f && walk <= 1.6f, "walk " + walk + " m/s");
        Assert.True(jog >= 2.5f && jog <= 3.5f, "jog " + jog + " m/s");
    }

    public static void TestInputCurveHasDeadZoneAndNeverCrawls()
    {
        Assert.Equal(0f, MovementMath.SpeedForInput(0f), "no input");
        Assert.Equal(0f, MovementMath.SpeedForInput(MovementMath.InputDeadZone), "inside the dead zone");
        float justPast = MovementMath.SpeedForInput(MovementMath.InputDeadZone + 0.001f);
        Assert.Equal(MovementMath.WalkSpeed, justPast, "just past the dead zone walks", 0.01f);
        Assert.Equal(MovementMath.JogSpeed, MovementMath.SpeedForInput(1f), "full tilt jogs", 0.0001f);

        float previous = 0f;
        for (float m = 0.15f; m <= 1f; m += 0.05f)
        {
            float speed = MovementMath.SpeedForInput(m);
            Assert.True(speed >= previous, "monotonic at " + m);
            previous = speed;
        }

        // Half tilt is still a walk, not a jog (the curve spends more of the stick on walking).
        Assert.True(MovementMath.SpeedForInput(0.5f) < (MovementMath.WalkSpeed + MovementMath.JogSpeed) * 0.5f, "half tilt walks");
    }

    public static void TestStartAndStopTakeAFractionOfASecond()
    {
        Assert.True(MovementMath.AccelerateSeconds >= 0.2f && MovementMath.AccelerateSeconds <= 0.3f, "accelerate seconds");
        Assert.True(MovementMath.DecelerateSeconds > MovementMath.AccelerateSeconds, "stopping takes a little longer");

        float speed = 0f;
        float t = 0f;
        const float dt = 1f / 60f;
        while (speed < MovementMath.JogSpeed && t < 2f)
        {
            speed = MovementMath.ApproachSpeed(speed, MovementMath.JogSpeed, dt);
            t += dt;
        }

        Assert.Equal(MovementMath.AccelerateSeconds, t, "time to jog", 0.02f);

        t = 0f;
        while (speed > 0f && t < 2f)
        {
            speed = MovementMath.ApproachSpeed(speed, 0f, dt);
            t += dt;
        }

        Assert.Equal(MovementMath.DecelerateSeconds, t, "time to stand", 0.02f);
        Assert.Equal(0.5f, MovementMath.ApproachSpeed(0.5f, 0.9f, 0f), "no time, no change");
    }

    public static void TestTurnIsCappedAndTakesTheShortWay()
    {
        Assert.True(MovementMath.TurnDegreesPerSecond >= 540f && MovementMath.TurnDegreesPerSecond <= 720f, "turn rate");
        const float dt = 0.01f;
        float step = MovementMath.TurnDegreesPerSecond * dt;
        Assert.Equal(step, MovementMath.TurnTowards(0f, 90f, dt), "capped turn", 0.001f);
        Assert.Equal(360f - step, MovementMath.TurnTowards(0f, 270f, dt), "short way round (left)", 0.001f);
        Assert.Equal(5f, MovementMath.TurnTowards(350f, 5f, 1f), "reaches the target across 0", 0.001f);
        Assert.Equal(-20f, MovementMath.DeltaAngle(10f, 350f), "delta across 0", 0.001f);
        Assert.Equal(90f, MovementMath.YawOf(1f, 0f), "+X is 90", 0.001f);
        Assert.Equal(0f, MovementMath.YawOf(0f, 1f), "+Z is 0", 0.001f);
        Assert.Equal(180f, MovementMath.YawOf(0f, -1f), "-Z is 180", 0.001f);
    }

    public static void TestTurnsOnTheSpotBeforeMoving()
    {
        Assert.Equal(1f, MovementMath.TurnSpeedFactor(0f), "facing the stick");
        Assert.Equal(1f, MovementMath.TurnSpeedFactor(-MovementMath.TurnFullSpeedDegrees), "small error");
        Assert.Equal(0f, MovementMath.TurnSpeedFactor(180f), "about face: turn first");
        Assert.Equal(0f, MovementMath.TurnSpeedFactor(-MovementMath.TurnStandStillDegrees), "side-on: turn first");
        float mid = MovementMath.TurnSpeedFactor(70f);
        Assert.True(mid > 0f && mid < 1f, "in between " + mid);
    }

    public static void TestRunClipStrideMatchesMeasurement()
    {
        // 5.181 model units per 0.667 s cycle at 0.12 scale (measured from run.fbx) = 0.93 u/s at 1x.
        Assert.Equal(0.9326f, MovementMath.RunClipGroundSpeed, "run clip ground speed", 0.002f);
        Assert.Equal(MovementMath.RunClipGroundSpeed, MovementMath.GroundSpeedAtGait(1f, MovementMath.DefaultModelScale), "gait 1 is the run", 0.0001f);
        Assert.Equal(16f / 24f, MovementMath.GaitCycleSeconds(1f), "run cycle");
        Assert.Equal(32f / 30f, MovementMath.GaitCycleSeconds(0f), "idle cycle");
        Assert.Equal(0f, MovementMath.StrideAtGait(0f), "idle feet do not travel");
    }

    public static void TestFeetPlantAcrossTheSpeedRange()
    {
        // At every speed the player can hold, the legs play at a rate whose foot speed equals the ground speed
        // (not clamped), and the cadence is a human one: walk ~100-130 steps/min, jog ~150-180.
        for (float speed = MovementMath.WalkSpeed; speed <= MovementMath.JogSpeed + 0.0001f; speed += 0.05f)
        {
            float gait = MovementMath.GaitForSpeed(speed);
            float playback = MovementMath.PlaybackRate(speed, gait, MovementMath.DefaultModelScale);
            float footSpeed = MovementMath.GroundSpeedAtGait(gait, MovementMath.DefaultModelScale) * playback;
            Assert.Equal(speed, footSpeed, "feet match the ground at " + speed, 0.001f);
            Assert.True(playback > MovementMath.MinPlayback && playback < MovementMath.MaxPlayback, "unclamped playback " + playback);
        }

        float walkCadence = MovementMath.StepsPerMinute(MovementMath.WalkGait,
            MovementMath.PlaybackRate(MovementMath.WalkSpeed, MovementMath.WalkGait, MovementMath.DefaultModelScale));
        float jogCadence = MovementMath.StepsPerMinute(1f, MovementMath.PlaybackRate(MovementMath.JogSpeed, 1f, MovementMath.DefaultModelScale));
        Assert.True(walkCadence >= 100f && walkCadence <= 130f, "walk cadence " + walkCadence);
        Assert.True(jogCadence >= 150f && jogCadence <= 180f, "jog cadence " + jogCadence);
    }

    public static void TestGaitLengthensWithSpeed()
    {
        Assert.Equal(MovementMath.WalkGait, MovementMath.GaitForSpeed(0.1f), "slow is the shortest gait");
        Assert.Equal(MovementMath.WalkGait, MovementMath.GaitForSpeed(MovementMath.WalkSpeed), "walk gait");
        Assert.Equal(1f, MovementMath.GaitForSpeed(MovementMath.JogSpeed), "jog is the full run", 0.0001f);
        Assert.Equal(1f, MovementMath.GaitForSpeed(5f), "never past the run");
        Assert.True(MovementMath.StrideAtGait(0.75f) > MovementMath.StrideAtGait(0.6f), "stride grows");
    }

    public static void TestPlaybackClampsAndFallsBack()
    {
        Assert.Equal(1f, MovementMath.PlaybackRate(0f, 1f, 0.12f), "standing");
        Assert.Equal(MovementMath.MinPlayback, MovementMath.PlaybackRate(0.01f, 1f, 0.12f), "creeping clamps low");
        Assert.Equal(MovementMath.MaxPlayback, MovementMath.PlaybackRate(10f, 1f, 0.12f), "fast clamps high");
        Assert.Equal(MovementMath.PlaybackRate(0.5f, 1f, MovementMath.DefaultModelScale),
            MovementMath.PlaybackRate(0.5f, 1f, 0f), "unknown scale uses the default");
        Assert.Equal(MovementMath.PlaybackRate(0.5f, 1f, 0.12f), MovementMath.RunPlaybackRate(0.5f), "run-only helper");
    }

    public static void TestMovingHasHysteresis()
    {
        Assert.True(!MovementMath.IsMoving(0.04f, false), "too slow to start");
        Assert.True(MovementMath.IsMoving(0.07f, false), "starts");
        Assert.True(MovementMath.IsMoving(0.04f, true), "keeps going while slowing");
        Assert.True(!MovementMath.IsMoving(0.02f, true), "stops");
    }

    public static void TestLeanIsSmallAndCapped()
    {
        float jogLean = MovementMath.LeanDegrees(MovementMath.JogSpeed, 0f);
        Assert.True(jogLean > 0f && jogLean < 4f, "steady jog lean " + jogLean);
        Assert.True(MovementMath.LeanDegrees(0.3f, -MovementMath.Deceleration) < 0f, "leans back when stopping");
        Assert.Equal(MovementMath.MaxLeanDegrees, MovementMath.LeanDegrees(1f, 100f), "capped forward");
        Assert.Equal(-MovementMath.MaxLeanDegrees, MovementMath.LeanDegrees(0f, -100f), "capped back");
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
