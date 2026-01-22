namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

/// <summary>
/// Parity tests for harvest mechanics (E2.2 - Harvest intent family)
/// Phase 2: Basic proof-of-concept with inline fixtures
/// Phase 3+: Will enhance with JSON fixture loading and Node.js comparison
/// </summary>
public sealed class HarvestParityTests
{
    [Fact]
    public async Task HarvestBasic_ExecutesSuccessfully()
    {
        // Arrange - Create simple harvest scenario
        var creep = CreateCreep("creep1", 10, 10, "user1", [BodyPartType.Work], capacity: 50);
        var source = CreateSource("source1", 11, 10, energy: 3000);
        var intents = CreateHarvestIntents("user1", creep.Id, source.Id);

        var state = CreateRoomState([creep, source], intents);

        // Act - Run .NET Engine
        var output = await ParityTestRunner.RunAsync(state, TestContext.Current.CancellationToken);

        // Assert - Basic smoke test (Phase 3 will add Node.js comparison)
        Assert.NotEmpty(output.MutationWriter.Patches);

        // Find creep patch (should have harvested energy)
        var (_, creepPayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "creep1" && p.Payload.Store is not null);
        var creepEnergy = creepPayload.Store![ResourceTypes.Energy];
        Assert.True(creepEnergy > 0, "Creep should have harvested energy");

        // Find source patch (should have lost energy)
        var (_, sourcePayload) = output.MutationWriter.Patches.First(p => p.ObjectId == "source1" && p.Payload.Energy.HasValue);
        Assert.True(sourcePayload.Energy < 3000, "Source should have lost energy");

        // Stats should be recorded
        Assert.True(output.StatsSink.EnergyHarvested.ContainsKey("user1"), "Energy harvested stat should be recorded");
    }

    private static RoomState CreateRoomState(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot intents)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);
        var state = new RoomState(
            "W1N1",
            100,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);
        return state;
    }

    private static RoomIntentSnapshot CreateHarvestIntents(string userId, string creepId, string targetId)
    {
        var argument = new IntentArgument(new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetId)
        });

        var record = new IntentRecord(IntentKeys.Harvest, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [creepId] = [record]
        };

        var envelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        var users = new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
        {
            [userId] = envelope
        };

        return new RoomIntentSnapshot("W1N1", "shard0", users);
    }

    private static RoomObjectSnapshot CreateCreep(
        string id,
        int x,
        int y,
        string userId,
        IReadOnlyList<BodyPartType> body,
        int capacity)
        => new(
            id,
            RoomObjectTypes.Creep,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: 100,
            HitsMax: 100,
            Fatigue: 0,
            TicksToLive: 1000,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: capacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: body.Select(part => new CreepBodyPartSnapshot(part, ScreepsGameConstants.BodyPartHitPoints, null)).ToArray());

    private static RoomObjectSnapshot CreateSource(string id, int x, int y, int energy)
        => new(
            id,
            RoomObjectTypes.Source,
            "W1N1",
            "shard0",
            null,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Source,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            IsSpawning: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Progress: null,
            ProgressTotal: null,
            ActionLog: null,
            Energy: energy,
            InvaderHarvested: 0);
}
