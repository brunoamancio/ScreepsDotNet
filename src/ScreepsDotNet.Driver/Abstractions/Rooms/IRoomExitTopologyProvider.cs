namespace ScreepsDotNet.Driver.Abstractions.Rooms;

using ScreepsDotNet.Driver.Contracts;

public interface IRoomExitTopologyProvider
{
    Task<IReadOnlyDictionary<string, RoomExitTopology>> GetTopologyAsync(CancellationToken token = default);

    void Invalidate();
}
