namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class CombatResolutionStepTests
{
    [Fact]
    public async Task ExecuteAsync_KillsCreep_InvokesDeathProcessor()
    {
        var creep = CreateCreep("creep1", hits: 50);
        var intents = CreateIntents(creep.Id, 50);
        var context = CreateContext([creep], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var deathProcessor = new RecordingDeathProcessor();
        var step = new CombatResolutionStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Single(deathProcessor.Calls);
        Assert.Equal(creep.Id, deathProcessor.Calls[0].Creep.Id);
        Assert.True(deathProcessor.Calls[0].Options.ViolentDeath);
        Assert.Empty(writer.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_KillsStructure_RemovesDirectly()
    {
        var structure = CreateStructure("wall1", hits: 10);
        var intents = CreateIntents(structure.Id, 10);
        var context = CreateContext([structure], intents);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var deathProcessor = new RecordingDeathProcessor();
        var step = new CombatResolutionStep(deathProcessor);

        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Contains(structure.Id, writer.Removals);
        Assert.Empty(deathProcessor.Calls);
    }

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects,
                                                      RoomIntentSnapshot intents,
                                                      int gameTime = 100)
    {
        var objectMap = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        foreach (var obj in objects)
            objectMap[obj.Id] = obj;

        var state = new RoomState("W1N1",
                                  gameTime,
                                  null,
                                  objectMap,
                                  new Dictionary<string, UserState>(StringComparer.Ordinal),
                                  intents,
                                  new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
                                  []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink());
    }

    private static RoomIntentSnapshot CreateIntents(string targetId, int damage, string userId = "user1")
    {
        var creepIntents = new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal)
        {
            ["attacker"] = new(null,
                               new AttackIntent(targetId, damage),
                               null,
                               new Dictionary<string, object?>(StringComparer.Ordinal))
        };

        var envelope = new IntentEnvelope(userId,
                                          new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
                                          new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
                                          creepIntents);

        return new RoomIntentSnapshot("W1N1",
                                      "shard0",
                                      new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal) { [userId] = envelope });
    }

    private static RoomObjectSnapshot CreateCreep(string id, int hits)
        => new(id,
               RoomObjectTypes.Creep,
               "W1N1",
               "shard0",
               "user1",
               10,
               10,
               Hits: hits,
               HitsMax: hits,
               Fatigue: 0,
               TicksToLive: 100,
               Name: id,
               Level: null,
               Density: null,
               MineralType: null,
               DepositType: null,
               StructureType: null,
               Store: new Dictionary<string, int>(StringComparer.Ordinal),
               StoreCapacity: null,
               StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
               Reservation: null,
               Sign: null,
               Structure: null,
               Effects: new Dictionary<string, object?>(),
               Spawning: null,
               Body: []);

    private static RoomObjectSnapshot CreateStructure(string id, int hits)
        => new(id,
               RoomObjectTypes.Wall,
               "W1N1",
               "shard0",
               null,
               10,
               11,
               Hits: hits,
               HitsMax: hits,
               Fatigue: null,
               TicksToLive: null,
               Name: id,
               Level: null,
               Density: null,
               MineralType: null,
               DepositType: null,
               StructureType: RoomObjectTypes.Wall,
               Store: new Dictionary<string, int>(StringComparer.Ordinal),
               StoreCapacity: null,
               StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
               Reservation: null,
               Sign: null,
               Structure: null,
               Effects: new Dictionary<string, object?>(),
               Spawning: null,
               Body: []);

    private sealed class RecordingDeathProcessor : ICreepDeathProcessor
    {
        public List<(RoomObjectSnapshot Creep, CreepDeathOptions Options)> Calls { get; } = [];

        public void Process(RoomProcessorContext context, RoomObjectSnapshot creep, CreepDeathOptions options, IDictionary<string, int> energyLedger)
            => Calls.Add((creep, options));
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<string> Removals { get; } = [];

        public void Upsert(RoomObjectSnapshot document) { }

        public void Patch(string objectId, RoomObjectPatchPayload patch)
            => Patches.Add((objectId, patch));

        public void Remove(string objectId)
        {
            if (!string.IsNullOrWhiteSpace(objectId))
                Removals.Add(objectId);
        }

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) { }

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset() { }
    }
}
