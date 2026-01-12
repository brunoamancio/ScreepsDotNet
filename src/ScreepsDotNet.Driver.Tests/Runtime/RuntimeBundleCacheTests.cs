using ScreepsDotNet.Driver.Services.Runtime;

namespace ScreepsDotNet.Driver.Tests.Runtime;

public sealed class RuntimeBundleCacheTests
{
    [Fact]
    public void GetOrAdd_ReusesSnapshotForSameHash()
    {
        var cache = new RuntimeBundleCache();
        var modules = RuntimeModuleBuilder.NormalizeModules(new Dictionary<string, string>
        {
            ["main"] = "module.exports.loop = () => {};",
            ["helper"] = "module.exports.say = () => 'hi';"
        });
        var hash = RuntimeModuleBuilder.ComputeCodeHash(modules);

        var first = cache.GetOrAdd(hash, modules);
        var second = cache.GetOrAdd(hash, modules);

        Assert.Same(first, second);
        Assert.Equal(first.Script, second.Script);
    }
}
