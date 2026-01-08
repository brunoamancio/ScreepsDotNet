namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models;

public interface IPlayerSpawnService
{
    Task<PlaceSpawnResult> PlaceSpawnAsync(string userId, PlaceSpawnRequest request, CancellationToken cancellationToken = default);
}
