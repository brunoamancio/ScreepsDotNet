namespace ScreepsDotNet.Backend.Core.Services;

/// <summary>
/// Provides low-level control over runtime simulation settings exposed via the legacy CLI.
/// </summary>
public interface ISystemControlService
{
    /// <summary>
    /// Determines whether the simulation loop is currently paused.
    /// </summary>
    Task<bool> IsSimulationPausedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses the simulation loop.
    /// </summary>
    Task PauseSimulationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Resumes the simulation loop.
    /// </summary>
    Task ResumeSimulationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the configured minimal tick duration (in milliseconds).
    /// </summary>
    Task<int?> GetTickDurationAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the minimal tick duration (in milliseconds) and broadcasts it to workers.
    /// </summary>
    Task SetTickDurationAsync(int minimalDurationMilliseconds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a server-wide message to all connected clients.
    /// </summary>
    Task PublishServerMessageAsync(string message, CancellationToken cancellationToken = default);
}
