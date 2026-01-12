namespace ScreepsDotNet.Driver.Services.Loops;

public sealed class MainLoopOptions
{
    public TimeSpan QueueFetchInterval { get; init; } = TimeSpan.FromMilliseconds(50);
}
