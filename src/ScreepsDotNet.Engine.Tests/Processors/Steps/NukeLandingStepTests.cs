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

public sealed class NukeLandingStepTests
{
    private readonly NukeLandingStep _step = new();

    [Fact]
    public async Task ExecuteAsync_CleanupPhase_RemovesNukeObject()
    {
        // Arrange - landTime has passed
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1000);
        var context = CreateContext([nuke], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var removal = Assert.Single(writer.Removals);
        Assert.Equal(nuke.Id, removal);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_KillsAllCreeps()
    {
        // Arrange - landTime == gameTime + 1 (landing is at landTime - 1)
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var creep1 = CreateCreep("creep1", 20, 20, "user1", hits: 100);
        var creep2 = CreateCreep("creep2", 30, 30, "user2", hits: 200);
        var context = CreateContext([nuke, creep1, creep2], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - all creeps removed
        Assert.Equal(2, writer.Removals.Count(id => id != nuke.Id));
        Assert.Contains(creep1.Id, writer.Removals);
        Assert.Contains(creep2.Id, writer.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_SetsPowerCreepHitsToZero()
    {
        // Arrange
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var powerCreep = CreatePowerCreep("pc1", 25, 25, "user1", hits: 1000);
        var context = CreateContext([nuke, powerCreep], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == powerCreep.Id);
        Assert.Equal(0, payload.Hits);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_RemovesConstructionSitesAndResources()
    {
        // Arrange
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var constructionSite = CreateConstructionSite("cs1", 20, 20);
        var energy = CreateEnergyDrop("energy1", 30, 30, amount: 100);
        var tombstone = CreateTombstone("tomb1", 22, 22);
        var ruin = CreateRuin("ruin1", 28, 28);
        var context = CreateContext([nuke, constructionSite, energy, tombstone, ruin], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(constructionSite.Id, writer.Removals);
        Assert.Contains(energy.Id, writer.Removals);
        Assert.Contains(tombstone.Id, writer.Removals);
        Assert.Contains(ruin.Id, writer.Removals);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_CancelsSpawnSpawning()
    {
        // Arrange
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var spawn = CreateSpawnWithSpawning("spawn1", 25, 25, "user1");
        var context = CreateContext([nuke, spawn], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == spawn.Id);
        Assert.Null(payload.Spawning);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_AppliesDamageAtCenter()
    {
        // Arrange - nuke at 25,25, tower at exact position (range 0)
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var tower = CreateTower("tower1", 25, 25, "user1", hits: 3000);
        var context = CreateContext([nuke, tower], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - center damage is 10,000,000 (should destroy tower)
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == tower.Id);
        var expectedHits = Math.Max(0, 3000 - ScreepsGameConstants.NukeDamageCenter);
        Assert.Equal(expectedHits, payload.Hits);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_AppliesDamageInOuterRing()
    {
        // Arrange - nuke at 25,25, tower at 27,27 (range 2)
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var tower = CreateTower("tower1", 27, 27, "user1", hits: 6_000_000);
        var context = CreateContext([nuke, tower], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - outer damage is 5,000,000
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == tower.Id);
        var expectedHits = 6_000_000 - ScreepsGameConstants.NukeDamageOuter;
        Assert.Equal(expectedHits, payload.Hits);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_RampartAbsorbsDamage()
    {
        // Arrange - nuke at 25,25, rampart and tower at same position
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var rampart = CreateRampart("rampart1", 25, 25, "user1", hits: 1_000_000);
        var tower = CreateTower("tower1", 25, 25, "user1", hits: 3000);
        var context = CreateContext([nuke, rampart, tower], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - rampart absorbs damage first, remainder passes to tower
        var (_, payload) = writer.Patches.Single(p => p.ObjectId == rampart.Id);
        var (_, towerPayload) = writer.Patches.Single(p => p.ObjectId == tower.Id);

        // Rampart takes 1,000,000 damage (destroyed)
        Assert.Equal(0, payload.Hits);

        // Tower takes remaining 9,000,000 damage (destroyed)
        Assert.Equal(0, towerPayload.Hits);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_BlocksControllerUpgrades()
    {
        // Arrange
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var controller = CreateControllerWithSafeMode("controller1", 25, 25, "user1", safeModeEndTime: 2000);
        var context = CreateContext([nuke, controller], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = writer.Patches.Single(p => p.ObjectId == controller.Id);

        // Controller upgrades blocked
        var expectedUpgradeBlockedTime = 1000 + ScreepsGameConstants.ControllerNukeBlockedUpgrade;
        Assert.Equal(expectedUpgradeBlockedTime, payload.Store![StoreKeys.UpgradeBlocked]);

        // Safe mode cancelled
        Assert.NotNull(payload.SafeMode);
        Assert.Equal(0, payload.SafeMode);
    }

    [Fact]
    public async Task ExecuteAsync_LandingPhase_CancelsSafeMode_OnlyIfActive()
    {
        // Arrange - controller WITHOUT active safe mode
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1001);
        var controller = CreateController("controller1", 25, 25, "user1");
        var context = CreateContext([nuke, controller], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = writer.Patches.Single(p => p.ObjectId == controller.Id);

        // Controller upgrades blocked
        Assert.NotNull(payload.Store);
        Assert.True(payload.Store.ContainsKey(StoreKeys.UpgradeBlocked));

        // Safe mode NOT patched (controller had no active safe mode)
        Assert.Null(payload.SafeMode);
    }

    [Fact]
    public async Task ExecuteAsync_BeforeLanding_DoesNothing()
    {
        // Arrange - gameTime < landTime - 1
        var nuke = CreateNuke("nuke1", "W2N2", 25, 25, landTime: 1010);
        var creep = CreateCreep("creep1", 25, 25, "user1", hits: 100);
        var context = CreateContext([nuke, creep], gameTime: 1000);
        var writer = (FakeMutationWriter)context.MutationWriter;

        // Act
        await _step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - nothing happens yet
        Assert.Empty(writer.Removals);
        Assert.Empty(writer.Patches);
    }

    #region Helper Methods

    private const string StoreKeys_UpgradeBlocked = "upgradeBlocked";
    private const string StoreKeys_DowngradeTimer = "downgradeTimer";

    private static class StoreKeys
    {
        public const string UpgradeBlocked = "upgradeBlocked";
        public const string DowngradeTimer = "downgradeTimer";
    }

    private static RoomObjectSnapshot CreateNuke(string id, string roomName, int x, int y, int landTime)
        => new(
            Id: id,
            Type: RoomObjectTypes.Nuke,
            RoomName: roomName,
            Shard: null,
            UserId: null,
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: "W1N1", // Launch room name
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
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: landTime);

    private static RoomObjectSnapshot CreateCreep(string id, int x, int y, string userId, int hits)
        => new(
            Id: id,
            Type: RoomObjectTypes.Creep,
            RoomName: "W2N2",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: hits,
            HitsMax: hits,
            Fatigue: 0,
            TicksToLive: 1500,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 0,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreatePowerCreep(string id, int x, int y, string userId, int hits)
        => new(
            Id: id,
            Type: RoomObjectTypes.PowerCreep,
            RoomName: "W2N2",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: hits,
            HitsMax: 1000,
            Fatigue: 0,
            TicksToLive: null,
            Name: id,
            Level: 1,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 100,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            Powers: new Dictionary<PowerTypes, PowerCreepPowerSnapshot>(),
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateConstructionSite(string id, int x, int y)
        => new(
            Id: id,
            Type: RoomObjectTypes.ConstructionSite,
            RoomName: "W2N2",
            Shard: null,
            UserId: "user1",
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Road,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: new RoomObjectStructureSnapshot(null, RoomObjectTypes.Road, "user1", null, null),
            Progress: 0,
            ProgressTotal: 300,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateEnergyDrop(string id, int x, int y, int amount)
        => new(
            Id: id,
            Type: ResourceTypes.Energy,
            RoomName: "W2N2",
            Shard: null,
            UserId: null,
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
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
            ResourceType: ResourceTypes.Energy,
            ResourceAmount: amount,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateTombstone(string id, int x, int y)
        => new(
            Id: id,
            Type: RoomObjectTypes.Tombstone,
            RoomName: "W2N2",
            Shard: null,
            UserId: "user1",
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: 5,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 0,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateRuin(string id, int x, int y)
        => new(
            Id: id,
            Type: RoomObjectTypes.Ruin,
            RoomName: "W2N2",
            Shard: null,
            UserId: "user1",
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: 500,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Storage,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: 1_000_000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: new RoomObjectStructureSnapshot(null, RoomObjectTypes.Storage, "user1", null, null),
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateSpawnWithSpawning(string id, int x, int y, string userId)
        => new(
            Id: id,
            Type: RoomObjectTypes.Spawn,
            RoomName: "W2N2",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: null,
            TicksToLive: null,
            Name: id,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Spawn,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 300 },
            StoreCapacity: 300,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Spawning: new RoomSpawnSpawningSnapshot(
                Name: "newCreep",
                NeedTime: 3,
                SpawnTime: 100,
                Directions: []),
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateTower(string id, int x, int y, string userId, int hits)
        => new(
            Id: id,
            Type: RoomObjectTypes.Tower,
            RoomName: "W2N2",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: hits,
            HitsMax: 3000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Tower,
            Store: new Dictionary<string, int>(StringComparer.Ordinal) { [ResourceTypes.Energy] = 0 },
            StoreCapacity: 1000,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateRampart(string id, int x, int y, string userId, int hits)
        => new(
            Id: id,
            Type: RoomObjectTypes.Rampart,
            RoomName: "W2N2",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: hits,
            HitsMax: 1_000_000,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: RoomObjectTypes.Rampart,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateController(string id, int x, int y, string userId)
        => new(
            Id: id,
            Type: RoomObjectTypes.Controller,
            RoomName: "W2N2",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: 5,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [StoreKeys.DowngradeTimer] = 20000
            },
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            SafeMode: null,
            SafeModeAvailable: 3,
            Progress: 10000,
            ProgressTotal: 100000,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomObjectSnapshot CreateControllerWithSafeMode(string id, int x, int y, string userId, int safeModeEndTime)
        => new(
            Id: id,
            Type: RoomObjectTypes.Controller,
            RoomName: "W2N2",
            Shard: null,
            UserId: userId,
            X: x,
            Y: y,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: 5,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [StoreKeys.DowngradeTimer] = 20000
            },
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            SafeMode: safeModeEndTime,
            SafeModeAvailable: 3,
            Progress: 10000,
            ProgressTotal: 100000,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: null);

    private static RoomProcessorContext CreateContext(IEnumerable<RoomObjectSnapshot> objects, RoomIntentSnapshot? intents = null, int gameTime = 100)
    {
        var objectMap = objects.ToDictionary(o => o.Id, o => o, StringComparer.Ordinal);

        var roomInfo = new RoomInfoSnapshot(
            RoomName: "W2N2",
            Shard: null,
            Status: null,
            IsNoviceArea: null,
            IsRespawnArea: null,
            OpenTime: null,
            OwnerUserId: "user1",
            ControllerLevel: ControllerLevel.Level5,
            EnergyAvailable: null,
            NextNpcMarketOrder: null,
            PowerBankTime: null,
            InvaderGoal: null,
            Type: RoomType.Normal);

        var state = new RoomState(
            "W2N2",
            gameTime,
            roomInfo,
            objectMap,
            new Dictionary<string, UserState>(StringComparer.Ordinal),
            intents,
            new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            []);

        return new RoomProcessorContext(state, new FakeMutationWriter(), new NullCreepStatsSink(), new NullGlobalMutationWriter());
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

    #endregion
}
