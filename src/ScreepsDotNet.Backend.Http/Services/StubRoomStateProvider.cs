namespace ScreepsDotNet.Backend.Http.Services;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Data.Rooms;

/// <summary>
/// Stub implementation of IRoomStateProvider for HTTP backend diagnostics.
/// Returns empty room states until full Driver integration is added.
/// </summary>
internal sealed class StubRoomStateProvider : IRoomStateProvider
{
    public Task<RoomState> GetRoomStateAsync(string roomName, int gameTime, CancellationToken token = default)
    {
        // TODO: Implement proper Driver integration in HTTP backend
        // For now, return empty state for testing/debugging purposes
        var state = new RoomState(
            RoomName: roomName,
            GameTime: gameTime,
            Info: null,
            Objects: new Dictionary<string, RoomObjectSnapshot>(),
            Users: new Dictionary<string, UserState>(),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(),
            Flags: []
        );

        return Task.FromResult(state);
    }

    public void Invalidate(string roomName)
    {
        // No-op for stub
    }
}
