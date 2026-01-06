namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

using System;
using System.Collections.Generic;
using System.Text.Json;

internal static class JsonElementExtensions
{
    public static object? ToObjectValue(this JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => element.ToDictionary(),
            JsonValueKind.Array => element.ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    private static IDictionary<string, object?> ToDictionary(this JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            dictionary[property.Name] = property.Value.ToObjectValue();
        return dictionary;
    }

    private static IList<object?> ToList(this JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
            list.Add(item.ToObjectValue());
        return list;
    }
}
