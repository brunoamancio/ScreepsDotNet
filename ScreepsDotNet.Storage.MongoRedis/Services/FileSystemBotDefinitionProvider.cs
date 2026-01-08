namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Services;

public sealed class FileSystemBotDefinitionProvider(IOptions<BotManifestOptions> options, ILogger<FileSystemBotDefinitionProvider> logger) : IBotDefinitionProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly BotManifestOptions _options = options.Value;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyDictionary<string, BotDefinition> _cache = new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset _cacheTimestamp = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<BotDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCacheAsync(cancellationToken).ConfigureAwait(false);
        return _cache.Values.ToList();
    }

    public async Task<BotDefinition?> FindDefinitionAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureCacheAsync(cancellationToken).ConfigureAwait(false);
        return _cache.TryGetValue(name, out var definition) ? definition : null;
    }

    private async Task EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (!NeedsReload())
            return;

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (!NeedsReload())
                return;

            var manifestPath = ResolveManifestPath();
            if (manifestPath is null) {
                _cache = new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase);
                _cacheTimestamp = DateTimeOffset.UtcNow;
                return;
            }

            var manifestDirectory = Path.GetDirectoryName(manifestPath)!;
            using var stream = File.OpenRead(manifestPath);
            var manifest = await JsonSerializer.DeserializeAsync<ModsManifest>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);

            if (manifest?.Bots is not { Count: > 0 }) {
                logger.LogWarning("Mods manifest '{Manifest}' does not define any bots.", manifestPath);
                _cache = new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase);
                _cacheTimestamp = DateTimeOffset.UtcNow;
                return;
            }

            var definitions = new ConcurrentDictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase);
            Parallel.ForEach(manifest.Bots, botEntry => {
                var botName = botEntry.Key;
                try {
                    var directory = Path.GetFullPath(Path.Combine(manifestDirectory, botEntry.Value));
                    var modules = LoadModules(directory);
                    var description = $"Bot AI loaded from {directory}";
                    definitions[botName] = new BotDefinition(botName, description, modules);
                }
                catch (Exception ex) {
                    logger.LogError(ex, "Failed to load bot \"{BotName}\" from {Path}.", botName, botEntry.Value);
                }
            });

            _cache = definitions;
            _cacheTimestamp = File.GetLastWriteTimeUtc(manifestPath);
        }
        finally {
            _refreshLock.Release();
        }
    }

    private bool NeedsReload()
    {
        var manifestPath = ResolveManifestPath();
        if (manifestPath is null)
            return _cache.Count != 0;

        var timestamp = File.GetLastWriteTimeUtc(manifestPath);
        return timestamp > _cacheTimestamp;
    }

    private string? ResolveManifestPath()
    {
        if (!string.IsNullOrWhiteSpace(_options.ManifestFile)) {
            var path = Path.GetFullPath(_options.ManifestFile);
            if (File.Exists(path))
                return path;

            logger.LogWarning("Mods manifest '{Path}' not found.", path);
            return null;
        }

        var env = Environment.GetEnvironmentVariable("MODFILE");
        if (string.IsNullOrWhiteSpace(env))
            return null;

        var envPath = Path.GetFullPath(env);
        if (!File.Exists(envPath)) {
            logger.LogWarning("Environment MODFILE='{Path}' does not exist.", envPath);
            return null;
        }

        return envPath;
    }

    private static IReadOnlyDictionary<string, string> LoadModules(string directory)
    {
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"Bot directory '{directory}' does not exist.");

        var files = Directory.EnumerateFiles(directory, "*.js", SearchOption.TopDirectoryOnly).ToList();
        if (files.Count == 0)
            throw new InvalidOperationException($"Bot directory '{directory}' does not contain any .js modules.");

        var modules = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in files) {
            var moduleName = Path.GetFileNameWithoutExtension(file);
            moduleName = SanitizeModuleName(moduleName);
            if (string.IsNullOrEmpty(moduleName))
                continue;

            modules[moduleName] = File.ReadAllText(file);
        }

        if (!modules.ContainsKey("main"))
            modules["main"] = string.Empty;

        return modules;
    }

    private static string SanitizeModuleName(string moduleName)
    {
        var sanitized = moduleName.Replace(".", "$DOT$")
                                  .Replace("/", "$SLASH$")
                                  .Replace("\\", "$BACKSLASH$");
        return sanitized.Length > 0 && sanitized[0] == '$'
            ? string.Empty
            : sanitized;
    }

    private sealed record ModsManifest(Dictionary<string, string>? Bots);
}
