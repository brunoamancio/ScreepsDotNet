namespace ScreepsDotNet.Engine.Tests._Examples;

using System.Diagnostics;

/// <summary>
/// Example demonstrating xunit v3 output capturing.
/// See: https://xunit.net/docs/capturing-output
///
/// Run with: dotnet test --filter "FullyQualifiedName~OutputCapturingExample"
/// </summary>
public sealed class OutputCapturingExample
{
    [Fact]
    public void Example_ConsoleWriteLine_CapturedInTestOutput()
    {
        // This will appear in test output thanks to [assembly: CaptureConsole]
        Console.WriteLine("✅ Console output is captured!");
        Console.Error.WriteLine("⚠️ Error output is also captured!");

        Assert.True(true);
    }

    [Fact]
    public void Example_TraceAndDebug_CapturedInTestOutput()
    {
        // This will appear in test output thanks to [assembly: CaptureTrace]
        // Note: Debug only works in Debug builds
        Trace.WriteLine("📝 Trace output is captured!");
        Debug.WriteLine("🐛 Debug output is captured (Debug builds only)!");

        Assert.True(true);
    }

    [Fact]
    public void Example_MultipleLines_AllCaptured()
    {
        Console.WriteLine("=== Test Starting ===");
        Console.WriteLine("Step 1: Setup");
        Console.WriteLine("Step 2: Execute");
        Console.WriteLine("Step 3: Verify");
        Console.WriteLine("=== Test Complete ===");

        Assert.True(true);
    }

    [Fact]
    public async Task Example_AsyncOutput_CapturedCorrectly()
    {
        Console.WriteLine("Before async operation");

        await Task.Delay(10, TestContext.Current.CancellationToken);
        Console.WriteLine("During async operation");

        await Task.Delay(10, TestContext.Current.CancellationToken);
        Console.WriteLine("After async operation");

        Assert.True(true);
    }

    /// <summary>
    /// IMPORTANT: Background threads not associated with the test will have their output silently discarded.
    /// Always use Console.WriteLine on the test thread or use TestContext.Current for extensibility code.
    /// </summary>
    [Fact]
    public async Task Example_BackgroundThreadWarning()
    {
        Console.WriteLine("✅ This output on test thread WILL be captured");

        // ⚠️ WARNING: This background thread output will be SILENTLY DISCARDED
        _ = Task.Run(() => Console.WriteLine("❌ This background thread output will be LOST!"), TestContext.Current.CancellationToken);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Console.WriteLine("✅ Back on test thread - this WILL be captured");
        Assert.True(true);
    }
}
