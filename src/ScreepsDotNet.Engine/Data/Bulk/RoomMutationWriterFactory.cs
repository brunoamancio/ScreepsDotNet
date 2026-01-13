namespace ScreepsDotNet.Engine.Data.Bulk;

using System.Text.Json;
using ScreepsDotNet.Driver.Abstractions.Rooms;

internal sealed class RoomMutationWriterFactory(
    IRoomMutationDispatcher dispatcher,
    JsonSerializerOptions? serializerOptions = null) : IRoomMutationWriterFactory
{
    public IRoomMutationWriter Create(string roomName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);
        return new RoomMutationWriter(roomName, dispatcher, serializerOptions);
    }
}
