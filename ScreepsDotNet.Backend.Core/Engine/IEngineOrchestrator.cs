namespace ScreepsDotNet.Backend.Core.Engine;

/// <summary>
/// Coordinates execution of the Screeps engine tick lifecycle.
/// </summary>
public interface IEngineOrchestrator
{
    Task<EngineTickResult> ExecuteTickAsync(EngineTickContext context, CancellationToken cancellationToken = default);
}
