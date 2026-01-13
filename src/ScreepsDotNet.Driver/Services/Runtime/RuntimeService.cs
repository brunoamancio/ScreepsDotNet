using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeService(IRuntimeSandboxPool sandboxPool) : IRuntimeService
{
    public async Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionContext context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sandbox = sandboxPool.Rent();
        try
        {
            var result = await sandbox.ExecuteAsync(context, token).ConfigureAwait(false);
            if (context.ForceColdSandbox)
                sandboxPool.Invalidate(sandbox);
            else
                sandboxPool.Return(sandbox);
            return result;
        }
        catch
        {
            sandboxPool.Invalidate(sandbox);
            throw;
        }
    }
}
