namespace ScreepsDotNet.Driver.Abstractions.Engine;

/// <summary>
/// High-level entry points exposed by the managed engine so driver loops can delegate
/// simulation work without referencing engine-specific types.
/// </summary>
public interface IEngineHost
{
    /// <summary>
    /// Executes the global processor for the given tick.
    /// </summary>
    Task RunGlobalAsync(int gameTime, CancellationToken token = default);

    /// <summary>
    /// Executes room-level processing for the given room and tick.
    /// </summary>
    Task RunRoomAsync(string roomName, int gameTime, CancellationToken token = default);
}
