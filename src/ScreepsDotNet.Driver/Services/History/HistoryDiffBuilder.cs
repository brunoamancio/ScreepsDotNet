using System.Text.Json.Nodes;

namespace ScreepsDotNet.Driver.Services.History;

internal static class HistoryDiffBuilder
{
    public static IReadOnlyDictionary<int, JsonNode?>? BuildChunk(IReadOnlyDictionary<int, JsonNode?> ticks, int baseGameTime)
    {
        if (!ticks.ContainsKey(baseGameTime))
            return null;

        var orderedKeys = ticks.Keys.OrderBy(tick => tick).ToArray();
        var result = new Dictionary<int, JsonNode?>(orderedKeys.Length);

        JsonNode? previous = null;
        foreach (var tick in orderedKeys)
        {
            if (!ticks.TryGetValue(tick, out var current))
                continue;

            if (previous is null || tick == baseGameTime)
                result[tick] = current?.DeepClone();
            else
                result[tick] = CreateDiff(previous, current);

            previous = current;
        }

        return result;
    }

    private static JsonNode? CreateDiff(JsonNode? previous, JsonNode? current)
    {
        if (previous is null || current is null)
            return current?.DeepClone();

        if (previous.GetType() != current.GetType())
            return current.DeepClone();

        if (current is JsonValue)
            return current.DeepClone();

        if (current is JsonArray array)
            return array.DeepClone();

        if (current is JsonObject currentObject && previous is JsonObject previousObject)
        {
            var diff = new JsonObject();
            var keys = currentObject.Select(pair => pair.Key)
                                    .Concat(previousObject.Select(pair => pair.Key))
                                    .Distinct(StringComparer.Ordinal);

            foreach (var key in keys)
            {
                var previousChild = previousObject[key];
                var currentChild = currentObject[key];

                if (currentChild is null)
                {
                    if (previousChild is not null)
                        diff[key] = null;
                    continue;
                }

                if (previousChild is null)
                {
                    diff[key] = currentChild.DeepClone();
                    continue;
                }

                if (JsonNode.DeepEquals(previousChild, currentChild))
                    continue;

                diff[key] = CreateDiff(previousChild, currentChild);
            }

            return diff;
        }

        return current.DeepClone();
    }
}
