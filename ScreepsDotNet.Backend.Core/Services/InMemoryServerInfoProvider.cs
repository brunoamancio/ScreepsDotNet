using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Services;

public sealed class InMemoryServerInfoProvider : IServerInfoProvider
{
    private readonly IOptionsMonitor<ServerInfoOptions> _optionsMonitor;

    public InMemoryServerInfoProvider(IOptionsMonitor<ServerInfoOptions> optionsMonitor)
        => _optionsMonitor = optionsMonitor;

    public ServerInfo GetServerInfo()
    {
        // TODO: Replace this in-memory configuration-backed provider with a storage/engine-backed implementation.
        var options = _optionsMonitor.CurrentValue;
        return new ServerInfo(options.Name, options.Build, options.CliEnabled);
    }
}
