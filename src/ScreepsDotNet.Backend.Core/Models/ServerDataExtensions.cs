namespace ScreepsDotNet.Backend.Core.Models;

public static class ServerDataExtensions
{
    public static ServerData WithCustomObjectOverrides(this ServerData source, IReadOnlyDictionary<string, object?> overrides)
    {
        if (overrides.Count == 0)
            return source;

        var merged = new Dictionary<string, object?>(source.CustomObjectTypes, StringComparer.Ordinal);
        foreach (var entry in overrides)
            merged[entry.Key] = entry.Value;

        return source with { CustomObjectTypes = merged };
    }
}
