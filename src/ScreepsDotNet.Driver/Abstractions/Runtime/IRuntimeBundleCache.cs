namespace ScreepsDotNet.Driver.Abstractions.Runtime;

public interface IRuntimeBundleCache
{
    RuntimeBundleSnapshot GetOrAdd(string codeHash, IReadOnlyDictionary<string, string> normalizedModules);
}

public sealed record RuntimeBundleSnapshot(IReadOnlyDictionary<string, string> Modules, string Script);
