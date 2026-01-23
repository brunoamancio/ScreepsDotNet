namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Bulk;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Processors.Helpers;

public sealed class LinkIntentStepTests
{
    private readonly LinkIntentStep _step = new();

    [Fact]
    public async Task BasicTransfer_TransfersEnergy()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 400);
        var targetLink = CreateLink("link2", 15, 15, "user1", energy: 200);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 100), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = writer.Patches.Single(p => p.ObjectId == sourceLink.Id);
        Assert.Equal(sourceLink.Id, objectId);
        Assert.Equal(300, payload.Store![ResourceTypes.Energy]);
        var expectedCooldown = ScreepsGameConstants.LinkCooldown * Math.Max(Math.Abs(15 - 10), Math.Abs(15 - 10));
        Assert.Equal(expectedCooldown, payload.Cooldown);  // Countdown ticker, not absolute time

        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == targetLink.Id);
        var transferredWithLoss = 100 - (int)Math.Ceiling(100 * ScreepsGameConstants.LinkLossRatio);
        Assert.Equal(200 + transferredWithLoss, Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task TransferWithLoss_AppliesLossRatio()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 1000);
        var targetLink = CreateLink("link2", 15, 15, "user1", energy: 0);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 100), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == targetLink.Id);
        var loss = (int)Math.Ceiling(100 * ScreepsGameConstants.LinkLossRatio);
        var expectedTarget = 100 - loss;
        Assert.Equal(expectedTarget, Payload.Store![ResourceTypes.Energy]);
        Assert.Equal(97, expectedTarget);
    }

    [Fact]
    public async Task TransferCappedByTargetCapacity_TransfersOnlyAvailableSpace()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 1000);
        var targetLink = CreateLink("link2", 15, 15, "user1", energy: 700);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 500), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == sourceLink.Id);
        Assert.Equal(900, Payload.Store![ResourceTypes.Energy]);

        var targetPatch = writer.Patches.Single(p => p.ObjectId == targetLink.Id);
        var transferredWithLoss = 100 - (int)Math.Ceiling(100 * ScreepsGameConstants.LinkLossRatio);
        Assert.Equal(700 + transferredWithLoss, targetPatch.Payload.Store![ResourceTypes.Energy]);
    }

    [Fact]
    public async Task SourceOnCooldown_DoesNothing()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 400, cooldown: 200);
        var targetLink = CreateLink("link2", 15, 15, "user1", energy: 200);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 100), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task InsufficientEnergy_DoesNothing()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 10);
        var targetLink = CreateLink("link2", 15, 15, "user1", energy: 200);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 50), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task TargetFull_DoesNothing()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 400);
        var targetLink = CreateLink("link2", 15, 15, "user1", energy: ScreepsGameConstants.LinkCapacity);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 100), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task CooldownBasedOnDistance_SetsCooldownCorrectly()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 400);
        var targetLink = CreateLink("link2", 15, 12, "user1", energy: 200);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 100), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (objectId, payload) = writer.Patches.Single(p => p.ObjectId == sourceLink.Id);
        Assert.Equal(sourceLink.Id, objectId);
        var distance = Math.Max(Math.Abs(15 - 10), Math.Abs(12 - 10));
        var expectedCooldown = ScreepsGameConstants.LinkCooldown * distance;
        Assert.Equal(expectedCooldown, payload.Cooldown);  // Countdown ticker, not absolute time
        Assert.Equal(5, distance);
    }

    [Fact]
    public async Task ActionLog_RecordsTransferTarget()
    {
        // Arrange
        var sourceLink = CreateLink("link1", 10, 10, "user1", energy: 400);
        var targetLink = CreateLink("link2", 15, 15, "user1", energy: 200);
        var context = CreateContext([sourceLink, targetLink],
            CreateTransferEnergyIntent("user1", sourceLink.Id, targetLink.Id, 100), gameTime: 100);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == sourceLink.Id);
        Assert.NotNull(Payload.ActionLog);
        Assert.NotNull(Payload.ActionLog!.TransferEnergy);
        Assert.Equal(15, Payload.ActionLog!.TransferEnergy!.X);
        Assert.Equal(15, Payload.ActionLog!.TransferEnergy!.Y);
    }

    #region Helper Methods

    private static RoomObjectSnapshot CreateLink(string id, int x, int y, string userId, int energy, int? cooldown = null)
        => new(
            id,
            RoomObjectTypes.Link,
            "W1N1",
            "shard0",
            userId,
            x,
            y,
            Hits: ScreepsGameConstants.LinkHits,
            HitsMax: ScreepsGameConstants.LinkHitsMax,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Link,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = energy
            },
            StoreCapacity: ScreepsGameConstants.LinkCapacity,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = ScreepsGameConstants.LinkCapacity
            },
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            IsSpawning: null,
            UserSummoned: null,
            IsPublic: null,
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
            MineralAmount: null,
            InvaderHarvested: null,
            Harvested: null,
            Cooldown: cooldown,
            CooldownTime: null,
            SafeMode: null,
            SafeModeAvailable: null,
            PortalDestination: null,
            Send: null);

    private static RoomIntentSnapshot CreateTransferEnergyIntent(string userId, string sourceLinkId, string targetLinkId, int amount)
    {
        var fields = new Dictionary<string, IntentFieldValue>(StringComparer.Ordinal)
        {
            [IntentKeys.TargetId] = new(IntentFieldValueKind.Text, TextValue: targetLinkId),
            [IntentKeys.Amount] = new(IntentFieldValueKind.Number, NumberValue: amount)
        };

        var argument = new IntentArgument(fields);

        var record = new IntentRecord(IntentKeys.TransferEnergy, [argument]);
        var objectIntents = new Dictionary<string, IReadOnlyList<IntentRecord>>(StringComparer.Ordinal)
        {
            [sourceLinkId] = [record]
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

        return new RoomProcessorContext(state, new FakeMutationWriter(), new FakeCreepStatsSink(), new NullGlobalMutationWriter());
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

    private sealed class FakeCreepStatsSink : ICreepStatsSink
    {
        private readonly Dictionary<string, Dictionary<string, int>> _metrics = [];

        public void IncrementEnergyControl(string userId, int amount) { }
        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyConstruction(string userId, int amount) { }
        public void IncrementEnergyHarvested(string userId, int amount) { }
#pragma warning disable CA1822 // Mark members as static
        public int GetMutationCount() => 0;
#pragma warning restore CA1822

        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }

    #endregion
}
