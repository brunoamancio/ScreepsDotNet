namespace ScreepsDotNet.Driver.Abstractions.History;

using ScreepsDotNet.Driver.Contracts;

public interface IRoomStatsRepository
{
    Task AppendAsync(RoomStatsUpdate update, CancellationToken token = default);
}
