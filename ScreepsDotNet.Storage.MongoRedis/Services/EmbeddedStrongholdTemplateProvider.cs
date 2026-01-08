namespace ScreepsDotNet.Storage.MongoRedis.Services;

using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Models.Strongholds;
using ScreepsDotNet.Backend.Core.Services;

public sealed class EmbeddedStrongholdTemplateProvider : IStrongholdTemplateProvider
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyDictionary<string, StrongholdTemplate> _templates = new Dictionary<string, StrongholdTemplate>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _depositTypes = Array.Empty<string>();
    private bool _initialized;

    public async Task<IReadOnlyList<StrongholdTemplate>> GetTemplatesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _templates.Values.ToList();
    }

    public async Task<StrongholdTemplate?> FindTemplateAsync(string name, CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _templates.TryGetValue(name, out var template) ? template : null;
    }

    public async Task<IReadOnlyList<string>> GetDepositTypesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
        return _depositTypes;
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
            return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try {
            if (_initialized)
                return;

            using var stream = GetResourceStream();
            var document = await JsonSerializer.DeserializeAsync<StrongholdDataDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                            ?? throw new InvalidOperationException("Stronghold data resource is missing or invalid.");

            var templateMap = new ConcurrentDictionary<string, StrongholdTemplate>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.Templates) {
                var structures = entry.Value.Structures.Select(structure => new StrongholdStructureBlueprint(structure.Type.ToStructureType(),
                                                                                                             structure.Dx,
                                                                                                             structure.Dy,
                                                                                                             structure.Level,
                                                                                                             structure.StrongholdBehavior))
                                                       .ToList();
                templateMap[entry.Key] = new StrongholdTemplate(entry.Key, entry.Value.Description, entry.Value.RewardLevel, structures);
            }

            _templates = templateMap;
            _depositTypes = document.CoreRewards.Keys.ToList();
            _initialized = true;
        }
        finally {
            _lock.Release();
        }
    }

    private static Stream GetResourceStream()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
                                   .FirstOrDefault(name => name.EndsWith("StrongholdsData.json", StringComparison.Ordinal));
        return resourceName is null
            ? throw new InvalidOperationException("Embedded stronghold data resource not found.")
            : assembly.GetManifestResourceStream(resourceName)
              ?? throw new InvalidOperationException("Unable to open stronghold data resource stream.");
    }

    private sealed record StrongholdDataDocument(
        Dictionary<string, StrongholdTemplateDocument> Templates,
        Dictionary<string, object> CoreRewards);

    private sealed record StrongholdTemplateDocument(
        string Description,
        int RewardLevel,
        List<StrongholdStructureDocument> Structures);

    private sealed record StrongholdStructureDocument(
        string Type,
        int Dx,
        int Dy,
        int? Level,
        string? StrongholdBehavior);
}
