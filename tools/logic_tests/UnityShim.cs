// The minimal slice of UnityEngine that MaliGo's pure logic files use, so they compile and run
// outside Unity (design spec 2.5). Owned by WP1 and frozen after stage 1 (only WP9 may add to it).
// Pure code that needs anything else uses System.Math / System.MathF, never a new Unity member.
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Infinity = float.PositiveInfinity;
        public static readonly float Epsilon = float.Epsilon;

        public static float Min(float a, float b) => a < b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;

        public static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
        public static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
        public static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;

        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int value) => Math.Abs(value);

        /// <summary>Like Unity: 1 for zero and positive values, -1 for negative ones.</summary>
        public static float Sign(float f) => f >= 0f ? 1f : -1f;

        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Ceil(float f) => (float)Math.Ceiling(f);

        /// <summary>Like Unity (System.Math.Round): halves go to the even number.</summary>
        public static float Round(float f) => (float)Math.Round(f);

        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static bool Approximately(float a, float b)
        {
            return Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        }
    }

    public static class Debug
    {
        public static void Log(object message)
        {
            Console.WriteLine("  [log] " + message);
        }

        public static void LogWarning(object message)
        {
            Console.WriteLine("  [warning] " + message);
        }

        /// <summary>Counts as a test failure unless the test expects it (Expect.Errors).</summary>
        public static void LogError(object message)
        {
            Console.WriteLine("  [error] " + message);
            LogCapture.Errors++;
        }

        /// <summary>Counts as a test failure unless the test expects it (Expect.Errors).</summary>
        public static void LogException(Exception exception)
        {
            Console.WriteLine("  [exception] " + exception);
            LogCapture.Errors++;
        }
    }

    public class ScriptableObject
    {
        public static T CreateInstance<T>() where T : new()
        {
            return new T();
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public sealed class SerializeField : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true)]
    public sealed class TooltipAttribute : Attribute
    {
        public readonly string tooltip;

        public TooltipAttribute(string tooltip)
        {
            this.tooltip = tooltip;
        }
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class HeaderAttribute : Attribute
    {
        public readonly string header;

        public HeaderAttribute(string header)
        {
            this.header = header;
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CreateAssetMenuAttribute : Attribute
    {
        public string fileName { get; set; }
        public string menuName { get; set; }
    }
}

/// <summary>Test-harness side of the shim: counts Debug.LogError / LogException calls.</summary>
public static class LogCapture
{
    public static int Errors;
}
