namespace ScreepsDotNet.Backend.Core.Services;

/// <summary>
/// Snapshot of aggregated Engine statistics for diagnostics.
/// </summary>
public sealed record EngineStatisticsSnapshot(
    int TotalRoomsProcessed,
    double AverageProcessingTimeMs,
    int TotalIntentsValidated,
    double RejectionRate,
    string? TopErrorCode,
    string? TopIntentType
);
