using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace ScreepsDotNet.Driver.Extensions;

internal static class BsonSerializationExtensions
{
    public static string ToStableJson(this object? document)
    {
        if (document is null)
            return string.Empty;

        try
        {
            return document.ToJson();
        }
        catch (BsonSerializationException)
        {
            return JsonSerializer.Serialize(document);
        }
        catch (NotSupportedException)
        {
            return JsonSerializer.Serialize(document);
        }
    }
}
