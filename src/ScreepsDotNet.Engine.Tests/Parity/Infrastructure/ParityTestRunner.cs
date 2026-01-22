namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Executes room processing for parity testing (simplified proof-of-concept for Phase 2)
/// Phase 3+ will expand to include all processor steps with proper dependency injection
/// </summary>
public static class ParityTestRunner
{
    public static async Task<ParityTestOutput> RunAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        var mutationWriter = new CapturingMutationWriter();
        var statsSink = new CapturingStatsSink();
        var globalWriter = new NullGlobalMutationWriter();

        var context = new RoomProcessorContext(state, mutationWriter, statsSink, globalWriter);

        // Phase 2: Run minimal processor steps (harvest only for proof-of-concept)
        // Phase 3+: Will add full processor pipeline with proper DI
        var steps = BuildMinimalProcessorSteps();

        // Execute all steps
        foreach (var step in steps)
        {
            await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        // Phase 2: Return mutations without computing final state (Phase 3+ will add)
        return new ParityTestOutput(
            MutationWriter: mutationWriter,
            StatsSink: statsSink,
            FinalState: new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        );
    }

    private static List<IRoomProcessorStep> BuildMinimalProcessorSteps()
    {
        var resourceDropHelper = new ResourceDropHelper();

        var steps = new List<IRoomProcessorStep>
        {
            // Phase 2: Harvest only (proof-of-concept)
            new HarvestIntentStep(resourceDropHelper)

            // Phase 3+: Add remaining processor steps with proper DI:
            // - MovementIntentStep (needs ICreepDeathProcessor)
            // - CreepBuildRepairStep (needs IStructureBlueprintProvider, IStructureSnapshotFactory)
            // - CombatResolutionStep (needs ICreepDeathProcessor)
            // - ResourceTransferIntentStep
            // - ControllerIntentStep
            // - LabIntentStep
            // - LinkIntentStep
            // - NukerIntentStep
            // - PowerSpawnIntentStep
            // - SpawnIntentStep (needs multiple dependencies)
            // - TowerIntentStep (needs ICreepDeathProcessor)
            // - FactoryIntentStep
            // - KeeperLairStep
            // - NukeLandingStep
            // - PassiveRegenerationStep
            // - DecayStep
            // - TtlStep
        };

        return steps;
    }
}

/// <summary>
/// Output from parity test run
/// </summary>
public sealed record ParityTestOutput(
    CapturingMutationWriter MutationWriter,
    CapturingStatsSink StatsSink,
    Dictionary<string, RoomObjectSnapshot> FinalState
);

/// <summary>
/// Null implementation of global mutation writer for parity tests
/// </summary>
sealed file class NullGlobalMutationWriter : IGlobalMutationWriter
{
    public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch) { }
    public void RemovePowerCreep(string powerCreepId) { }
    public void UpsertPowerCreep(PowerCreepSnapshot snapshot) { }
    public void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard) { }
    public void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard) { }
    public void RemoveMarketOrder(string orderId, bool isInterShard) { }
    public void AdjustUserMoney(string userId, double newBalance) { }
    public void InsertUserMoneyLog(UserMoneyLogEntry entry) { }
    public void UpsertRoomObject(RoomObjectSnapshot snapshot) { }
    public void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch) { }
    public void RemoveRoomObject(string objectId) { }
    public void InsertTransaction(TransactionLogEntry entry) { }
    public void AdjustUserResource(string userId, string resourceType, int newBalance) { }
    public void InsertUserResourceLog(UserResourceLogEntry entry) { }
    public void IncrementUserGcl(string userId, int amount) { }
    public void IncrementUserPower(string userId, double amount) { }
    public void DecrementUserPower(string userId, double amount) { }
    public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
    public void Reset() { }
}
