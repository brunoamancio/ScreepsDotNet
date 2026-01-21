namespace ScreepsDotNet.Backend.Core.Services;

/// <summary>
/// Stub implementation of IEngineDiagnosticsService for E8 Phase 2.
/// Returns placeholder statistics until full telemetry aggregation is implemented.
/// </summary>
public sealed class StubEngineDiagnosticsService : IEngineDiagnosticsService
{
    public Task<EngineStatisticsSnapshot> GetEngineStatisticsAsync(CancellationToken token = default)
    {
        // TODO: Implement proper statistics aggregation from telemetry pipeline
        // For now, return placeholder values
        var stats = new EngineStatisticsSnapshot(
            TotalRoomsProcessed: 0,
            AverageProcessingTimeMs: 0,
            TotalIntentsValidated: 0,
            RejectionRate: 0,
            TopErrorCode: null,
            TopIntentType: null
        );

        return Task.FromResult(stats);
    }
}
