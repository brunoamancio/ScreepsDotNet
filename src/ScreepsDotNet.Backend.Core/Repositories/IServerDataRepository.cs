namespace ScreepsDotNet.Backend.Core.Repositories;

using ScreepsDotNet.Backend.Core.Models;

public interface IServerDataRepository
{
    Task<ServerData> GetServerDataAsync(CancellationToken cancellationToken = default);
}
