namespace ScreepsDotNet.Backend.Core.Engine;

/// <summary>
/// Summary data returned once the engine finishes processing a tick.
/// </summary>
public sealed record EngineTickResult(long TickNumber, TimeSpan Duration, bool Success, string? FailureReason = null);
