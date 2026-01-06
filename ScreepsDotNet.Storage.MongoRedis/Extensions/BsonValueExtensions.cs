using MongoDB.Bson;

namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

internal static class BsonValueExtensions
{
    public static object? ToDotNet(this BsonValue value)
        => value.IsBsonNull ? null : BsonTypeMapper.MapToDotNetValue(value);

    public static object? ToPlainObject(this BsonValue value)
        => value switch
        {
            BsonDocument document => document.ToPlainDictionary(),
            BsonArray array => array.ToPlainList(),
            BsonString s => s.AsString,
            BsonBoolean b => b.AsBoolean,
            BsonInt32 i => i.AsInt32,
            BsonInt64 l => l.AsInt64,
            BsonDouble d => d.AsDouble,
            BsonDecimal128 dec => (double)dec.ToDecimal(),
            BsonNull => null,
            _ => value.ToString()
        };

    private static IList<object?> ToPlainList(this BsonArray array)
    {
        var list = new List<object?>(array.Count);
        list.AddRange(array.Select(item => item.ToPlainObject()));
        return list;
    }
}
