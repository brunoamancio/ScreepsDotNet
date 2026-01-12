using System.Text.Json;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Driver.Abstractions.Config;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Loops;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Services.Loops;

internal sealed class ProcessorLoopWorker(IRoomDataService roomDataService, IEnvironmentService environmentService, IDriverLoopHooks loopHooks,
                                          IDriverConfig config, ILogger<ProcessorLoopWorker>? logger = null)
    : IProcessorLoopWorker
{
    private static readonly JsonSerializerOptions HistoryJsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IRoomDataService _rooms = roomDataService;
    private readonly IEnvironmentService _environment = environmentService;
    private readonly IDriverLoopHooks _hooks = loopHooks;
    private readonly IDriverConfig _config = config;
    private readonly ILogger<ProcessorLoopWorker>? _logger = logger;

    public async Task HandleRoomAsync(string roomName, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roomName);

        var gameTime = await _environment.GetGameTimeAsync(token).ConfigureAwait(false);
        var roomObjects = await _rooms.GetRoomObjectsAsync(roomName, token).ConfigureAwait(false);
        var intents = await _rooms.GetRoomIntentsAsync(roomName, token).ConfigureAwait(false);

        if (intents is not null)
            await ProcessRoomIntentsAsync(roomName, intents, token).ConfigureAwait(false);

        await _rooms.ClearRoomIntentsAsync(roomName, token).ConfigureAwait(false);

        var historyPayload = JsonSerializer.Serialize(new
        {
            room = roomName,
            objects = roomObjects.Objects,
            users = roomObjects.Users
        }, HistoryJsonOptions);

        await _hooks.SaveRoomHistoryAsync(roomName, gameTime, historyPayload, token).ConfigureAwait(false);

        var chunkSize = Math.Max(_config.HistoryChunkSize, 1);
        if (gameTime % chunkSize == 0)
        {
            var chunkBase = Math.Max(gameTime - chunkSize + 1, 0);
            await _hooks.UploadRoomHistoryChunkAsync(roomName, chunkBase, token).ConfigureAwait(false);
        }
    }

    private Task ProcessRoomIntentsAsync(string roomName, RoomIntentDocument intents, CancellationToken token)
    {
        if (intents.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var totalIntents = 0;
        foreach (var (_, payload) in intents.Users) {
            if (payload?.ObjectsManual is { Count: > 0 })
                totalIntents += payload.ObjectsManual.Count;
        }

        if (totalIntents > 0)
            _logger?.LogDebug("Processing {Count} intents for room {Room}.", totalIntents, roomName);

        return Task.CompletedTask;
    }
}
