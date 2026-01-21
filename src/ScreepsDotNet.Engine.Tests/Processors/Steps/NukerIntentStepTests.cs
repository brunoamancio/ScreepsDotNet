namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;

public sealed class NukerIntentStepTests
{
    private readonly NukerIntentStep _step = new();

    [Fact]
    public async Task ExecuteAsync_ValidLaunch_CreatesNukeObject()
    {
        // Arrange
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 5_000, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W2N2", 30, 30), gameTime: 200);
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var nukeUpsert = Assert.Single(globalWriter.RoomObjectUpserts);
        Assert.Equal(RoomObjectTypes.Nuke, nukeUpsert.Type);
        Assert.Equal("W2N2", nukeUpsert.RoomName);
        Assert.Equal(30, nukeUpsert.X);
        Assert.Equal(30, nukeUpsert.Y);
        Assert.Equal("W1N1", nukeUpsert.Name); // launchRoomName stored in Name field

        var landTime = nukeUpsert.NextRegenerationTime;
        Assert.NotNull(landTime);
        Assert.Equal(200 + ScreepsGameConstants.NukeLandTime, landTime);
    }

    [Fact]
    public async Task ExecuteAsync_ValidLaunch_ConsumesResources()
    {
        // Arrange
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 5_000, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W2N2", 30, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = Assert.Single(writer.Patches);
        Assert.Equal(nuker.Id, objectId);
        Assert.Equal(0, payload.Store![ResourceTypes.Energy]);
        Assert.Equal(0, payload.Store![ResourceTypes.Ghodium]);
    }

    [Fact]
    public async Task ExecuteAsync_ValidLaunch_SetsCooldown()
    {
        // Arrange
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 5_000, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W2N2", 30, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(writer.Patches);
        Assert.Equal(200 + ScreepsGameConstants.NukerCooldown, payload.CooldownTime);
    }

    [Fact]
    public async Task ExecuteAsync_InsufficientEnergy_Rejected()
    {
        // Arrange
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 299_999, ghodium: 5_000, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W2N2", 30, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        Assert.Empty(globalWriter.RoomObjectUpserts);
    }

    [Fact]
    public async Task ExecuteAsync_InsufficientGhodium_Rejected()
    {
        // Arrange
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 4_999, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W2N2", 30, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        Assert.Empty(globalWriter.RoomObjectUpserts);
    }

    [Fact]
    public async Task ExecuteAsync_CooldownActive_Rejected()
    {
        // Arrange
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 5_000, cooldownTime: 300);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W2N2", 30, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        Assert.Empty(globalWriter.RoomObjectUpserts);
    }

    [Fact]
    public async Task ExecuteAsync_OutOfRange_Rejected()
    {
        // Arrange - nuker at W1N1, target at W12N1 (11 rooms away horizontally, range limit is 10)
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 5_000, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W12N1", 30, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        Assert.Empty(globalWriter.RoomObjectUpserts);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRoomName_Rejected()
    {
        // Arrange
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 5_000, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "InvalidRoom", 30, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        Assert.Empty(globalWriter.RoomObjectUpserts);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCoordinates_Rejected()
    {
        // Arrange - coordinates outside 0-49 range
        var nuker = CreateNuker("nuker1", 25, 25, "user1", "W1N1", energy: 300_000, ghodium: 5_000, cooldownTime: 100);
        var context = CreateContext([nuker],
            CreateLaunchNukeIntent("user1", nuker.Id, "W2N2", 50, 30), gameTime: 200);
        var writer = (FakeMutationWriter)context.MutationWriter;
        var globalWriter = (FakeGlobalMutationWriter)context.GlobalMutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
        Assert.Empty(globalWriter.RoomObjectUpserts);
    }

    #region Helper Methods

    private static RoomObjectSnapshot CreateNuker(string id, int x, int y, string userId, string roomName, int energy, int ghodium, int? cooldownTime)
        => new(
            Id: id,
            Type: RoomObjectTypes.Nuker,
            RoomName: roomName,
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: ScreepsGameConstants.NukerHits,
            HitsMax: ScreepsGameConstants.NukerHits,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Nuker,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = energy,
                [ResourceTypes.Ghodium] = ghodium
            },
            StoreCapacity: ScreepsGameConstants.NukerEnergyCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.NukerEnergyCapacity,
                [ResourceTypes.Ghodium] = ScreepsGameConstants.NukerGhodiumCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: new RoomObjectStructureSnapshot(null, null, null, null, null),
            CooldownTime: cooldownTime,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            NextRegenerationTime: null);

    private static RoomIntentSnapshot CreateLaunchNukeIntent(string userId, string nukerId, string roomName, int x, int y)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [NukerIntentFields.RoomName] = new(IntentFieldValueKind.Text, TextValue: roomName),
            [NukerIntentFields.X] = new(IntentFieldValueKind.Number, NumberValue: x),
            [NukerIntentFields.Y] = new(IntentFieldValueKind.Number, NumberValue: y)
        };

        var argument = new IntentArgument(fields);
        var record = new IntentRecord(IntentKeys.LaunchNuke, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [nukerId] = [record]
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

        var result = new RoomIntentSnapshot("W1N1", "shard0", users);
        return result;
    }

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot? intents = null, int gameTime = 100)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        var state = new RoomState(
            "W1N1",
            gameTime,
            null,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink(), new FakeGlobalMutationWriter());
    }

    private sealed class FakeMutationWriter : IRoomMutationWriter
    {
        public List<(string ObjectId, RoomObjectPatchPayload Payload)> Patches { get; } = [];
        public List<RoomObjectSnapshot> Upserts { get; } = [];
        public List<string> Removals { get; } = [];
        public RoomInfoPatchPayload? RoomInfoPatch { get; private set; }

        public void Upsert(RoomObjectSnapshot document) => Upserts.Add(document);

        public void Patch(string objectId, RoomObjectPatchPayload patch) => Patches.Add((objectId, patch));

        public void Remove(string objectId) => Removals.Add(objectId);

        public void SetRoomInfoPatch(RoomInfoPatchPayload patch) => RoomInfoPatch = patch;

        public void SetEventLog(IRoomEventLogPayload? eventLog) { }

        public void SetMapView(IRoomMapViewPayload? mapView) { }

#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

        public void Reset()
        {
            Patches.Clear();
            Upserts.Clear();
            Removals.Clear();
        }
    }

    private sealed class FakeGlobalMutationWriter : IGlobalMutationWriter
    {
        public List<RoomObjectSnapshot> RoomObjectUpserts { get; } = [];
        public List<(string ObjectId, GlobalRoomObjectPatch Patch)> RoomObjectPatches { get; } = [];
        public List<string> RoomObjectRemovals { get; } = [];

        public void UpsertRoomObject(RoomObjectSnapshot snapshot) => RoomObjectUpserts.Add(snapshot);

        public void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch) => RoomObjectPatches.Add((objectId, patch));

        public void RemoveRoomObject(string objectId) => RoomObjectRemovals.Add(objectId);

        public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch) { }
        public void RemovePowerCreep(string powerCreepId) { }
        public void UpsertPowerCreep(PowerCreepSnapshot snapshot) { }
        public void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard) { }
        public void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard) { }
        public void RemoveMarketOrder(string orderId, bool isInterShard) { }
        public void AdjustUserMoney(string userId, double newBalance) { }
        public void InsertUserMoneyLog(UserMoneyLogEntry entry) { }
        public void AdjustUserResource(string userId, string resourceType, int newBalance) { }
        public void InsertUserResourceLog(UserResourceLogEntry entry) { }
        public void InsertTransaction(TransactionLogEntry entry) { }
        public void IncrementUserGcl(string userId, int amount) { }
        public void IncrementUserPower(string userId, double amount) { }
        public void DecrementUserPower(string userId, double amount) { }
#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;
        public void Reset()
        {
            RoomObjectUpserts.Clear();
            RoomObjectPatches.Clear();
            RoomObjectRemovals.Clear();
        }
    }

    #endregion
}
