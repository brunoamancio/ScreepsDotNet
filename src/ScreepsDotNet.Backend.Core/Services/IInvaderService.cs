namespace ScreepsDotNet.Backend.Core.Services;

using ScreepsDotNet.Backend.Core.Models;

public interface IInvaderService
{
    Task<CreateInvaderResult> CreateInvaderAsync(string userId, CreateInvaderRequest request, CancellationToken cancellationToken = default);

    Task<RemoveInvaderResult> RemoveInvaderAsync(string userId, RemoveInvaderRequest request, CancellationToken cancellationToken = default);
}
