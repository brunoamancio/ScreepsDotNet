using System.Collections.Concurrent;
using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeSandboxPool(IRuntimeSandboxFactory factory) : IRuntimeSandboxPool
{
    private readonly IRuntimeSandboxFactory _factory = factory;
    private readonly ConcurrentBag<IRuntimeSandbox> _pool = new();

    public IRuntimeSandbox Rent()
    {
        if (_pool.TryTake(out var sandbox))
            return sandbox;
        return _factory.CreateSandbox();
    }

    public void Return(IRuntimeSandbox sandbox)
    {
        if (sandbox is null)
            return;
        _pool.Add(sandbox);
    }
}
