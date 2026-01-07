namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IWorldMetadataRepository
{
    Task<int> GetGameTimeAsync(CancellationToken cancellationToken = default);

    Task<int> GetTickDurationAsync(CancellationToken cancellationToken = default);

    Task<WorldSize> GetWorldSizeAsync(CancellationToken cancellationToken = default);
}
