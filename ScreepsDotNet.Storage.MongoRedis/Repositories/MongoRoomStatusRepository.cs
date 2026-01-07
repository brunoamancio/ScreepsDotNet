namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoRoomStatusRepository(IMongoDatabaseProvider databaseProvider) : IRoomStatusRepository
{
    private readonly IMongoCollection<RoomDocument> _collection = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);

    public async Task<RoomStatusInfo?> GetRoomStatusAsync(string roomName, CancellationToken cancellationToken = default)
    {
        var filter = Builders<RoomDocument>.Filter.Eq(document => document.Id, roomName);
        var document = await _collection.Find(filter)
                                        .FirstOrDefaultAsync(cancellationToken)
                                        .ConfigureAwait(false);
        return document is null ? null : Convert(document);
    }

    public async Task<IReadOnlyDictionary<string, RoomStatusInfo>> GetRoomStatusesAsync(IEnumerable<string> roomNames, CancellationToken cancellationToken = default)
    {
        var names = roomNames.Where(name => !string.IsNullOrWhiteSpace(name))
                             .Select(name => name!)
                             .Distinct(StringComparer.OrdinalIgnoreCase)
                             .ToList();

        if (names.Count == 0)
            return new Dictionary<string, RoomStatusInfo>(StringComparer.OrdinalIgnoreCase);

        var filter = Builders<RoomDocument>.Filter.In(document => document.Id, names);
        var documents = await _collection.Find(filter)
                                         .ToListAsync(cancellationToken)
                                         .ConfigureAwait(false);

        return documents.Select(Convert)
                        .ToDictionary(status => status.RoomName, StringComparer.OrdinalIgnoreCase);
    }

    private static RoomStatusInfo Convert(RoomDocument document)
        => new(document.Id,
               document.Status,
               document.Novice,
               document.RespawnArea,
               document.OpenTime);
}
