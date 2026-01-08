namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models;

public interface IPowerCreepService
{
    Task<IReadOnlyCollection<PowerCreepListItem>> GetListAsync(string userId, CancellationToken cancellationToken = default);

    Task<PowerCreepListItem> CreateAsync(string userId, string name, string className, CancellationToken cancellationToken = default);

    Task DeleteAsync(string userId, string creepId, CancellationToken cancellationToken = default);

    Task CancelDeleteAsync(string userId, string creepId, CancellationToken cancellationToken = default);

    Task RenameAsync(string userId, string creepId, string newName, CancellationToken cancellationToken = default);

    Task UpgradeAsync(string userId, string creepId, IReadOnlyDictionary<string, int> requestedPowers, CancellationToken cancellationToken = default);

    Task RegisterExperimentationAsync(string userId, CancellationToken cancellationToken = default);
}
