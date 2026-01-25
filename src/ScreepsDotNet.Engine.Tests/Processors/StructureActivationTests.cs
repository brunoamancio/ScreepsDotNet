namespace ScreepsDotNet.Engine.Tests.Processors;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

/// <summary>
/// Validates that owned structures (spawn, tower, lab, link, factory, nuker, power spawn)
/// check controller activation before processing intents.
/// Controller activation requires:
/// - Controller exists in room
/// - Controller level >= 1
/// - Controller owned by same user
/// - Structure allowed at current RCL
/// - Structure within distance priority limit (for multi-instance structures)
/// </summary>
public sealed class StructureActivationTests
{
    private const string UserId = "user1";
    private const string RoomName = "W1N1";
    private const string Shard = "shard0";

    [Fact]
    public async Task SpawnIntentStep_WithNoController_DoesNotProcessIntent()
    {
        // Arrange
        var spawn = CreateStructure("spawn1", RoomObjectTypes.Spawn, x: 25, y: 25, store: CreateEnergyStore(300));

        var envelope = new SpawnIntentEnvelope(
            new CreateCreepIntent("creep1", [BodyPartType.Work], [], null),
            null,
            null,
            null,
            false);

        var context = CreateContext([spawn], CreateSpawnIntents(spawn.Id, envelope), includeController: false);
        var writer = (FakeMutationWriter)context.MutationWriter;

        var bodyHelper = new BodyAnalysisHelper();
        var parser = new SpawnIntentParser(bodyHelper);
        var stateReader = new SpawnStateReader();
        var energyAllocator = new SpawnEnergyAllocator();
        var energyCharger = new SpawnEnergyCharger(energyAllocator);
        var deathProcessor = new CreepDeathProcessor();
        var dropHelper = new ResourceDropHelper();
        var step = new SpawnIntentStep(parser, stateReader, energyCharger, deathProcessor, dropHelper);

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no patches emitted (spawn not active without controller)
        Assert.Empty(writer.Patches);
        Assert.Empty(writer.Upserts);
    }

    [Fact]
    public async Task LinkIntentStep_WithNoController_DoesNotProcessIntent()
    {
        // Arrange
        var link = CreateStructure("link1", RoomObjectTypes.Link, x: 25, y: 25, store: CreateEnergyStore(800));
        var targetLink = CreateStructure("link2", RoomObjectTypes.Link, x: 30, y: 30, store: CreateEnergyStore(0));

        var context = CreateContext([link, targetLink], CreateLinkIntents(link.Id, targetLink.Id), includeController: false);
        var writer = (FakeMutationWriter)context.MutationWriter;

        var step = new LinkIntentStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no patches emitted
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task TowerIntentStep_WithNoController_DoesNotProcessIntent()
    {
        // Arrange
        var tower = CreateStructure("tower1", RoomObjectTypes.Tower, x: 25, y: 25, store: CreateEnergyStore(1000));
        var target = CreateCreep("creep1", x: 30, y: 30, userId: "user2");

        var context = CreateContext([tower, target], CreateTowerIntents(tower.Id, target.Id), includeController: false);
        var writer = (FakeMutationWriter)context.MutationWriter;

        var deathProcessor = new CreepDeathProcessor();
        var step = new TowerIntentStep(deathProcessor);

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no patches emitted
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task LabIntentStep_WithNoController_DoesNotProcessIntent()
    {
        // Arrange
        var lab = CreateStructure("lab1", RoomObjectTypes.Lab, x: 25, y: 25, store: new Dictionary<string, int>
        {
            [ResourceTypes.CatalyzedGhodiumAcid] = 3000,
            [RoomDocumentFields.RoomObject.Store.Energy] = 2000
        });
        var creep = CreateCreep("creep1", x: 26, y: 25);

        var context = CreateContext([lab, creep], CreateLabIntents(lab.Id, creep.Id), includeController: false);
        var writer = (FakeMutationWriter)context.MutationWriter;

        var step = new LabIntentStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no patches emitted
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task NukerIntentStep_WithNoController_DoesNotProcessIntent()
    {
        // Arrange
        var nuker = CreateStructure("nuker1", RoomObjectTypes.Nuker, x: 25, y: 25, store: new Dictionary<string, int>
        {
            [ResourceTypes.Energy] = ScreepsGameConstants.NukerEnergyCapacity,
            [ResourceTypes.Ghodium] = ScreepsGameConstants.NukerGhodiumCapacity
        });

        var context = CreateContext([nuker], CreateNukerIntents(nuker.Id), includeController: false);
        var writer = (FakeMutationWriter)context.MutationWriter;

        var step = new NukerIntentStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no patches emitted
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task FactoryIntentStep_WithNoController_DoesNotProcessIntent()
    {
        // Arrange
        var factory = CreateStructure("factory1", RoomObjectTypes.Factory, x: 25, y: 25, level: 5, store: new Dictionary<string, int>
        {
            [ResourceTypes.Battery] = 50,
            [ResourceTypes.Energy] = 50
        });

        var context = CreateContext([factory], CreateFactoryIntents(factory.Id), includeController: false);
        var writer = (FakeMutationWriter)context.MutationWriter;

        var step = new FactoryIntentStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no patches emitted
        Assert.Empty(writer.Patches);
    }

    private static RoomProcessorContext CreateContext(
        IEnumerable<RoomObjectSnapshot> objects,
        RoomIntentSnapshot intents,
        int gameTime = 200,
        bool includeController = true,
        int controllerLevel = 8)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        if (includeController) {
            var controller = CreateController("controller1", controllerLevel, x: 35, y: 35);
            objectMap[controller.Id] = controller;
        }

        var state = new RoomState(
            RoomName,
            gameTime,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink(), new NullGlobalMutationWriter(), new NullNotificationSink());
    }

    private static RoomObjectSnapshot CreateStructure(string id, string type, int x, int y, Dictionary<string, int>? store = null, int? level = null)
        => new(
            id,
            type,
            RoomName,
            Shard,
            UserId,
            x,
            y,
            Hits: 1000,
            HitsMax: 1000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: level,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: type,
            Store: store ?? new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 1000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null,
            Cooldown: null,
            CooldownTime: null,
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateCreep(string id, int x, int y, string? userId = null)
        => new(
            id,
            RoomObjectTypes.Creep,
            RoomName,
            Shard,
            userId ?? UserId,
            x,
            y,
            Hits: 100,
            HitsMax: 100,
            Fatigue: 0,
            TicksToLive: 1500,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: CreateEnergyStore(50),
            StoreCapacity: 50,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null);

    private static RoomObjectSnapshot CreateController(string id, int level, int x, int y)
        => new(
            id,
            RoomObjectTypes.Controller,
            RoomName,
            Shard,
            UserId,
            x,
            y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: level,
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
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Spawning: null,
            IsSpawning: null,
            UserSummoned: null,
            StrongholdId: null,
            DeathTime: null,
            DecayTime: null,
            CreepId: null,
            CreepName: null,
            CreepTicksToLive: null,
            CreepSaying: null,
            ResourceType: null,
            ResourceAmount: null);

    private static Dictionary<string, int> CreateEnergyStore(int amount)
        => new(StringComparer.Ordinal)
        {
            [RoomDocumentFields.RoomObject.Store.Energy] = amount
        };

    private static RoomIntentSnapshot CreateSpawnIntents(string spawnId, SpawnIntentEnvelope envelope, string userId = UserId)
    {
        var spawnIntents = new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal)
        {
            [spawnId] = envelope
        };

        var intentEnvelope = new IntentEnvelope(
            userId,
            new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal),
            spawnIntents,
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            RoomName,
            Shard,
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = intentEnvelope
            });
    }

    private static RoomIntentSnapshot CreateLinkIntents(string linkId, string targetLinkId, string userId = UserId)
    {
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [linkId] = [
                new IntentRecord(
                    IntentKeys.TransferEnergy,
                    [new IntentArgument(
                        new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                        {
                            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetLinkId),
                            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: 100)
                        })])
            ]
        };

        var intentEnvelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            RoomName,
            Shard,
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = intentEnvelope
            });
    }

    private static RoomIntentSnapshot CreateTowerIntents(string towerId, string targetId, string userId = UserId)
    {
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [towerId] = [
                new IntentRecord(
                    "attack",
                    [new IntentArgument(
                        new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                        {
                            ["id"] = new(IntentFieldValueKind.Text, TextValue: targetId)
                        })])
            ]
        };

        var intentEnvelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            RoomName,
            Shard,
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = intentEnvelope
            });
    }

    private static RoomIntentSnapshot CreateLabIntents(string labId, string creepId, string userId = UserId)
    {
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [labId] = [
                new IntentRecord(
                    IntentKeys.BoostCreep,
                    [new IntentArgument(
                        new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                        {
                            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: creepId)
                        })])
            ]
        };

        var intentEnvelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            RoomName,
            Shard,
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = intentEnvelope
            });
    }

    private static RoomIntentSnapshot CreateNukerIntents(string nukerId, string userId = UserId)
    {
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [nukerId] = [
                new IntentRecord(
                    IntentKeys.LaunchNuke,
                    [new IntentArgument(
                        new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                        {
                            [NukerIntentFields.RoomName] = new(IntentFieldValueKind.Text, TextValue: "W2N1"),
                            [NukerIntentFields.X] = new(IntentFieldValueKind.Number, NumberValue: 25),
                            [NukerIntentFields.Y] = new(IntentFieldValueKind.Number, NumberValue: 25)
                        })])
            ]
        };

        var intentEnvelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            RoomName,
            Shard,
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = intentEnvelope
            });
    }

    private static RoomIntentSnapshot CreateFactoryIntents(string factoryId, string userId = UserId)
    {
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [factoryId] = [
                new IntentRecord(
                    IntentKeys.Produce,
                    [new IntentArgument(
                        new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
                        {
                            [IntentKeys.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Cell)
                        })])
            ]
        };

        var intentEnvelope = new IntentEnvelope(
            userId,
            objectIntents,
            new Dictionary<string, SpawnIntentEnvelope>(StringComparer.Ordinal),
            new Dictionary<string, CreepIntentEnvelope>(StringComparer.Ordinal));

        return new RoomIntentSnapshot(
            RoomName,
            Shard,
            new Dictionary<string, IntentEnvelope>(StringComparer.Ordinal)
            {
                [userId] = intentEnvelope
            });
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<string> Removals { get; } = [];
        public List<RoomObjectSnapshot> Upserts { get; } = [];

        public void Upsert(RoomObjectSnapshot document)
            => Upserts.Add(document);

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

        public int GetMutationCount() => 0;

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }

        public void Reset() { }
    }
}
