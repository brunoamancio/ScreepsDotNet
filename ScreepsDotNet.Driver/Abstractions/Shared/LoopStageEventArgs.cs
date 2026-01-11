namespace ScreepsDotNet.Driver.Abstractions.Shared;

public sealed class LoopStageEventArgs(string stage, object? payload) : EventArgs
{
    public string Stage { get; } = stage;
    public object? Payload { get; } = payload;
}
