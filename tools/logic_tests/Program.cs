// MaliGo logic tests, run outside Unity: dotnet run --project tools/logic_tests
// Runs every public static void method whose name starts with "Test" in every class whose name ends
// with "Tests". A test fails by throwing, or by logging an error it did not expect (Expect.Errors).
// Prints PASS/FAIL per test and a total; exit code 1 on any failure.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static class Program
{
    public static int Main(string[] args)
    {
        string filter = args.Length > 0 ? args[0] : null;
        var tests = new List<MethodInfo>();
        foreach (Type type in typeof(Program).Assembly.GetTypes()
                     .Where(t => t.IsClass && t.Name.EndsWith("Tests", StringComparison.Ordinal))
                     .OrderBy(t => t.FullName, StringComparer.Ordinal))
        {
            tests.AddRange(type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name.StartsWith("Test", StringComparison.Ordinal)
                            && m.ReturnType == typeof(void)
                            && m.GetParameters().Length == 0
                            && !m.ContainsGenericParameters)
                .OrderBy(m => m.MetadataToken));
        }

        int passed = 0;
        var failed = new List<string>();
        foreach (MethodInfo test in tests)
        {
            string name = test.DeclaringType.Name + "." + test.Name;
            if (filter != null && name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            LogCapture.Errors = 0;
            MaliGo.Core.GameEvents.ClearPendingMoneyChanged();
            string failure = null;
            try
            {
                test.Invoke(null, null);
                if (LogCapture.Errors > 0)
                {
                    failure = LogCapture.Errors + " unexpected error(s) logged";
                }
            }
            catch (TargetInvocationException ex)
            {
                failure = (ex.InnerException ?? ex).GetType().Name + ": " + (ex.InnerException ?? ex).Message;
            }
            catch (Exception ex)
            {
                failure = ex.GetType().Name + ": " + ex.Message;
            }

            if (failure == null)
            {
                passed++;
                Console.WriteLine("PASS " + name);
            }
            else
            {
                failed.Add(name);
                Console.WriteLine("FAIL " + name + " - " + failure);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"{passed} passed, {failed.Count} failed, {passed + failed.Count} total");
        foreach (string name in failed)
        {
            Console.WriteLine("  failed: " + name);
        }

        return failed.Count == 0 && passed > 0 ? 0 : 1;
    }
}

public class AssertionException : Exception
{
    public AssertionException(string message) : base(message)
    {
    }
}

public static class Assert
{
    public static void True(bool condition, string message)
    {
        if (!condition)
        {
            throw new AssertionException(message);
        }
    }

    public static void Equal(float expected, float actual, string message, float tol = 0.005f)
    {
        if (float.IsNaN(actual) || Math.Abs(expected - actual) > tol)
        {
            throw new AssertionException($"{message}: expected {expected}, got {actual}");
        }
    }

    public static void Equal(string expected, string actual, string message)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new AssertionException($"{message}: expected \"{expected}\", got \"{actual}\"");
        }
    }
}

public static class Expect
{
    /// <summary>Runs the action and requires exactly `count` errors to be logged by it (they don't fail the test).</summary>
    public static void Errors(int count, Action action, string message)
    {
        int before = LogCapture.Errors;
        action();
        int logged = LogCapture.Errors - before;
        LogCapture.Errors = before;
        if (logged != count)
        {
            throw new AssertionException($"{message}: expected {count} logged error(s), got {logged}");
        }
    }
}
