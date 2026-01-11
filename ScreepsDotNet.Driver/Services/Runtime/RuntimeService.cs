using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal sealed class RuntimeService(IRuntimeSandboxFactory sandboxFactory) : IRuntimeService
{
    private readonly IRuntimeSandboxFactory _sandboxFactory = sandboxFactory;

    public Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionContext context, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var sandbox = _sandboxFactory.CreateSandbox();
        return sandbox.ExecuteAsync(context, token);
    }
}
