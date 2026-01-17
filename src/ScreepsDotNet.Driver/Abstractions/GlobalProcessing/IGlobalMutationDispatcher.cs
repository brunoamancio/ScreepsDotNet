namespace ScreepsDotNet.Driver.Abstractions.GlobalProcessing;

using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Applies global-level mutations (power creeps, market, etc.) staged by the engine.
/// </summary>
public interface IGlobalMutationDispatcher
{
    Task ApplyAsync(GlobalMutationBatch batch, CancellationToken token = default);
}
