using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeService(IRuntimeSandboxPool sandboxPool) : IRuntimeService
{
    private readonly IRuntimeSandboxPool _sandboxPool = sandboxPool;

    public async Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionContext context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sandbox = _sandboxPool.Rent();
        try
        {
            var result = await sandbox.ExecuteAsync(context, token).ConfigureAwait(false);
            _sandboxPool.Return(sandbox);
            return result;
        }
        catch
        {
            _sandboxPool.Invalidate(sandbox);
            throw;
        }
    }
}
