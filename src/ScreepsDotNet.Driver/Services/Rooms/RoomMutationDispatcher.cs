using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

namespace ScreepsDotNet.Driver.Services.Rooms;

internal sealed class RoomMutationDispatcher(
    IBulkWriterFactory bulkWriterFactory,
    IRoomDataService roomDataService) : IRoomMutationDispatcher
{
    public async Task ApplyAsync(RoomMutationBatch batch, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentException.ThrowIfNullOrWhiteSpace(batch.RoomName);

        var objectsWriter = bulkWriterFactory.CreateRoomObjectsWriter();

        ApplyUpserts(objectsWriter, batch.ObjectUpserts);
        ApplyPatches(objectsWriter, batch.ObjectPatches);
        ApplyRemovals(objectsWriter, batch.ObjectDeletes);

        if (objectsWriter.HasPendingOperations)
            await objectsWriter.ExecuteAsync(token).ConfigureAwait(false);

        if (batch.RoomInfoPatch is not null)
            await ApplyRoomInfoPatchAsync(batch.RoomName, batch.RoomInfoPatch, token).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(batch.EventLogJson))
            await roomDataService.SaveRoomEventLogAsync(batch.RoomName, batch.EventLogJson!, token).ConfigureAwait(false);

        if (!string.IsNullOrWhiteSpace(batch.MapViewJson))
            await roomDataService.SaveMapViewAsync(batch.RoomName, batch.MapViewJson!, token).ConfigureAwait(false);
    }

    private static void ApplyUpserts(IBulkWriter<RoomObjectDocument> writer, IReadOnlyList<RoomObjectUpsert> upserts)
    {
        if (upserts.Count == 0) return;
        foreach (var upsert in upserts)
        {
            if (string.IsNullOrWhiteSpace(upsert.DocumentJson))
                continue;

            var document = ParseDocument(upsert.DocumentJson);
            var entity = BsonSerializer.Deserialize<RoomObjectDocument>(document);
            writer.Insert(entity);
        }
    }

    private static void ApplyPatches(IBulkWriter<RoomObjectDocument> writer, IReadOnlyList<RoomObjectPatch> patches)
    {
        if (patches.Count == 0) return;
        foreach (var patch in patches)
        {
            if (string.IsNullOrWhiteSpace(patch.ObjectId) || string.IsNullOrWhiteSpace(patch.UpdateJson))
                continue;

            var delta = ParseDocument(patch.UpdateJson);
            writer.Update(patch.ObjectId, delta);
        }
    }

    private static void ApplyRemovals(IBulkWriter<RoomObjectDocument> writer, IReadOnlyList<string> removals)
    {
        if (removals.Count == 0) return;
        foreach (var id in removals)
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            writer.Remove(id);
        }
    }

    private async Task ApplyRoomInfoPatchAsync(string roomName, RoomInfoPatch patch, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(patch.UpdateJson))
            return;

        var delta = ParseDocument(patch.UpdateJson);
        var roomsWriter = bulkWriterFactory.CreateRoomsWriter();
        roomsWriter.Update(roomName, delta);
        if (roomsWriter.HasPendingOperations)
            await roomsWriter.ExecuteAsync(token).ConfigureAwait(false);
    }

    private static BsonDocument ParseDocument(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return BsonDocument.Parse(json);
    }
}
