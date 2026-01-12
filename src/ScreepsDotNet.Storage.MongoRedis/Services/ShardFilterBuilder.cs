namespace ScreepsDotNet.Storage.MongoRedis.Services;

using MongoDB.Bson;
using MongoDB.Driver;

internal static class ShardFilterBuilder
{
    public static FilterDefinition<BsonDocument> ForRoom(FilterDefinitionBuilder<BsonDocument> builder, string room, string? shard)
        => ApplyShard(builder, builder.Eq("room", room), shard);

    public static FilterDefinition<BsonDocument> ForRoomId(FilterDefinitionBuilder<BsonDocument> builder, string room, string? shard)
        => ApplyShardField(builder, builder.Eq("_id", room), shard, "shard");

    public static FilterDefinition<BsonDocument> ApplyShard(FilterDefinitionBuilder<BsonDocument> builder, FilterDefinition<BsonDocument> baseFilter, string? shard)
        => ApplyShardField(builder, baseFilter, shard, "shard");

    public static FilterDefinition<BsonDocument> ApplyShardField(FilterDefinitionBuilder<BsonDocument> builder,
                                                                 FilterDefinition<BsonDocument> baseFilter,
                                                                 string? shard,
                                                                 string fieldName)
    {
        if (!string.IsNullOrWhiteSpace(shard))
            return baseFilter & builder.Eq(fieldName, shard);

        return baseFilter & builder.Or(builder.Eq(fieldName, BsonNull.Value), builder.Exists(fieldName, false));
    }
}
