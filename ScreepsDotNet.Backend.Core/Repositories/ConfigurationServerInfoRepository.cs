using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

/// <summary>
/// Temporary repository that reads server metadata directly from configuration files.
/// </summary>
public sealed class ConfigurationServerInfoRepository : IServerInfoRepository
{
    private readonly IOptionsMonitor<ServerInfoOptions> _optionsMonitor;

    public ConfigurationServerInfoRepository(IOptionsMonitor<ServerInfoOptions> optionsMonitor)
        => _optionsMonitor = optionsMonitor;

    public ServerInfo GetServerInfo()
    {
        var options = _optionsMonitor.CurrentValue;
        return new ServerInfo(options.Name, options.Build, options.CliEnabled);
    }
}
