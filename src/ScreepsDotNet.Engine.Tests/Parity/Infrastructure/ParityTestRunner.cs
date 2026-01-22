namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Executes room processing for parity testing with full processor pipeline
/// Uses test doubles for complex dependencies (movement, combat, build/repair, spawn, lifecycle)
/// All 20 processor steps are operational for comprehensive parity validation
/// </summary>
public static class ParityTestRunner
{
    public static async Task<ParityTestOutput> RunAsync(RoomState state, CancellationToken cancellationToken = default)
    {
        var mutationWriter = new CapturingMutationWriter();
        var statsSink = new CapturingStatsSink();
        var globalWriter = new NullGlobalMutationWriter();

        var context = new RoomProcessorContext(state, mutationWriter, statsSink, globalWriter);

        // Run full processor pipeline with test doubles for complex dependencies
        var steps = BuildProcessorSteps();

        // Execute all steps
        foreach (var step in steps)
        {
            await step.ExecuteAsync(context, cancellationToken).ConfigureAwait(false);
        }

        // Return mutations and stats for parity comparison
        return new ParityTestOutput(
            MutationWriter: mutationWriter,
            StatsSink: statsSink,
            FinalState: new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal)
        );
    }

    private static List<IRoomProcessorStep> BuildProcessorSteps()
    {
        // Full processor pipeline with test doubles for complex dependencies
        // Test doubles allow all 20 steps to execute without requiring full production logic

        var resourceDropHelper = new ResourceDropHelper();
        var deathProcessor = new ParityTestDoubles.StubCreepDeathProcessor();
        var spawnIntentParser = new ParityTestDoubles.StubSpawnIntentParser();
        var spawnStateReader = new ParityTestDoubles.StubSpawnStateReader();
        var spawnEnergyCharger = new ParityTestDoubles.StubSpawnEnergyCharger();
        var blueprintProvider = new ParityTestDoubles.StubStructureBlueprintProvider();
        var structureFactory = new ParityTestDoubles.StubStructureSnapshotFactory();

        var steps = new List<IRoomProcessorStep>
        {
            // Movement ✅ (test double)
            new MovementIntentStep(deathProcessor),

            // Build/Repair ✅ (test doubles)
            new CreepBuildRepairStep(blueprintProvider, structureFactory),

            // Combat ✅ (test double)
            new CombatResolutionStep(deathProcessor),

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

            // Spawn ✅ (test doubles)
            new SpawnIntentStep(spawnIntentParser, spawnStateReader, spawnEnergyCharger, deathProcessor, resourceDropHelper),

            // Tower ✅ (test double)
            new TowerIntentStep(deathProcessor),

            // Factory ✅
            new FactoryIntentStep(),

            // Observer - Not implemented yet (E2 deferred)

            // Power Ability ✅
            new PowerAbilityStep(),

            // Power Ability Cooldown ✅
            new PowerAbilityCooldownStep(),

            // Power Effect Decay ✅
            new PowerEffectDecayStep(),

            // Keeper Lair ✅
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

            // Creep Lifecycle ✅ (test double)
            new CreepLifecycleStep(deathProcessor),

            // Spawn Spawning ✅ (test doubles)
            new SpawnSpawningStep(spawnStateReader, deathProcessor)
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
