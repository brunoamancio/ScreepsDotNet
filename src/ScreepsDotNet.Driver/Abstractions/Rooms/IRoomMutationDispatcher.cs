using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Driver.Abstractions.Rooms;

public interface IRoomMutationDispatcher
{
    Task ApplyAsync(RoomMutationBatch batch, CancellationToken token = default);
}
