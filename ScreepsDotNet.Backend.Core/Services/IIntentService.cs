namespace ScreepsDotNet.Backend.Core.Services;

using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Persists user intents (global and per-object) with the same semantics as the legacy Screeps backend.
/// </summary>
public interface IIntentService
{
    Task AddObjectIntentAsync(string roomName, string? shardName, string objectId, string intentName, JsonElement payload, string userId, CancellationToken cancellationToken = default);

    Task AddGlobalIntentAsync(string intentName, JsonElement payload, string userId, CancellationToken cancellationToken = default);
}
