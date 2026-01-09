namespace ScreepsDotNet.Backend.Core.Services;

using System.Threading;
using System.Threading.Tasks;

public interface IObjectNameService
{
    Task<string> GenerateSpawnNameAsync(string userId, CancellationToken cancellationToken = default);

    Task<bool> IsSpawnNameUniqueAsync(string userId, string name, CancellationToken cancellationToken = default);
}
