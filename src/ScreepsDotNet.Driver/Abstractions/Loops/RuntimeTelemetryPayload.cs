namespace ScreepsDotNet.Driver.Abstractions.Loops;

public sealed record RuntimeTelemetryPayload(
    string UserId,
    int GameTime,
    int CpuLimit,
    int CpuBucket,
    int CpuUsed,
    bool TimedOut,
    bool ScriptError,
    long HeapUsedBytes,
    long HeapSizeLimitBytes,
    string? ErrorMessage);
