namespace ScreepsDotNet.Backend.Core.Repositories;

public interface IUserRepository
{
    Task<int> GetActiveUsersCountAsync(CancellationToken cancellationToken = default);
}
