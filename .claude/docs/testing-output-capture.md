# xunit v3 Output Capturing

## Overview

All test projects are configured with xunit v3 output capturing via assembly-level attributes in `XunitConfiguration.cs`.

**Documentation:** https://xunit.net/docs/capturing-output

## Configuration Files

Each test project has `XunitConfiguration.cs`:

```csharp
// Capture Console.WriteLine and Console.Error
[assembly: CaptureConsole]

// Capture Trace and Debug output (Debug only in Debug builds)
[assembly: CaptureTrace]
```

**Location:** `src/*/XunitConfiguration.cs` in all `*.Tests` projects

## Usage in Tests

### Simple Console Output

```csharp
[Fact]
public void MyTest()
{
    Console.WriteLine("This will appear in test output!");
    Console.Error.WriteLine("Error output also captured!");

    Assert.True(true);
}
```

### Trace and Debug Output

```csharp
[Fact]
public void MyDebugTest()
{
    Trace.WriteLine("Trace output captured");
    Debug.WriteLine("Debug output (Debug builds only)");

    Assert.True(true);
}
```

### Async Tests

```csharp
[Fact]
public async Task MyAsyncTest()
{
    Console.WriteLine("Before async");
    await Task.Delay(10, TestContext.Current.CancellationToken);
    Console.WriteLine("After async");

    Assert.True(true);
}
```

## Viewing Output

### Default (quiet mode)
```bash
dotnet test
# Output hidden unless test fails
```

### Detailed output (always show)
```bash
dotnet test --logger "console;verbosity=detailed"
# Shows "Standard Output Messages" for all tests
```

### Filter specific test
```bash
dotnet test --filter "FullyQualifiedName~MyTest" --logger "console;verbosity=detailed"
```

## Example Output

```
Passed ScreepsDotNet.Engine.Tests.MyTest [10 ms]
  Standard Output Messages:
 ✅ Console output is captured!
 ⚠️ Error output is also captured!
```

## Important Limitations

⚠️ **Background Threads:** Output written on background threads NOT associated with the test will be **silently discarded**.

```csharp
// ❌ BAD - Background thread output will be LOST
_ = Task.Run(() =>
{
    Console.WriteLine("This will be discarded!");
});

// ✅ GOOD - Test thread output will be captured
Console.WriteLine("This will be captured!");
```

## Examples

See `src/ScreepsDotNet.Engine.Tests/_Examples/OutputCapturingExample.cs` for working examples:

- Basic console output
- Trace/Debug output
- Async output
- Background thread warnings

## Alternative: ITestOutputHelper (v2 compatibility)

xunit v3 also supports the v2 `ITestOutputHelper` pattern:

```csharp
public class MyTests(ITestOutputHelper output)
{
    [Fact]
    public void MyTest()
    {
        output.WriteLine("Test output via ITestOutputHelper");
        Assert.True(true);
    }
}
```

**Recommendation:** Use `Console.WriteLine` for simplicity unless you need per-test-instance output isolation.

## Configured Test Projects

1. ✅ ScreepsDotNet.Engine.Tests
2. ✅ ScreepsDotNet.Backend.Http.Tests
3. ✅ ScreepsDotNet.Backend.Cli.Tests
4. ✅ ScreepsDotNet.Driver.Tests

All projects have `XunitConfiguration.cs` with `[assembly: CaptureConsole]` and `[assembly: CaptureTrace]`.
