namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Core.Models.Bots;
using ScreepsDotNet.Backend.Core.Models.Mods;
using ScreepsDotNet.Backend.Core.Services;

public sealed class FileSystemBotDefinitionProvider(IModManifestProvider manifestProvider,
                                                    ILogger<FileSystemBotDefinitionProvider> logger)
    : IBotDefinitionProvider
{
    private readonly IModManifestProvider _manifestProvider = manifestProvider;
    private readonly ILogger<FileSystemBotDefinitionProvider> _logger = logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IReadOnlyDictionary<string, BotDefinition> _cache = new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase);
    private string? _cachedManifestPath;
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
        var manifest = await _manifestProvider.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (!NeedsReload(manifest))
            return;

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            manifest = await _manifestProvider.GetManifestAsync(cancellationToken).ConfigureAwait(false);
            if (!NeedsReload(manifest))
                return;

            var definitions = BuildDefinitions(manifest);
            _cache = definitions;
            _cachedManifestPath = manifest.SourcePath;
            _cacheTimestamp = manifest.LastModifiedUtc;
        }
        finally {
            _refreshLock.Release();
        }
    }

    private bool NeedsReload(ModManifest manifest)
    {
        if (!string.Equals(_cachedManifestPath, manifest.SourcePath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (manifest.LastModifiedUtc > _cacheTimestamp)
            return true;

        if (manifest.SourcePath is null && _cache.Count != 0)
            return true;

        return false;
    }

    private IReadOnlyDictionary<string, BotDefinition> BuildDefinitions(ModManifest manifest)
    {
        if (manifest.SourcePath is null || manifest.Bots.Count == 0) {
            if (manifest.SourcePath is not null)
                _logger.LogWarning("Mods manifest '{Path}' does not define any bots.", manifest.SourcePath);
            return new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase);
        }

        var manifestDirectory = Path.GetDirectoryName(manifest.SourcePath);
        if (string.IsNullOrEmpty(manifestDirectory))
            return new Dictionary<string, BotDefinition>(StringComparer.OrdinalIgnoreCase);

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
                _logger.LogError(ex, "Failed to load bot \"{BotName}\" from {Path}.", botName, botEntry.Value);
            }
        });

        return definitions;
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

}
