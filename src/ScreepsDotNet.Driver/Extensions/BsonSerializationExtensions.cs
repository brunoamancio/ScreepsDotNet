using MongoDB.Bson;

namespace ScreepsDotNet.Driver.Extensions;

internal static class BsonSerializationExtensions
{
    public static string ToStableJson(this object? document)
        => document is null ? string.Empty : document.ToJson();
}
