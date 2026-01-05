using ScreepsDotNet.Backend.Core.Models;

namespace ScreepsDotNet.Backend.Core.Repositories;

/// <summary>
/// Provides direct access to server metadata stored in the legacy Screeps infrastructure.
/// </summary>
public interface IServerInfoRepository
{
    ServerInfo GetServerInfo();
}
