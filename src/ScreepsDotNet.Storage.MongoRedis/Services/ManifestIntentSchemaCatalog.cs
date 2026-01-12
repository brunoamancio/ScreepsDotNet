namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreepsDotNet.Backend.Core.Intents;
using ScreepsDotNet.Backend.Core.Models.Mods;
using ScreepsDotNet.Backend.Core.Services;

public sealed class ManifestIntentSchemaCatalog(IModManifestProvider manifestProvider) : IIntentSchemaCatalog
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    private IReadOnlyDictionary<string, IntentDefinition> _cache = IntentSchemas.All;
    private string? _cachedPath;
    private DateTimeOffset _cachedTimestamp = DateTimeOffset.MinValue;

    public async ValueTask<IReadOnlyDictionary<string, IntentDefinition>> GetSchemasAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await manifestProvider.GetManifestAsync(cancellationToken).ConfigureAwait(false);
        if (!NeedsReload(manifest))
            return _cache;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            manifest = await manifestProvider.GetManifestAsync(cancellationToken).ConfigureAwait(false);
            if (!NeedsReload(manifest))
                return _cache;

            _cache = BuildSchemas(manifest);
            _cachedPath = manifest.SourcePath;
            _cachedTimestamp = manifest.LastModifiedUtc;
            return _cache;
        }
        finally {
            _lock.Release();
        }
    }

    private bool NeedsReload(ModManifest manifest)
    {
        if (!string.Equals(_cachedPath, manifest.SourcePath, StringComparison.OrdinalIgnoreCase))
            return true;

        if (manifest.LastModifiedUtc > _cachedTimestamp)
            return true;

        return false;
    }

    private static IReadOnlyDictionary<string, IntentDefinition> BuildSchemas(ModManifest manifest)
    {
        if (manifest.CustomIntentTypes.Count == 0)
            return IntentSchemas.All;

        var merged = new Dictionary<string, IntentDefinition>(IntentSchemas.All, StringComparer.Ordinal);
        foreach (var entry in manifest.CustomIntentTypes)
            merged[entry.Key] = entry.Value;

        return merged;
    }
}
