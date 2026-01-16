namespace ScreepsDotNet.Storage.MongoRedis.Repositories;

using System.Collections.Generic;
using System.Linq;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Comparers;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

public sealed class MongoWorldStatsRepository(IMongoDatabaseProvider databaseProvider, IWorldMetadataRepository metadataRepository) : IWorldStatsRepository
{
    private static readonly string ControllerType = RoomObjectType.Controller.ToDocumentValue();
    private static readonly string InvaderCoreType = RoomObjectType.InvaderCore.ToDocumentValue();
    private static readonly string MineralType = RoomObjectType.Mineral.ToDocumentValue();
    private static readonly string[] ObjectTypes = [ControllerType, InvaderCoreType, MineralType];

    private readonly IMongoCollection<RoomDocument> _roomsCollection = databaseProvider.GetCollection<RoomDocument>(databaseProvider.Settings.RoomsCollection);
    private readonly IMongoCollection<RoomObjectDocument> _roomObjectsCollection = databaseProvider.GetCollection<RoomObjectDocument>(databaseProvider.Settings.RoomObjectsCollection);
    private readonly IMongoCollection<UserDocument> _usersCollection = databaseProvider.GetCollection<UserDocument>(databaseProvider.Settings.UsersCollection);

    public async Task<MapStatsResult> GetMapStatsAsync(MapStatsRequest request, CancellationToken cancellationToken = default)
    {
        var requestedRooms = NormalizeRooms(request.Rooms);
        var gameTime = await metadataRepository.GetGameTimeAsync(cancellationToken).ConfigureAwait(false);

        if (requestedRooms.Count == 0) {
            return new MapStatsResult(gameTime,
                                      new Dictionary<string, MapStatsRoom>(StringComparer.OrdinalIgnoreCase),
                                      new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                                      new Dictionary<string, MapStatsUser>(StringComparer.OrdinalIgnoreCase));
        }

        var stats = await LoadBaseRoomStatsAsync(requestedRooms, cancellationToken).ConfigureAwait(false);
        if (stats.Count == 0) {
            return new MapStatsResult(gameTime,
                                      new Dictionary<string, MapStatsRoom>(),
                                      new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                                      new Dictionary<string, MapStatsUser>(StringComparer.OrdinalIgnoreCase));
        }

        var builders = stats.ToDictionary(entry => entry.RoomName,
                                          entry => new MapStatsRoomBuilder(entry),
                                          StringComparer.OrdinalIgnoreCase);

        var userIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await PopulateRoomObjectsAsync(requestedRooms, builders, userIds, gameTime, cancellationToken).ConfigureAwait(false);

        var users = await LoadUsersAsync(userIds, cancellationToken).ConfigureAwait(false);

        var finalRooms = builders.Values.ToDictionary(builder => builder.RoomName,
                                                      builder => builder.Build(),
                                                      StringComparer.OrdinalIgnoreCase);

        return new MapStatsResult(gameTime,
                                  finalRooms,
                                  new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
                                  users);
    }

    private async Task<IReadOnlyList<MapStatsRoom>> LoadBaseRoomStatsAsync(IReadOnlyCollection<RoomReference> rooms, CancellationToken cancellationToken)
    {
        var filter = BuildRoomDocumentFilter(rooms);
        if (filter is null)
            return [];

        var documents = await _roomsCollection.Find(filter)
                                              .ToListAsync(cancellationToken)
                                              .ConfigureAwait(false);

        return documents.Select(document => new MapStatsRoom(document.Id, document.Status, document.Novice, document.RespawnArea, document.OpenTime, null, null, false, null))
                        .ToList();
    }

    private async Task PopulateRoomObjectsAsync(IReadOnlyCollection<RoomReference> rooms, IDictionary<string, MapStatsRoomBuilder> builders,
                                                ISet<string> userIds, int gameTime, CancellationToken cancellationToken)
    {
        var roomFilter = BuildRoomObjectFilter(rooms);
        if (roomFilter is null)
            return;

        var filter = Builders<RoomObjectDocument>.Filter.And(roomFilter,
                                                             Builders<RoomObjectDocument>.Filter.In(obj => obj.Type, ObjectTypes));

        var objects = await _roomObjectsCollection.Find(filter)
                                                  .ToListAsync(cancellationToken)
                                                  .ConfigureAwait(false);

        foreach (var obj in objects) {
            if (obj.Room is null || !builders.TryGetValue(obj.Room, out var builder))
                continue;

            if (string.Equals(obj.Type, ControllerType, StringComparison.Ordinal))
                ApplyController(builder, obj, userIds, gameTime);
            else if (string.Equals(obj.Type, InvaderCoreType, StringComparison.Ordinal))
                ApplyInvaderCore(builder, obj, userIds);
            else if (string.Equals(obj.Type, MineralType, StringComparison.Ordinal))
                ApplyMineral(builder, obj);
        }
    }

    private static void ApplyController(MapStatsRoomBuilder builder, RoomObjectDocument document, ISet<string> userIds, int gameTime)
    {
        if (!string.IsNullOrWhiteSpace(document.UserId)) {
            builder.Ownership = new RoomOwnershipInfo(document.UserId!, document.Level ?? 0);
            userIds.Add(document.UserId!);
        }
        else if (!string.IsNullOrWhiteSpace(document.Reservation?.UserId)) {
            builder.Ownership = new RoomOwnershipInfo(document.Reservation.UserId!, 0);
            userIds.Add(document.Reservation.UserId!);
        }

        if (document.Sign is not null && !string.IsNullOrWhiteSpace(document.Sign.UserId)) {
            builder.Sign = new RoomSignInfo(document.Sign.UserId!, document.Sign.Text, document.Sign.Time);
            userIds.Add(document.Sign.UserId!);
        }

        builder.IsSafeMode = document.SafeMode.HasValue && document.SafeMode > gameTime;
    }

    private static void ApplyInvaderCore(MapStatsRoomBuilder builder, RoomObjectDocument document, ISet<string> userIds)
    {
        if (string.IsNullOrWhiteSpace(document.UserId))
            return;

        var level = document.Level ?? 0;
        if (level <= 0)
            return;

        builder.Ownership = new RoomOwnershipInfo(document.UserId!, level);
        userIds.Add(document.UserId!);
    }

    private static void ApplyMineral(MapStatsRoomBuilder builder, RoomObjectDocument document)
    {
        if (string.IsNullOrWhiteSpace(document.MineralType))
            return;

        builder.PrimaryMineral = new RoomMineralInfo(document.MineralType!, document.Density);
    }

    private async Task<IReadOnlyDictionary<string, MapStatsUser>> LoadUsersAsync(ISet<string> userIds, CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return new Dictionary<string, MapStatsUser>(StringComparer.OrdinalIgnoreCase);

        var filter = Builders<UserDocument>.Filter.In(user => user.Id, userIds);
        var documents = await _usersCollection.Find(filter)
                                              .ToListAsync(cancellationToken)
                                              .ConfigureAwait(false);

        return documents.Where(user => !string.IsNullOrWhiteSpace(user.Id))
                        .Select(user => new MapStatsUser(user.Id!, user.Username ?? string.Empty, user.Badge))
                        .ToDictionary(user => user.Id, user => user, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<RoomReference> NormalizeRooms(IEnumerable<RoomReference> rooms)
        => rooms?.Where(reference => reference is not null && !string.IsNullOrWhiteSpace(reference.RoomName))
                 .Select(reference => RoomReference.Create(reference.RoomName, reference.ShardName))
                 .Distinct(RoomReferenceComparer.OrdinalIgnoreCase)
                 .ToList()
           ?? [];

    private static FilterDefinition<RoomDocument>? BuildRoomDocumentFilter(IReadOnlyCollection<RoomReference> rooms)
    {
        if (rooms.Count == 0)
            return null;

        var filters = new List<FilterDefinition<RoomDocument>>();
        foreach (var group in rooms.GroupBy(reference => reference.RoomName, StringComparer.OrdinalIgnoreCase)) {
            if (group.Any(reference => string.IsNullOrWhiteSpace(reference.ShardName))) {
                filters.Add(Builders<RoomDocument>.Filter.Eq(document => document.Id, group.Key));
                continue;
            }

            var shards = group.Select(reference => reference.ShardName!)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();

            if (shards.Count == 0) {
                filters.Add(Builders<RoomDocument>.Filter.Eq(document => document.Id, group.Key));
                continue;
            }

            var roomFilter = Builders<RoomDocument>.Filter.Eq(document => document.Id, group.Key);
            var shardFilter = Builders<RoomDocument>.Filter.In(document => document.Shard, shards);
            filters.Add(Builders<RoomDocument>.Filter.And(roomFilter, shardFilter));
        }

        return filters.Count switch
        {
            0 => null,
            1 => filters[0],
            _ => Builders<RoomDocument>.Filter.Or(filters)
        };
    }

    private static FilterDefinition<RoomObjectDocument>? BuildRoomObjectFilter(IReadOnlyCollection<RoomReference> rooms)
    {
        if (rooms.Count == 0)
            return null;

        var filters = new List<FilterDefinition<RoomObjectDocument>>();
        foreach (var group in rooms.GroupBy(reference => reference.RoomName, StringComparer.OrdinalIgnoreCase)) {
            if (group.Any(reference => string.IsNullOrWhiteSpace(reference.ShardName))) {
                filters.Add(Builders<RoomObjectDocument>.Filter.Eq(document => document.Room, group.Key));
                continue;
            }

            var shards = group.Select(reference => reference.ShardName!)
                              .Distinct(StringComparer.OrdinalIgnoreCase)
                              .ToList();

            if (shards.Count == 0) {
                filters.Add(Builders<RoomObjectDocument>.Filter.Eq(document => document.Room, group.Key));
                continue;
            }

            var roomFilter = Builders<RoomObjectDocument>.Filter.Eq(document => document.Room, group.Key);
            var shardFilter = Builders<RoomObjectDocument>.Filter.In(document => document.Shard, shards);
            filters.Add(Builders<RoomObjectDocument>.Filter.And(roomFilter, shardFilter));
        }

        return filters.Count switch
        {
            0 => null,
            1 => filters[0],
            _ => Builders<RoomObjectDocument>.Filter.Or(filters)
        };
    }

    private sealed class MapStatsRoomBuilder(MapStatsRoom source)
    {
        public string RoomName { get; } = source.RoomName;

        public string? Status { get; } = source.Status;

        public bool? IsNoviceArea { get; } = source.IsNoviceArea;

        public bool? IsRespawnArea { get; } = source.IsRespawnArea;

        public long? OpenTime { get; } = source.OpenTime;

        public RoomOwnershipInfo? Ownership { get; set; }

        public RoomSignInfo? Sign { get; set; }

        public bool IsSafeMode { get; set; }

        public RoomMineralInfo? PrimaryMineral { get; set; }

        public MapStatsRoom Build()
            => new(RoomName, Status, IsNoviceArea, IsRespawnArea, OpenTime, Ownership, Sign, IsSafeMode, PrimaryMineral);
    }
}
