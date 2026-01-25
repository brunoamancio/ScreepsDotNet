namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors.GlobalSteps;

/// <summary>
/// Runs multi-room parity tests by executing global processor steps (.NET Engine) against a GlobalState fixture.
/// Captures mutations for comparison with Node.js engine output.
/// </summary>
public static class DotNetMultiRoomParityTestRunner
{
    public static async Task<MultiRoomParityTestOutput> RunAsync(GlobalState globalState, CancellationToken cancellationToken = default)
    {
        var mutationWriter = new CapturingGlobalMutationWriter();
        var context = new GlobalProcessorContext(globalState, mutationWriter);

        // Run global processor steps
        var steps = BuildGlobalProcessorSteps();
        foreach (var step in steps) {
            await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        // Return captured mutations
        var output = new MultiRoomParityTestOutput(
            GlobalMutationWriter: mutationWriter);
        return output;
    }

    private static List<IGlobalProcessorStep> BuildGlobalProcessorSteps()
    {
        var steps = new List<IGlobalProcessorStep>
        {
            new MarketIntentStep(),
            new PowerCreepIntentStep()
            // InterRoomTransferStep omitted - requires IInterRoomTransferProcessor dependency
            // which isn't needed for Terminal.send testing (handled by MarketIntentStep)
        };
        return steps;
    }
}

public sealed record MultiRoomParityTestOutput(
    CapturingGlobalMutationWriter GlobalMutationWriter
);
