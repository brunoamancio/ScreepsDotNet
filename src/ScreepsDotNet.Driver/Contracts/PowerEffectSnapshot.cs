namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Represents an active power effect on a room object.
/// </summary>
public sealed record PowerEffectSnapshot(
    int Power,
    int Level,
    int EndTime);
