using ScreepsDotNet.Driver.Services;
using ScreepsDotNet.Driver.Tests.TestSupport;

namespace ScreepsDotNet.Driver.Tests.Config;

public sealed class DriverConfigTests
{
    [Fact]
    public async Task Subscribe_MainLoopStage_InvokesEmitterHandler()
    {
        var environment = new FakeEnvironmentService();
        var config = new DriverConfig(environment);
        var token = TestContext.Current.CancellationToken;
        var received = new TaskCompletionSource<object?[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        using var subscription = config.Subscribe("mainLoopStage", args => received.TrySetResult(args ?? []));

        config.EmitMainLoopStage("start", new { Rooms = 1 });

        var args = await received.Task.WaitAsync(TimeSpan.FromSeconds(1), token);
        Assert.NotNull(args);
        Assert.Equal("start", args[0]);
    }

    [Fact]
    public void DisposingSubscription_UnregistersHandler()
    {
        var environment = new FakeEnvironmentService();
        var config = new DriverConfig(environment);
        var called = false;

        var subscription = config.Subscribe("runnerLoopStage", _ => called = true);
        subscription.Dispose();

        config.EmitRunnerLoopStage("finish");

        Assert.False(called);
    }
}
