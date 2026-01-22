namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Represents a terminal send operation (inter-room resource transfer).
/// Legacy property-based representation - will be converted to intent pattern at Engine boundary.
/// </summary>
public sealed record TerminalSendSnapshot(
    string TargetRoomName,
    string ResourceType,
    int Amount,
    string? Description = null);
