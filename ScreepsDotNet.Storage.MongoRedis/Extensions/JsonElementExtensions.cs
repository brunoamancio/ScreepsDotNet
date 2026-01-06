using System.Text.Json;
using MongoDB.Bson;

namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

internal static class JsonElementExtensions
{
    public static BsonValue ToBsonValue(this JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => element.ToBsonDocument(),
            JsonValueKind.Array => element.ToBsonArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => BsonNull.Value
        };

    private static BsonDocument ToBsonDocument(this JsonElement element)
    {
        var document = new BsonDocument();
        foreach (var property in element.EnumerateObject())
            document[property.Name] = property.Value.ToBsonValue();
        return document;
    }

    private static BsonArray ToBsonArray(this JsonElement element)
    {
        var array = new BsonArray();
        foreach (var item in element.EnumerateArray())
            array.Add(item.ToBsonValue());
        return array;
    }
}
