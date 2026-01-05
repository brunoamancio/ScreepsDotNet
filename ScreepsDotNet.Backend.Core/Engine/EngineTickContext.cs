namespace ScreepsDotNet.Backend.Core.Engine;

/// <summary>
/// Provides high-level information about the tick being executed.
/// </summary>
public sealed record EngineTickContext(long TickNumber, DateTimeOffset ScheduledAtUtc);
