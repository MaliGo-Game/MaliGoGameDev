// Driving the player's car: speeds at world scale, 0 to top speed in about 3 s, braking, reverse, coasting, the
// joystick mapping (push the way you want to go), speed-dependent steering and where the driver gets out (VehicleMath).
using System;
using MaliGo.Core;

public static class VehicleTests
{
    // ~0.27 u per metre (player ~0.475 u tall for 1.75 m).
    const float UnitsPerMetre = 0.271f;
    const float Dt = 1f / 60f;

    public static void TestTopSpeedIsASuburbanStreetSpeed()
    {
        float kmh = VehicleMath.MaxForwardSpeed / UnitsPerMetre * 3.6f;
        Assert.True(kmh >= 40f && kmh <= 55f, "top speed " + kmh + " km/h");
        Assert.True(VehicleMath.MaxReverseSpeed < VehicleMath.MaxForwardSpeed * 0.5f, "reverse is slow");
    }

    public static void TestFullThrottleReachesTopSpeedInAboutThreeSeconds()
    {
        float speed = 0f;
        float t = 0f;
        while (speed < VehicleMath.MaxForwardSpeed * 0.95f && t < 10f)
        {
            speed = VehicleMath.UpdateSpeed(speed, 1f, Dt);
            t += Dt;
        }

        Assert.True(t >= 2.4f && t <= 3.6f, "0 to 95% of top speed in " + t + " s");

        for (int i = 0; i < 600; i++)
        {
            speed = VehicleMath.UpdateSpeed(speed, 1f, Dt);
        }

        Assert.Equal(VehicleMath.MaxForwardSpeed, speed, "held at top speed", 0.001f);
    }

    public static void TestBrakingStopsWithoutFlippingIntoReverse()
    {
        float speed = VehicleMath.MaxForwardSpeed;
        float t = 0f;
        while (speed > 0f && t < 5f)
        {
            speed = VehicleMath.UpdateSpeed(speed, -1f, Dt);
            Assert.True(speed >= 0f, "never below zero while braking from forward");
            t += Dt;
        }

        Assert.True(t <= 1.2f, "full brake from top speed stops in " + t + " s");

        // Reversing from a stop, then braking with forward throttle stops at zero.
        speed = 0f;
        for (int i = 0; i < 300; i++)
        {
            speed = VehicleMath.UpdateSpeed(speed, -1f, Dt);
        }

        Assert.Equal(-VehicleMath.MaxReverseSpeed, speed, "reverse top speed", 0.001f);
        speed = VehicleMath.UpdateSpeed(speed, 1f, 1f);
        Assert.Equal(0f, speed, "forward pedal brakes a reversing car to a stop first");
    }

    public static void TestCoastingSlowsToAStop()
    {
        float speed = VehicleMath.MaxForwardSpeed;
        float t = 0f;
        float previous = speed;
        while (speed > 0f && t < 20f)
        {
            speed = VehicleMath.UpdateSpeed(speed, 0f, Dt);
            Assert.True(speed <= previous, "coasting never speeds up");
            previous = speed;
            t += Dt;
        }

        Assert.True(t >= 2f && t <= 8f, "coasts from top speed to a stop in " + t + " s");

        speed = -VehicleMath.MaxReverseSpeed;
        for (int i = 0; i < 600; i++)
        {
            speed = VehicleMath.UpdateSpeed(speed, 0f, Dt);
        }

        Assert.Equal(0f, speed, "coasting in reverse stops too");
    }

    public static void TestStickAheadDrivesAndSteersTowardIt()
    {
        bool reversing = false;

        // Car faces +Z (yaw 0); stick straight ahead.
        VehicleMath.PedalsFromStick(0f, 1f, 0f, 0f, ref reversing, out float throttle, out float steer);
        Assert.Equal(1f, throttle, "full stick ahead is full throttle");
        Assert.Equal(0f, steer, "no steering straight ahead");
        Assert.True(!reversing, "forward gear");

        // Stick to the right (+X): steer right (positive), still drive.
        VehicleMath.PedalsFromStick(1f, 0f, 0f, 0f, ref reversing, out throttle, out steer);
        Assert.Equal(1f, steer, "stick to the right is full right lock");
        Assert.True(throttle > 0.3f && throttle < 1f, "drives on gently while turning hard: " + throttle);

        // Stick a little left: proportional left steering.
        VehicleMath.PedalsFromStick(-0.26f, 0.97f, 0f, 0f, ref reversing, out throttle, out steer);
        Assert.True(steer < 0f && steer > -1f, "a little left is a little left lock: " + steer);

        // Car facing +X (yaw 90): stick +X is straight ahead.
        VehicleMath.PedalsFromStick(1f, 0f, 90f, 1f, ref reversing, out throttle, out steer);
        Assert.Equal(0f, steer, "relative to the car's heading", 0.001f);

        // Dead zone.
        VehicleMath.PedalsFromStick(0.05f, 0.05f, 0f, 2f, ref reversing, out throttle, out steer);
        Assert.Equal(0f, throttle, "thumb drift releases the pedals");
        Assert.Equal(0f, steer, "and the steering");
    }

    public static void TestStickBehindBrakesThenReverses()
    {
        bool reversing = false;

        // Rolling forward, stick behind: brake in a straight line, stay in forward gear.
        VehicleMath.PedalsFromStick(0f, -1f, 0f, 2f, ref reversing, out float throttle, out float steer);
        Assert.True(!reversing, "still forward gear while rolling");
        Assert.Equal(-1f, throttle, "full brake");
        Assert.Equal(0f, steer, "no steering under braking");
        Assert.True(VehicleMath.UpdateSpeed(2f, throttle, Dt) < 2f, "it slows");

        // Stopped, stick behind: reverse.
        VehicleMath.PedalsFromStick(0f, -1f, 0f, 0f, ref reversing, out throttle, out steer);
        Assert.True(reversing, "reverse gear once stopped");
        Assert.True(throttle < 0f, "backs up");
        Assert.Equal(0f, steer, "straight back");

        // Reversing, stick behind and to the right (+X, -Z): like a real car, the wheel turns right so the rear swings
        // right (toward +X); the nose swings left, so the body yaws anticlockwise.
        VehicleMath.PedalsFromStick(0.5f, -0.87f, 0f, -0.5f, ref reversing, out throttle, out steer);
        Assert.True(reversing, "still reversing");
        Assert.True(steer > 0f, "right lock backs the rear toward the stick: " + steer);
        Assert.True(VehicleMath.YawRate(-0.5f, steer) < 0f, "the body turns anticlockwise, the rear toward +X");

        // Hysteresis: stick swings to the side (90 degrees) - still reversing; ahead - forward again.
        VehicleMath.PedalsFromStick(1f, 0f, 0f, -0.2f, ref reversing, out throttle, out steer);
        Assert.True(reversing, "90 degrees off does not flip the gear");
        VehicleMath.PedalsFromStick(0f, 1f, 0f, -0.1f, ref reversing, out throttle, out steer);
        Assert.True(!reversing, "stick ahead drives forward again");
        Assert.True(throttle > 0f, "forward pedal (brakes the backwards roll first)");
    }

    public static void TestSteeringWidensWithSpeed()
    {
        Assert.Equal(0f, VehicleMath.YawRate(0f, 1f), "a parked car does not turn");
        Assert.Equal(VehicleMath.MinTurnRadius, VehicleMath.TurnRadius(0.5f), "tight at a crawl");
        Assert.True(VehicleMath.TurnRadius(VehicleMath.MaxForwardSpeed) > 3f, "wide at top speed");

        float previous = 0f;
        for (float v = 0.25f; v <= VehicleMath.MaxForwardSpeed; v += 0.25f)
        {
            float radius = VehicleMath.TurnRadius(v);
            Assert.True(radius >= previous, "the turn never tightens with speed at " + v);
            previous = radius;

            // Sideways acceleration v^2 / r stays within grip (bar the crawl, where the radius floor rules).
            Assert.True(v * v / radius <= VehicleMath.MaxLateralAcceleration + 0.001f, "grip at " + v);
        }

        float rate = VehicleMath.YawRate(VehicleMath.MaxForwardSpeed, 1f);
        Assert.True(rate > 20f && rate < 90f, "full lock at top speed turns " + rate + " deg/s");
        Assert.True(VehicleMath.YawRate(1f, -1f) < 0f, "left lock turns anticlockwise");

        // A 90 degree bend at 1 u/s on full lock takes about a second and a half.
        float yaw = 0f;
        float t = 0f;
        while (yaw < 90f && t < 5f)
        {
            yaw += VehicleMath.YawRate(1f, 1f) * Dt;
            t += Dt;
        }

        Assert.True(t > 0.5f && t < 2f, "quarter turn at 1 u/s in " + t + " s");
    }

    public static void TestSteeringWheelEasesAndReturns()
    {
        float steer = 0f;
        steer = VehicleMath.UpdateSteer(steer, 1f, 0.1f);
        Assert.Equal(VehicleMath.SteerRate * 0.1f, steer, "turns in at the steering rate", 0.0001f);
        steer = VehicleMath.UpdateSteer(steer, 1f, 10f);
        Assert.Equal(1f, steer, "reaches full lock and stops there");
        steer = VehicleMath.UpdateSteer(steer, 0f, 0.1f);
        Assert.Equal(1f - VehicleMath.SteerReturnRate * 0.1f, steer, "returns faster than it turns in", 0.0001f);
    }

    public static void TestWheelsRollWithDistance()
    {
        float radius = 0.093f;
        float circumference = 2f * (float)Math.PI * radius;
        Assert.Equal(360f, VehicleMath.WheelRollDegrees(circumference, radius), "one turn per circumference", 0.01f);
        Assert.True(VehicleMath.WheelRollDegrees(-0.1f, radius) < 0f, "reverse rolls backwards");
        Assert.Equal(0f, VehicleMath.WheelRollDegrees(1f, 0f), "no radius, no roll");
    }

    public static void TestDriverGetsOutOnTheRightSideClearOfTheCar()
    {
        const float halfWidth = 0.24f;
        const float halfLength = 0.4f;
        const float radius = 0.09f;
        const float gap = 0.04f;

        // Car at the origin facing +Z: the driver's door (right-hand drive) is on +X.
        VehicleMath.ExitSpot(0f, 0f, 0f, halfWidth, halfLength, radius, gap, 0, out float x, out float z);
        Assert.Equal(halfWidth + radius + gap, x, "beside the right side");
        Assert.True(z > 0f && z < halfLength, "by the front seat");

        VehicleMath.ExitSpot(0f, 0f, 0f, halfWidth, halfLength, radius, gap, 1, out x, out z);
        Assert.Equal(-(halfWidth + radius + gap), x, "passenger side on the left");

        VehicleMath.ExitSpot(0f, 0f, 0f, halfWidth, halfLength, radius, gap, 2, out x, out z);
        Assert.Equal(-(halfLength + radius + gap), z, "behind");

        // Facing +X (yaw 90): the right side is -Z.
        VehicleMath.ExitSpot(2f, -0.6f, 90f, halfWidth, halfLength, radius, gap, 0, out x, out z);
        Assert.Equal(-0.6f - (halfWidth + radius + gap), z, "right side turns with the car");

        // Every side leaves the person's whole capsule outside the car's footprint.
        for (int side = 0; side < 4; side++)
        {
            VehicleMath.ExitSpot(0f, 0f, 0f, halfWidth, halfLength, radius, gap, side, out x, out z);
            Assert.True(!UnstuckMath.PushOutOfBox(x, z, halfWidth, halfLength, radius, out _, out _),
                "side " + side + " is clear of the car");
        }
    }
}
