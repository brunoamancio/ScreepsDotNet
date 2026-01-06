using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserCodeRepository
{
    Task<IReadOnlyCollection<UserCodeBranch>> GetBranchesAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserCodeBranch?> GetBranchAsync(string userId, string branchIdentifier, CancellationToken cancellationToken = default);

    Task<bool> UpdateBranchModulesAsync(string userId, string branchIdentifier, IDictionary<string, string> modules, CancellationToken cancellationToken = default);

    Task<bool> SetActiveBranchAsync(string userId, string branchName, string activeName, CancellationToken cancellationToken = default);

    Task<bool> CloneBranchAsync(string userId, string? sourceBranch, string newBranchName, IDictionary<string, string>? defaultModules, CancellationToken cancellationToken = default);

    Task<bool> DeleteBranchAsync(string userId, string branchName, CancellationToken cancellationToken = default);
}
