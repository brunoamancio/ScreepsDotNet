namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Test double implementations for processor step dependencies
/// These are minimal stub implementations that allow parity tests to run
/// without requiring full production logic for movement, combat, build/repair, and spawn mechanics
/// </summary>
internal static class ParityTestDoubles
{
    /// <summary>
    /// Stub implementation of ICreepDeathProcessor for parity testing
    /// Does NOT create tombstones or process death logic (tests focus on other mechanics)
    /// </summary>
    internal sealed class StubCreepDeathProcessor : ICreepDeathProcessor
    {
        public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger) =>
            // Minimal death processing: just remove the creep
            context.MutationWriter.Remove(creep.Id);// No tombstone creation, no energy drops, no stats tracking// This is sufficient for parity tests that don't focus on death mechanics
    }

    /// <summary>
    /// Stub implementation of ISpawnIntentParser for parity testing
    /// Always returns success with null intents (tests don't exercise spawn mechanics)
    /// </summary>
    internal sealed class StubSpawnIntentParser : ISpawnIntentParser
    {
        public SpawnIntentParseResult Parse(SpawnIntentEnvelope? envelope)
        {
            // Minimal parsing: extract intents from envelope without validation
            // Real implementation validates body parts, names, etc.
            if (envelope is null)
                return SpawnIntentParseResult.CreateSuccess();

            var recycleIntent = envelope.RecycleCreep is not null
                ? new ParsedRecycleIntent(envelope.RecycleCreep.TargetId)
                : null;

            var renewIntent = envelope.RenewCreep is not null
                ? new ParsedRenewIntent(envelope.RenewCreep.TargetId)
                : null;

            var result = SpawnIntentParseResult.CreateSuccess(
                create: null,
                renew: renewIntent,
                recycle: recycleIntent,
                directions: null,
                cancel: envelope.CancelSpawning
            );
            return result;
        }
    }

    /// <summary>
    /// Stub implementation of ISpawnStateReader for parity testing
    /// Returns empty state for all spawn checks (tests don't exercise spawn mechanics)
    /// </summary>
    internal sealed class StubSpawnStateReader : ISpawnStateReader
    {
        public SpawnRuntimeState GetState(RoomState state, RoomObjectSnapshot spawn)
        {
            // Return empty state: no spawning in progress
            var emptyState = new SpawnRuntimeState(
                spawn,
                Spawning: null,
                PendingCreep: null,
                RemainingTime: null
            );
            return emptyState;
        }
    }

    /// <summary>
    /// Stub implementation of ISpawnEnergyCharger for parity testing
    /// Validates spawn has sufficient energy but does NOT pull from extensions/containers (simplified logic)
    /// Full production logic (extension pulling, structure searching) tested separately in unit tests
    /// </summary>
    internal sealed class StubSpawnEnergyCharger : ISpawnEnergyCharger
    {
        public EnergyChargeResult TryCharge(RoomProcessorContext context, RoomObjectSnapshot spawn, int requiredEnergy, IReadOnlyList<string>? preferredStructureIds, Dictionary<string, int> energyLedger)
        {
            // Check spawn's own energy (simplified: don't pull from extensions like production logic)
            var spawnEnergy = spawn.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

            if (spawnEnergy < requiredEnergy)
                return EnergyChargeResult.Failure("Insufficient energy");

            // Track energy deduction for mutation accuracy
            energyLedger[spawn.Id] = requiredEnergy;

            return EnergyChargeResult.SuccessResult;
        }
    }

    /// <summary>
    /// Stub implementation of IStructureBlueprintProvider for parity testing
    /// Returns null for all structure types (tests don't exercise build completion)
    /// </summary>
    internal sealed class StubStructureBlueprintProvider : IStructureBlueprintProvider
    {
        public bool TryGet(string? type, out StructureBlueprint? blueprint)
        {
            blueprint = null;
            return false;
        }

        public StructureBlueprint GetRequired(string type)
            => throw new NotImplementedException("StubStructureBlueprintProvider.GetRequired not implemented for parity tests");

        public IReadOnlyDictionary<string, StructureBlueprint> GetAll()
            => new Dictionary<string, StructureBlueprint>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Stub implementation of IStructureSnapshotFactory for parity testing
    /// Returns minimal snapshot (tests don't exercise structure creation)
    /// </summary>
    internal sealed class StubStructureSnapshotFactory : IStructureSnapshotFactory
    {
        public RoomObjectSnapshot CreateStructureSnapshot(StructureBlueprint blueprint, StructureCreationOptions options)
        {
            // Return minimal snapshot - just enough to not crash
            var snapshot = new RoomObjectSnapshot(
                Guid.NewGuid().ToString("N"),
                blueprint.Type,
                options.RoomName,
                options.Shard,
                options.UserId,
                options.X,
                options.Y,
                Hits: blueprint.Hits.HitsMax,
                HitsMax: blueprint.Hits.HitsMax,
                Fatigue: null,
                TicksToLive: null,
                Name: options.Name,
                Level: null,
                Density: null,
                MineralType: null,
                DepositType: null,
                StructureType: blueprint.Type,
                Store: new Dictionary<string, int>(StringComparer.Ordinal),
                StoreCapacity: null,
                StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
                Reservation: null,
                Sign: null,
                Structure: null,
                Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
                Body: [],
                Spawning: null
            );

            return snapshot;
        }
    }
}
