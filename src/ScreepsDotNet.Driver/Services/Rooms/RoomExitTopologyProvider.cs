namespace ScreepsDotNet.Driver.Services.Rooms;

using MongoDB.Driver;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Abstractions.Rooms;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

internal sealed class RoomExitTopologyProvider(IMongoDatabaseProvider databaseProvider) : IRoomExitTopologyProvider
{
    private readonly IMongoCollection<RoomDocument> _rooms = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);
    private readonly IMongoCollection<RoomTerrainDocument> _roomTerrain = databaseProvider.GetCollection<RoomTerrainDocument>(databaseProvider.Settings.RoomTerrainCollection);
    private readonly Lock _gate = new();
    private IReadOnlyDictionary<string, RoomExitTopology>? _cache;

    public async Task<IReadOnlyDictionary<string, RoomExitTopology>> GetTopologyAsync(CancellationToken token = default)
    {
        if (TryGetCached(out var cached))
            return cached;

        var topology = await BuildAsync(token).ConfigureAwait(false);
        Cache(topology);
        return topology;
    }

    public void Invalidate()
    {
        lock (_gate)
            _cache = null;
    }

    private bool TryGetCached(out IReadOnlyDictionary<string, RoomExitTopology> cache)
    {
        lock (_gate)
        {
            if (_cache is not null)
            {
                cache = _cache;
                return true;
            }
        }

        cache = null!;
        return false;
    }

    private void Cache(IReadOnlyDictionary<string, RoomExitTopology> topology)
    {
        lock (_gate)
            _cache = topology;
    }

    private async Task<IReadOnlyDictionary<string, RoomExitTopology>> BuildAsync(CancellationToken token)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var roomDocuments = await _rooms.Find(room => room.Status == RoomDocumentFields.RoomStatusValues.Normal)
                                        .ToListAsync(token)
                                        .ConfigureAwait(false);

        var accessibleRooms = roomDocuments
            .Where(room => !string.IsNullOrWhiteSpace(room.Id) && (!room.OpenTime.HasValue || room.OpenTime <= now))
            .Select(room => room.Id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (accessibleRooms.Length == 0)
            return new Dictionary<string, RoomExitTopology>(0, StringComparer.Ordinal);

        var filter = Builders<RoomTerrainDocument>.Filter.In(document => document.Room, accessibleRooms);
        var terrainDocuments = await _roomTerrain.Find(filter)
                                                 .ToListAsync(token)
                                                 .ConfigureAwait(false);

        var terrainMap = terrainDocuments
            .Where(document => !string.IsNullOrWhiteSpace(document.Room) && !string.IsNullOrEmpty(document.Terrain))
            .GroupBy(document => document.Room!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Terrain!, StringComparer.Ordinal);

        return RoomExitTopologyBuilder.Build(accessibleRooms, terrainMap);
    }
}
