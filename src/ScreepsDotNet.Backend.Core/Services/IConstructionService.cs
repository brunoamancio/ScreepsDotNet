namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models;

public interface IConstructionService
{
    Task<PlaceConstructionResult> CreateConstructionAsync(string userId, PlaceConstructionRequest request, CancellationToken cancellationToken = default);
}
