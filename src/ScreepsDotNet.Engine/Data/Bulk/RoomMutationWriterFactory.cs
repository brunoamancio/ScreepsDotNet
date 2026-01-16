namespace ScreepsDotNet.Engine.Data.Bulk;

using ScreepsDotNet.Driver.Abstractions.Rooms;

internal sealed class RoomMutationWriterFactory(IRoomMutationDispatcher dispatcher) : IRoomMutationWriterFactory
{
    public IRoomMutationWriter Create(string roomName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        return new RoomMutationWriter(roomName, dispatcher);
    }
}
