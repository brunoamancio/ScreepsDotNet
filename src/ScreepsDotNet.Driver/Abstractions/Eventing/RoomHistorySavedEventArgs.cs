using ScreepsDotNet.Driver.Abstractions.History;

namespace ScreepsDotNet.Driver.Abstractions.Eventing;

public sealed class RoomHistorySavedEventArgs(string roomName, int baseGameTime, RoomHistoryChunk chunk) : EventArgs
{
    public string RoomName { get; } = roomName;
    public int BaseGameTime { get; } = baseGameTime;
    public RoomHistoryChunk Chunk { get; } = chunk;
}
