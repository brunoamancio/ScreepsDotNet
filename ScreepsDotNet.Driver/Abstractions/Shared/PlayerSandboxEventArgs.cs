namespace ScreepsDotNet.Driver.Abstractions.Shared;

public sealed class PlayerSandboxEventArgs(string userId, object sandboxHandle) : EventArgs
{
    public string UserId { get; } = userId;
    public object SandboxHandle { get; } = sandboxHandle;
}
