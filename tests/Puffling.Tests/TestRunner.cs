using System.Diagnostics;
using System.Reflection;

namespace Puffling.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute : Attribute { }

public sealed class AssertException(string message) : Exception(message);

public static class Assert
{
    public static void True(bool cond, string msg) { if (!cond) throw new AssertException(msg); }
    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new AssertException($"{what}: expected {expected}, got {actual}");
    }
}

/// <summary>Zero-dependency test runner: finds every [Test] static method and runs it.
/// Exit code is non-zero on failure so it can gate CI.</summary>
public static class TestRunner
{
    public static int Main(string[] args)
    {
        string? filter = args.FirstOrDefault();
        var tests = typeof(TestRunner).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<TestAttribute>() != null)
            .Where(m => filter == null || m.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(m => m.DeclaringType!.Name).ThenBy(m => m.Name)
            .ToList();

        int failed = 0;
        foreach (var t in tests)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                t.Invoke(null, null);
                Console.WriteLine($"  PASS  {t.DeclaringType!.Name}.{t.Name} ({sw.ElapsedMilliseconds} ms)");
            }
            catch (TargetInvocationException ex)
            {
                failed++;
                Console.WriteLine($"  FAIL  {t.DeclaringType!.Name}.{t.Name}: {ex.InnerException?.Message}");
                if (ex.InnerException is not AssertException) Console.WriteLine(ex.InnerException);
            }
        }
        Console.WriteLine($"\n{tests.Count - failed}/{tests.Count} passed");
        return failed == 0 ? 0 : 1;
    }
}
