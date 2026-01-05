using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;

namespace ScreepsDotNet.Backend.Core.Services;

public sealed class InMemoryServerInfoProvider : IServerInfoProvider
{
    private readonly IServerInfoRepository _repository;

    public InMemoryServerInfoProvider(IServerInfoRepository repository)
        => _repository = repository;

    public ServerInfo GetServerInfo()
    {
        // TODO: Replace this in-memory configuration-backed provider with a storage/engine-backed implementation.
        return _repository.GetServerInfo();
    }
}
