namespace ScreepsDotNet.Storage.MongoRedis.Extensions;

internal static class DictionaryPathExtensions
{
    public static void SetValueAtPath(this IDictionary<string, object?> root, IReadOnlyList<string> segments, object? value)
    {
        if (segments.Count == 0)
            throw new ArgumentException("Segments cannot be empty.", nameof(segments));

        var current = root;
        for (var i = 0; i < segments.Count - 1; i++) {
            var segment = segments[i];
            if (!current.TryGetValue(segment, out var child) || child is not IDictionary<string, object?> childDictionary) {
                childDictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
                current[segment] = childDictionary;
            }

            current = childDictionary;
        }

        current[segments[^1]] = value;
    }

    public static void RemoveValueAtPath(this IDictionary<string, object?> root, IReadOnlyList<string> segments)
    {
        if (segments.Count == 0)
            return;

        Remove(root, segments, 0);
    }

    private static void Remove(IDictionary<string, object?> current, IReadOnlyList<string> segments, int index)
    {
        var key = segments[index];
        if (!current.TryGetValue(key, out var child))
            return;

        if (index == segments.Count - 1) {
            current.Remove(key);
            return;
        }

        if (child is IDictionary<string, object?> childDictionary) {
            Remove(childDictionary, segments, index + 1);
            if (childDictionary.Count == 0)
                current.Remove(key);
        }
    }
}
