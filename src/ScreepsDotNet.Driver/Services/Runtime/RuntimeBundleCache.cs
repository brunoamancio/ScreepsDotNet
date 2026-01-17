using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeBundleCache : IRuntimeBundleCache
{
    private readonly ConcurrentDictionary<string, RuntimeBundleSnapshot> _cache = new(StringComparer.Ordinal);

    public RuntimeBundleSnapshot GetOrAdd(string codeHash, IReadOnlyDictionary<string, string> normalizedModules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeHash);
        ArgumentNullException.ThrowIfNull(normalizedModules);

        return _cache.GetOrAdd(codeHash, _ => {
            var modulesCopy = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(normalizedModules, StringComparer.Ordinal));
            var script = RuntimeModuleBuilder.BuildBundle(modulesCopy);
            return new RuntimeBundleSnapshot(modulesCopy, script);
        });
    }
}
