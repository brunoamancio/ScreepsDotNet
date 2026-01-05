using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserRepository
{
    Task<IReadOnlyCollection<UserSummary>> GetUsersAsync(CancellationToken cancellationToken = default);
}
