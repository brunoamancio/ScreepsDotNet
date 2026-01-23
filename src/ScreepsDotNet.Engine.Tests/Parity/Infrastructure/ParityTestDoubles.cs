namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Common.Constants;
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

}
