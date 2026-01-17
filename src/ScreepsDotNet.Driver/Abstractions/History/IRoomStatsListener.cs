namespace ScreepsDotNet.Driver.Abstractions.History;

using ScreepsDotNet.Driver.Contracts;

public interface IRoomStatsListener
{
    Task OnRoomStatsAsync(RoomStatsUpdate update, CancellationToken token = default);
}
