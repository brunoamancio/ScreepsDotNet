namespace ScreepsDotNet.Backend.Core.Services;

/// <summary>
/// Provides diagnostic information about the Engine subsystem.
/// Used by CLI/HTTP endpoints for observability and debugging.
/// </summary>
public interface IEngineDiagnosticsService
{
    /// <summary>
    /// Gets aggregated Engine statistics (rooms processed, validation stats, etc.).
    /// </summary>
    Task<EngineStatisticsSnapshot> GetEngineStatisticsAsync(CancellationToken token = default);
}
