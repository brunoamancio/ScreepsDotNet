namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Represents terminal send data stored on the terminal object.
/// </summary>
public sealed record TerminalSendSnapshot(
    string TargetRoomName,
    string ResourceType,
    int Amount,
    string? Description = null);
