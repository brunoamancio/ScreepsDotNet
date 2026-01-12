namespace ScreepsDotNet.Backend.Core.Services;

using System.Threading;
using System.Threading.Tasks;
using ScreepsDotNet.Backend.Core.Models;

public interface IInvaderService
{
    Task<CreateInvaderResult> CreateInvaderAsync(string userId, CreateInvaderRequest request, CancellationToken cancellationToken = default);

    Task<RemoveInvaderResult> RemoveInvaderAsync(string userId, RemoveInvaderRequest request, CancellationToken cancellationToken = default);
}
