using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeSandboxPool(IRuntimeSandboxFactory factory, ILogger<RuntimeSandboxPool>? logger = null) : IRuntimeSandboxPool
{
    private readonly IRuntimeSandboxFactory _factory = factory;
    private readonly ILogger<RuntimeSandboxPool>? _logger = logger;
    private readonly ConcurrentBag<IRuntimeSandbox> _pool = new();

    public IRuntimeSandbox Rent()
    {
        if (_pool.TryTake(out var sandbox))
            return sandbox;
        _logger?.LogTrace("Allocating new runtime sandbox.");
        return _factory.CreateSandbox();
    }

    public void Return(IRuntimeSandbox sandbox)
    {
        if (sandbox is null)
            return;
        _pool.Add(sandbox);
    }

    public void Invalidate(IRuntimeSandbox sandbox)
    {
        if (sandbox is null)
            return;

        if (sandbox is IDisposable disposable)
        {
            try
            {
                disposable.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to dispose sandbox during invalidation.");
            }
        }
        _logger?.LogDebug("Sandbox invalidated and removed from pool.");
    }
}
