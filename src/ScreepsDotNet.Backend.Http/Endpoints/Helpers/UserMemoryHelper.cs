namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

internal static class UserMemoryHelper
{
    public static IReadOnlyDictionary<string, object?> EnsureEffectiveMemory(IDictionary<string, object?> memory, IReadOnlyDictionary<string, object?> defaultMemory)
        => memory.Count == 0 ? defaultMemory
                             : memory as IReadOnlyDictionary<string, object?> ?? new Dictionary<string, object?>(memory, StringComparer.Ordinal);

    public static string EncodeMemoryValue(object value, string gzipPrefix)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new StreamWriter(gzip, Encoding.UTF8)) {
            var json = JsonSerializer.Serialize(value);
            writer.Write(json);
        }

        return gzipPrefix + Convert.ToBase64String(buffer.ToArray());
    }

    public static object? ResolveMemoryPath(IReadOnlyDictionary<string, object?> root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return root;

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        object? current = root;
        foreach (var segment in segments) {
            switch (current) {
                case IReadOnlyDictionary<string, object?> dictionary when dictionary.TryGetValue(segment, out current):
                case IDictionary<string, object?> mutableDictionary when mutableDictionary.TryGetValue(segment, out current):
                    continue;
                default:
                    return null;
            }
        }

        return current;
    }
}
