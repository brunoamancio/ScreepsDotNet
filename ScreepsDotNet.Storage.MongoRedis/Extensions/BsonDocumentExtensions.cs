using MongoDB.Bson;

namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

internal static class BsonDocumentExtensions
{
    public static string? GetStringOrNull(this BsonDocument document, string fieldName)
        => document.TryGetValue(fieldName, out var value) && value.IsString ? value.AsString : null;

    public static bool GetBooleanOrDefault(this BsonDocument document, string fieldName)
        => document.TryGetValue(fieldName, out var value) && value.IsBoolean && value.AsBoolean;

    public static double GetDoubleOrDefault(this BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
            return 0;

        return value.IsNumeric ? value.ToDouble() : 0;
    }

    public static DateTime? GetDateTimeOrNull(this BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
            return null;

        if (value.BsonType == BsonType.DateTime)
            return value.ToUniversalTime();

        return null;
    }

    public static int GetInt32OrDefault(this BsonDocument document, string fieldName)
    {
        if (!document.TryGetValue(fieldName, out var value) || value.IsBsonNull)
            return 0;

        return value.IsNumeric ? value.ToInt32() : 0;
    }

    public static object? ToDotNet(this BsonDocument document, string fieldName, BsonValue defaultValue)
        => document.GetValue(fieldName, defaultValue).ToDotNet();

    public static IDictionary<string, object?> ToPlainDictionary(this BsonDocument document)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var element in document)
            result[element.Name] = element.Value.ToPlainObject();
        return result;
    }
}
