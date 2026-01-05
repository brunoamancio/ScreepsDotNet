using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserCodeRepository
{
    Task<IReadOnlyCollection<UserCodeBranch>> GetBranchesAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserCodeBranch?> GetBranchAsync(string userId, string branchIdentifier, CancellationToken cancellationToken = default);
}
