namespace ScreepsDotNet.Driver.Services.Rooms;

using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Abstractions.Bulk;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal sealed class InterRoomTransferProcessor(IMongoDatabaseProvider databaseProvider, IBulkWriterFactory bulkWriterFactory, IRoomDataService roomDataService) : IInterRoomTransferProcessor
{
    private static readonly string[] MovingTypes = [RoomObjectTypes.Creep, RoomObjectTypes.PowerCreep];
    private readonly IMongoCollection<RoomObjectDocument> _roomObjects = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);

    public async Task<int> ProcessTransfersAsync(
        IReadOnlyDictionary<string, RoomInfoSnapshot> accessibleRooms,
        CancellationToken token = default)
    {
        var filter = Builders<RoomObjectDocument>.Filter.And(
            Builders<RoomObjectDocument>.Filter.In(document => document.Type, MovingTypes),
            Builders<RoomObjectDocument>.Filter.Ne(RoomDocumentFields.RoomObject.InterRoom, BsonNull.Value));

        var pending = await _roomObjects
            .Find(filter)
            .ToListAsync(token)
            .ConfigureAwait(false);

        if (pending.Count == 0)
            return 0;

        var writer = bulkWriterFactory.CreateRoomObjectsWriter();
        var activatedRooms = new HashSet<string>(StringComparer.Ordinal);
        var processed = 0;

        foreach (var document in pending)
        {
            if (!TryParseDestination(document.InterRoom, out var destination))
                continue;

            if (!IsAccessible(destination.RoomName, accessibleRooms))
                continue;

            writer.Update(document, BuildUpdateDocument(destination));
            activatedRooms.Add(destination.RoomName);
            processed++;
        }

        if (!writer.HasPendingOperations)
            return 0;

        await writer.ExecuteAsync(token).ConfigureAwait(false);

        if (activatedRooms.Count > 0)
            await roomDataService.ActivateRoomsAsync(activatedRooms, token).ConfigureAwait(false);

        return processed;
    }

    private static bool IsAccessible(string roomName, IReadOnlyDictionary<string, RoomInfoSnapshot> accessibleRooms)
        => accessibleRooms.Count == 0 || accessibleRooms.ContainsKey(roomName);

    private static bool TryParseDestination(BsonDocument? source, out InterRoomDestination destination)
    {
        destination = default!;
        if (source is null)
            return false;

        if (!source.TryGetValue(RoomDocumentFields.RoomObject.InterRoomFields.Room, out var roomValue) ||
            roomValue.IsString == false)
            return false;

        var roomName = roomValue.AsString;
        if (string.IsNullOrWhiteSpace(roomName))
            return false;

        var x = source.TryGetValue(RoomDocumentFields.RoomObject.InterRoomFields.X, out var xValue) ? xValue.ToInt32() : 0;
        var y = source.TryGetValue(RoomDocumentFields.RoomObject.InterRoomFields.Y, out var yValue) ? yValue.ToInt32() : 0;

        string? shard = null;
        if (source.TryGetValue(RoomDocumentFields.RoomObject.InterRoomFields.Shard, out var shardValue) && shardValue.IsString)
            shard = shardValue.AsString;

        destination = new InterRoomDestination(roomName, Math.Clamp(x, 0, 49), Math.Clamp(y, 0, 49), shard);
        return true;
    }

    private static BsonDocument BuildUpdateDocument(InterRoomDestination destination)
    {
        var document = new BsonDocument
        {
            [RoomDocumentFields.RoomObject.Room] = destination.RoomName,
            [RoomDocumentFields.RoomObject.X] = destination.X,
            [RoomDocumentFields.RoomObject.Y] = destination.Y,
            [RoomDocumentFields.RoomObject.InterRoom] = BsonNull.Value
        };

        if (!string.IsNullOrWhiteSpace(destination.Shard))
            document[RoomDocumentFields.RoomObject.Shard] = destination.Shard;

        return document;
    }

    private sealed record InterRoomDestination(string RoomName, int X, int Y, string? Shard);
}
