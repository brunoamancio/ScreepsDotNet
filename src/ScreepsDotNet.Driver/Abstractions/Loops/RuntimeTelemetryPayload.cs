namespace ScreepsDotNet.Driver.Abstractions.Loops;

public sealed record RuntimeTelemetryPayload(
    DriverProcessType Loop,
    string UserId,
    int GameTime,
    int CpuLimit,
    int CpuBucket,
    int CpuUsed,
    bool TimedOut,
    bool ScriptError,
    long HeapUsedBytes,
    long HeapSizeLimitBytes,
    string? ErrorMessage,
    int? QueueDepth = null,
    bool ColdStartRequested = false,
    string? Stage = null);
