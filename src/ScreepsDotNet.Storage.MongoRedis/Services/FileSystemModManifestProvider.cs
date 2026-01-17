namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Intents;
using ScreepsDotNet.Backend.Core.Models.Mods;
using ScreepsDotNet.Backend.Core.Services;

public sealed class FileSystemModManifestProvider(IOptions<BotManifestOptions> options,
                                                 ILogger<FileSystemModManifestProvider> logger)
    : IModManifestProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BotManifestOptions _options = options.Value;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ModManifest _cache = ModManifest.Empty;
    private string? _cachedPath;

    public async Task<ModManifest> GetManifestAsync(CancellationToken cancellationToken = default)
    {
        var path = ResolveManifestPath();
        if (path is null)
            return ModManifest.Empty;

        var timestamp = File.GetLastWriteTimeUtc(path);

        if (string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) &&
            timestamp <= _cache.LastModifiedUtc) {
            return _cache;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (string.Equals(_cachedPath, path, StringComparison.OrdinalIgnoreCase) &&
                timestamp <= _cache.LastModifiedUtc) {
                return _cache;
            }

            var manifest = await LoadManifestAsync(path, timestamp, cancellationToken).ConfigureAwait(false);
            _cache = manifest;
            _cachedPath = path;
            return manifest;
        }
        finally {
            _lock.Release();
        }
    }

    private async Task<ModManifest> LoadManifestAsync(string path, DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        try {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;

            var bots = ParseBots(root);
            var customIntentTypes = ParseCustomIntentTypes(root);
            var customObjectTypes = ParseCustomObjectTypes(root);

            return new ModManifest(path, timestamp, bots, customIntentTypes, customObjectTypes);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) {
            logger.LogError(ex, "Failed to load mods manifest '{Path}'.", path);
            return new ModManifest(path,
                                   timestamp,
                                   new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                                   new Dictionary<string, IntentDefinition>(StringComparer.Ordinal),
                                   new Dictionary<string, object?>(StringComparer.Ordinal));
        }
    }

    private static IReadOnlyDictionary<string, string> ParseBots(JsonElement root)
    {
        if (!root.TryGetProperty("bots", out var botsElement) || botsElement.ValueKind != JsonValueKind.Object)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var bots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in botsElement.EnumerateObject()) {
            if (property.Value.ValueKind != JsonValueKind.String)
                continue;

            var directory = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            bots[property.Name] = directory!;
        }

        return bots;
    }

    private IReadOnlyDictionary<string, IntentDefinition> ParseCustomIntentTypes(JsonElement root)
    {
        if (!root.TryGetProperty("customIntentTypes", out var customElement) ||
            customElement.ValueKind != JsonValueKind.Object) {
            return new Dictionary<string, IntentDefinition>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, IntentDefinition>(StringComparer.Ordinal);
        foreach (var intentNode in customElement.EnumerateObject()) {
            if (intentNode.Value.ValueKind != JsonValueKind.Object) {
                logger.LogWarning("Custom intent '{Intent}' must be an object of field definitions.", intentNode.Name);
                continue;
            }

            var fields = new Dictionary<string, IntentFieldType>(StringComparer.Ordinal);
            var invalid = false;
            foreach (var field in intentNode.Value.EnumerateObject()) {
                if (field.Value.ValueKind != JsonValueKind.String) {
                    logger.LogWarning("Field '{Field}' on custom intent '{Intent}' must be a string.", field.Name, intentNode.Name);
                    invalid = true;
                    break;
                }

                var fieldType = field.Value.GetString();
                if (!TryMapIntentFieldType(fieldType, out var mapped)) {
                    logger.LogWarning("Unsupported field type '{Type}' on custom intent '{Intent}'.", fieldType, intentNode.Name);
                    invalid = true;
                    break;
                }

                fields[field.Name] = mapped;
            }

            if (invalid || fields.Count == 0)
                continue;

            result[intentNode.Name] = new IntentDefinition(intentNode.Name, fields);
        }

        return result;
    }

    private static IReadOnlyDictionary<string, object?> ParseCustomObjectTypes(JsonElement root)
    {
        if (!root.TryGetProperty("customObjectTypes", out var objectsElement) ||
            objectsElement.ValueKind != JsonValueKind.Object) {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var entry in objectsElement.EnumerateObject())
            result[entry.Name] = ConvertJsonElement(entry.Value);

        return result;
    }

    private static object? ConvertJsonElement(JsonElement element)
        => element.ValueKind switch
        {
            JsonValueKind.Object => ConvertObject(element),
            JsonValueKind.Array => ConvertArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.GetRawText()
        };

    private static object ConvertObject(JsonElement element)
    {
        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            dictionary[property.Name] = ConvertJsonElement(property.Value);
        return dictionary;
    }

    private static object ConvertArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
            list.Add(ConvertJsonElement(item));
        return list;
    }

    private static bool TryMapIntentFieldType(string? type, out IntentFieldType mapped)
    {
        switch (type) {
            case "string":
                mapped = IntentFieldType.ScalarString;
                return true;
            case "number":
                mapped = IntentFieldType.ScalarNumber;
                return true;
            case "boolean":
                mapped = IntentFieldType.ScalarBoolean;
                return true;
            case "price":
                mapped = IntentFieldType.Price;
                return true;
            case "string[]":
                mapped = IntentFieldType.StringArray;
                return true;
            case "number[]":
                mapped = IntentFieldType.NumberArray;
                return true;
            case "bodypart[]":
                mapped = IntentFieldType.BodyPartArray;
                return true;
            case "userString":
                mapped = IntentFieldType.UserString;
                return true;
            case "userText":
                mapped = IntentFieldType.UserText;
                return true;
            default:
                mapped = default;
                return false;
        }
    }

    private string? ResolveManifestPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.ManifestFile)) {
            var explicitPath = Path.GetFullPath(_options.ManifestFile);
            if (File.Exists(explicitPath))
                return explicitPath;

            logger.LogWarning("Mods manifest '{Path}' not found.", explicitPath);
            return null;
        }

        var env = Environment.GetEnvironmentVariable("MODFILE");
        if (string.IsNullOrWhiteSpace(env))
            return null;

        var envPath = Path.GetFullPath(env);
        if (!File.Exists(envPath)) {
            logger.LogWarning("Environment MODFILE='{Path}' not found.", envPath);
            return null;
        }

        return envPath;
    }
}
