namespace ScreepsDotNet.Driver.Services.Loops;

public sealed class RunnerLoopOptions
{
    public TimeSpan FetchTimeout { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan IdleDelay { get; init; } = TimeSpan.FromMilliseconds(50);
}
