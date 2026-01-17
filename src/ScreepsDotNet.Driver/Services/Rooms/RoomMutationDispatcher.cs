using System.Text.Json;
using MongoDB.Bson;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Environment;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Services.Rooms;

internal sealed class RoomMutationDispatcher(
    IBulkWriterFactory bulkWriterFactory,
    IRoomDataService roomDataService,
    IEnvironmentService environmentService,
    IRoomObjectBlueprintEnricher blueprintEnricher) : IRoomMutationDispatcher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task ApplyAsync(RoomMutationBatch batch, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.RoomName);

        var objectsWriter = bulkWriterFactory.CreateRoomObjectsWriter();
        if (batch.ObjectUpserts.Count > 0) {
            var gameTime = await environmentService.GetGameTimeAsync(token).ConfigureAwait(false);
            ApplyUpserts(objectsWriter, batch.ObjectUpserts, gameTime);
        }

        ApplyPatches(objectsWriter, batch.ObjectPatches);
        ApplyRemovals(objectsWriter, batch.ObjectDeletes);

        if (objectsWriter.HasPendingOperations)
            await objectsWriter.ExecuteAsync(token).ConfigureAwait(false);

        if (batch.RoomInfoPatch is not null)
            await ApplyRoomInfoPatchAsync(batch.RoomName, batch.RoomInfoPatch, token).ConfigureAwait(false);

        if (batch.EventLog is not null) {
            var payload = JsonSerializer.Serialize(batch.EventLog, batch.EventLog.GetType(), JsonOptions);
            await roomDataService.SaveRoomEventLogAsync(batch.RoomName, payload, token).ConfigureAwait(false);
        }

        if (batch.MapView is not null) {
            var payload = JsonSerializer.Serialize(batch.MapView, batch.MapView.GetType(), JsonOptions);
            await roomDataService.SaveMapViewAsync(batch.RoomName, payload, token).ConfigureAwait(false);
        }
    }

    private void ApplyUpserts(IBulkWriter<RoomObjectDocument> writer, IReadOnlyList<RoomObjectUpsert> upserts, int? gameTime)
    {
        if (upserts.Count == 0) return;
        foreach (var upsert in upserts) {
            var enriched = blueprintEnricher.Enrich(upsert.Document, gameTime);
            var entity = RoomContractMapper.MapRoomObjectDocument(enriched);
            writer.Insert(entity);
        }
    }

    private static void ApplyPatches(IBulkWriter<RoomObjectDocument> writer, IReadOnlyList<RoomObjectPatch> patches)
    {
        if (patches.Count == 0) return;
        foreach (var patch in patches) {
            if (string.IsNullOrWhiteSpace(patch.ObjectId))
                continue;

            var delta = BuildPatchDocument(patch.Payload);
            if (delta is null || delta.ElementCount == 0)
                continue;

            writer.Update(patch.ObjectId, delta);
        }
    }

    private static void ApplyRemovals(IBulkWriter<RoomObjectDocument> writer, IReadOnlyList<string> removals)
    {
        if (removals.Count == 0) return;
        foreach (var id in removals) {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            writer.Remove(id);
        }
    }

    private async Task ApplyRoomInfoPatchAsync(string roomName, RoomInfoPatchPayload patch, CancellationToken token)
    {
        if (!patch.HasChanges)
            return;

        var delta = RoomContractMapper.CreateRoomInfoPatchDocument(patch);
        if (delta.ElementCount == 0)
            return;

        var roomsWriter = bulkWriterFactory.CreateRoomsWriter();
        roomsWriter.Update(roomName, delta);
        if (roomsWriter.HasPendingOperations)
            await roomsWriter.ExecuteAsync(token).ConfigureAwait(false);
    }

    private static BsonDocument? BuildPatchDocument(IRoomObjectPatchPayload payload)
        => payload switch
        {
            RoomObjectPatchPayload typed => RoomContractMapper.CreateRoomObjectPatchDocument(typed),
            BsonRoomObjectPatchPayload legacy => legacy.Document.DeepClone().AsBsonDocument,
            _ => null
        };
}
