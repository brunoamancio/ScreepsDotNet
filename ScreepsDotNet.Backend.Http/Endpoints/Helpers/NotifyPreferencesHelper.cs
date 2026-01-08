namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

internal static class NotifyPreferencesHelper
{
    public static Dictionary<string, object?> CreatePreferencesDictionary(object? notifyPrefs)
    {
        return notifyPrefs switch
        {
            IDictionary<string, object?> typedDictionary => new Dictionary<string, object?>(typedDictionary, StringComparer.Ordinal),
            IDictionary dictionary => CopyFromDictionary(dictionary),
            JsonElement { ValueKind: JsonValueKind.Object } element => CopyFromJson(element),
            _ => new Dictionary<string, object?>(StringComparer.Ordinal)
        };
    }

    public static void ApplyBooleanPreference(IDictionary<string, object?> preferences, string key, bool? value)
    {
        if (value.HasValue)
            preferences[key] = value.Value;
    }

    public static void ApplyIntervalPreference(IDictionary<string, object?> preferences, string key, int? value, IReadOnlyList<int> allowedValues)
    {
        if (value.HasValue && allowedValues.Contains(value.Value))
            preferences[key] = value.Value;
    }

    private static Dictionary<string, object?> CopyFromDictionary(IDictionary source)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in source) {
            if (entry.Key is string key)
                result[key] = entry.Value;
        }

        return result;
    }

    private static Dictionary<string, object?> CopyFromJson(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            result[property.Name] = ConvertJsonElementValue(property.Value);
        return result;
    }

    private static object? ConvertJsonElementValue(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt32(out var intValue) => intValue,
            JsonValueKind.String => element.GetString(),
            _ => null
        };
}
