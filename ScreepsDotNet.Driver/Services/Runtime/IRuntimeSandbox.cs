using ScreepsDotNet.Driver.Abstractions.Runtime;

namespace ScreepsDotNet.Driver.Services.Runtime;

internal interface IRuntimeSandbox
{
    Task<RuntimeExecutionResult> ExecuteAsync(RuntimeExecutionContext context, CancellationToken token = default);
}
