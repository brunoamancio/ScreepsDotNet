using MongoDB.Bson;

namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

internal static class BsonValueExtensions
{
    public static object? ToDotNet(this BsonValue value)
        => value.IsBsonNull ? null : BsonTypeMapper.MapToDotNetValue(value);
}
