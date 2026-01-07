namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IWorldStatsRepository
{
    Task<MapStatsResult> GetMapStatsAsync(MapStatsRequest request, CancellationToken cancellationToken = default);
}
