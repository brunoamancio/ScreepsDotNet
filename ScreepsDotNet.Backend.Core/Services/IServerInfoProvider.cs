using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Services;

public interface IServerInfoProvider
{
    ServerInfo GetServerInfo();
}
