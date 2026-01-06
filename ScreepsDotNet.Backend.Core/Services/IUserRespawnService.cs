namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models;

public interface IUserRespawnService
{
    Task<UserRespawnResult> RespawnAsync(string userId, CancellationToken cancellationToken = default);
}
