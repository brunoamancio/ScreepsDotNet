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

        // Phase 3: Run full processor pipeline (limited to steps that don't need ICreepDeathProcessor)
        var steps = BuildProcessorSteps();

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

    private static List<IRoomProcessorStep> BuildProcessorSteps()
    {
        // Phase 3: Expanded processor pipeline (all steps that don't need complex dependencies)
        // Deferred: Steps needing ICreepDeathProcessor, IStructureBlueprintProvider, etc.
        // Will be added in Phase 4+ when test doubles are implemented.

        var resourceDropHelper = new ResourceDropHelper();

        var steps = new List<IRoomProcessorStep>
        {
            // Movement - Deferred (needs ICreepDeathProcessor)
            // Build/Repair - Deferred (needs IStructureBlueprintProvider, IStructureSnapshotFactory)
            // Combat - Deferred (needs ICreepDeathProcessor)

            // Harvest ✅
            new HarvestIntentStep(resourceDropHelper),

            // Resource Transfer ✅
            new ResourceTransferIntentStep(resourceDropHelper),

            // Controller ✅
            new ControllerIntentStep(),

            // Lab ✅
            new LabIntentStep(),

            // Link ✅
            new LinkIntentStep(),

            // Nuker ✅
            new NukerIntentStep(),

            // Power Spawn ✅
            new PowerSpawnIntentStep(),

            // Spawn - Deferred (needs ISpawnIntentParser, ISpawnStateReader, ISpawnEnergyCharger, ICreepDeathProcessor)
            // Tower - Deferred (needs ICreepDeathProcessor)

            // Factory ✅
            new FactoryIntentStep(),

            // Observer - Not implemented yet (E2 deferred)

            // Power Ability ✅
            new PowerAbilityStep(),

            // Power Ability Cooldown ✅
            new PowerAbilityCooldownStep(),

            // Power Effect Decay ✅
            new PowerEffectDecayStep(),

            // Keeper Lair ✅ (no dependencies)
            new KeeperLairStep(),

            // Nuke Landing ✅
            new NukeLandingStep(),

            // Source Regeneration ✅
            new SourceRegenerationStep(),

            // Mineral Regeneration ✅
            new MineralRegenerationStep(),

            // Controller Downgrade ✅
            new ControllerDowngradeStep(),

            // Structure Decay ✅
            new StructureDecayStep(),

            // Creep Lifecycle (TTL/Death) - Deferred (needs ICreepDeathProcessor)
            // Spawn Spawning - Deferred (needs ISpawnStateReader, ICreepDeathProcessor)
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
