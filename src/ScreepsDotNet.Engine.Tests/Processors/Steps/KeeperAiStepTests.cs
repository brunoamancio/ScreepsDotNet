using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;
using ScreepsDotNet.Engine.Processors.Steps;
using ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

namespace ScreepsDotNet.Engine.Tests.Processors.Steps;

public sealed class KeeperAiStepTests
{
    [Fact]
    public async Task ExecuteAsync_KeeperWithNoTarget_AssignsNearestSourceWithin5Tiles()
    {
        // Arrange
        var keeper = CreateKeeper("keeper1", x: 25, y: 25);
        var source1 = CreateSource("source1", x: 28, y: 25); // 3 tiles away
        var source2 = CreateSource("source2", x: 35, y: 25); // 10 tiles away (out of range)

        var (context, writer) = CreateContext(keeper, source1, source2);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - keeper assigns target (may also move and cache path)
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == keeper.Id && p.Payload.MemorySourceId is not null);
        Assert.Equal(source1.Id, Payload.MemorySourceId);
    }

    [Fact]
    public async Task ExecuteAsync_KeeperWithMemoryTarget_ReusesStoredSource()
    {
        // Arrange
        var source = CreateSource("source1", x: 28, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id);

        var (context, writer) = CreateContext(keeper, source);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - keeper should not reassign target
        var patches = writer.Patches.Where(p => p.ObjectId == keeper.Id && p.Payload.MemorySourceId is not null).ToList();
        Assert.Empty(patches); // No new assignment since target is still valid
    }

    [Fact]
    public async Task ExecuteAsync_KeeperMemoryTargetInvalid_FindsNewSource()
    {
        // Arrange
        var newSource = CreateSource("source2", x: 28, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: "dead_source"); // Invalid ID

        var (context, writer) = CreateContext(keeper, newSource);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == keeper.Id && p.Payload.MemorySourceId is not null);
        Assert.Equal(newSource.Id, payload.MemorySourceId);
    }

    [Fact]
    public async Task ExecuteAsync_KeeperFarFromTarget_MovesTowardTarget()
    {
        // Arrange
        var source = CreateSource("source1", x: 30, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id);

        var (context, writer) = CreateContext(keeper, source);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - keeper should move toward source (right direction)
        var positionPatches = writer.Patches.Where(p => p.ObjectId == keeper.Id && p.Payload.Position is not null).ToList();
        Assert.NotEmpty(positionPatches);

        var (_, payload) = positionPatches[0];
        Assert.NotNull(payload.Position);
        Assert.True(payload.Position.X > 25); // Moved right toward source
    }

    [Fact]
    public async Task ExecuteAsync_KeeperCachesPath_ReusesForSubsequentTicks()
    {
        // Arrange
        var source = CreateSource("source1", x: 30, y: 25);
        var packedPath = PathCaching.PackPosition(30, 25);
        var memoryMove = new KeeperMoveMemory(
            Dest: packedPath,
            Path: packedPath,
            Time: 100,
            LastMove: null
        );
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id, memoryMove: memoryMove);

        var (context, writer) = CreateContext(110, keeper, source); // Within 50-tick cache window

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - keeper should reuse cached path, so no new MemoryMove patch
        var memoryPatches = writer.Patches.Where(p => p.ObjectId == keeper.Id && p.Payload.MemoryMove is not null).ToList();
        Assert.Empty(memoryPatches); // Path still valid, no recalculation
    }

    [Fact]
    public async Task ExecuteAsync_KeeperCachedPathExpired_RecalculatesPath()
    {
        // Arrange
        var source = CreateSource("source1", x: 30, y: 25);
        var oldPacked = PathCaching.PackPosition(20, 20); // Wrong destination
        var memoryMove = new KeeperMoveMemory(
            Dest: oldPacked,
            Path: oldPacked,
            Time: 100,
            LastMove: null
        );
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id, memoryMove: memoryMove);

        var (context, writer) = CreateContext(200, keeper, source); // Expired (>50 ticks)

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - keeper should recalculate path
        var memoryPatches = writer.Patches.Where(p => p.ObjectId == keeper.Id && p.Payload.MemoryMove is not null).ToList();
        Assert.NotEmpty(memoryPatches); // New path calculated
    }

    [Fact]
    public async Task ExecuteAsync_HostileInMeleeRange_AttacksLowestHp()
    {
        // Arrange
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, attackParts: 10);
        var hostile1 = CreateHostileCreep("hostile1", userId: "user1", x: 25, y: 26, hits: 1000); // Adjacent, higher HP
        var hostile2 = CreateHostileCreep("hostile2", userId: "user1", x: 26, y: 25, hits: 500);  // Adjacent, lower HP

        var (context, writer) = CreateContext(keeper, hostile1, hostile2);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - should attack hostile2 (lowest HP)
        var attackedPatches = writer.Patches.Where(p => p.ObjectId == hostile2.Id && p.Payload.Hits.HasValue).ToList();
        Assert.NotEmpty(attackedPatches);

        var (_, payload) = attackedPatches[0];
        Assert.Equal(200, payload.Hits); // 500 - (30 * 10) = 200
    }

    [Fact]
    public async Task ExecuteAsync_HostilesInRangedRange_UsesRangedAttack()
    {
        // Arrange
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, rangedParts: 10);
        var hostile = CreateHostileCreep("hostile1", userId: "user1", x: 27, y: 25, hits: 1000); // 2 tiles away

        var (context, writer) = CreateContext(keeper, hostile);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - ranged attack at distance 2 = 4 damage per part
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == hostile.Id);
        Assert.Equal(960, payload.Hits); // 1000 - (4 * 10) = 960
    }

    [Fact]
    public async Task ExecuteAsync_MultipleHostilesHighMassDamage_UsesMassAttack()
    {
        // Arrange
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, rangedParts: 2); // 2 ranged parts
        var hostile1 = CreateHostileCreep("h1", userId: "user1", x: 25, y: 27, hits: 100); // Range 2 = 4 dmg/part = 8 total
        var hostile2 = CreateHostileCreep("h2", userId: "user1", x: 27, y: 25, hits: 100); // Range 2 = 4 dmg/part = 8 total
        // Total mass damage = 16 > 13 threshold (not in melee range)

        var (context, writer) = CreateContext(keeper, hostile1, hostile2);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - both hostiles should be damaged
        Assert.Equal(2, writer.Patches.Count(p => p.Payload.Hits.HasValue));
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == hostile1.Id);
        var h2Patch = writer.Patches.Single(p => p.ObjectId == hostile2.Id);
        Assert.Equal(92, Payload.Hits); // 100 - (4 * 2) = 92 (range 2 damage)
        Assert.Equal(92, h2Patch.Payload.Hits); // 100 - (4 * 2) = 92 (range 2 damage)
    }

    [Fact]
    public async Task ExecuteAsync_MultipleHostilesLowMassDamage_UsesSingleTarget()
    {
        // Arrange
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, rangedParts: 1); // 1 ranged part
        var hostile1 = CreateHostileCreep("h1", userId: "user1", x: 28, y: 25, hits: 500); // Range 3 = 1 dmg/part = 1 total
        var hostile2 = CreateHostileCreep("h2", userId: "user1", x: 28, y: 26, hits: 300); // Range 3 = 1 dmg/part = 1 total
        // Total mass damage = 2 < 13 threshold

        var (context, writer) = CreateContext(keeper, hostile1, hostile2);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - only lowest HP target should be attacked
        var damagedCount = writer.Patches.Count(p => p.Payload.Hits.HasValue);
        Assert.Equal(1, damagedCount);
        var (_, payload) = Assert.Single(writer.Patches, p => p.ObjectId == hostile2.Id && p.Payload.Hits.HasValue);
        Assert.Equal(299, payload.Hits); // 300 - 1 = 299
    }

    [Fact]
    public async Task ExecuteAsync_NonKeeperCreep_IsIgnored()
    {
        // Arrange
        var userCreep = CreateHostileCreep("creep1", userId: "user1", x: 25, y: 25, hits: 100);

        var (context, writer) = CreateContext(userCreep);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - no AI logic applied to non-keeper
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_InvaderCreep_NotAttackedByKeeper()
    {
        // Arrange
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, attackParts: 10);
        var invader = CreateHostileCreep("invader1", userId: NpcUserIds.Invader, x: 26, y: 25, hits: 1000); // Adjacent

        var (context, writer) = CreateContext(keeper, invader);

        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - invader should not be attacked (keepers ignore invaders)
        var attackPatches = writer.Patches.Where(p => p.ObjectId == invader.Id && p.Payload.Hits.HasValue).ToList();
        Assert.Empty(attackPatches);
    }

    // Helper methods
    private static RoomObjectSnapshot CreateKeeper(string id, int x, int y,
        int attackParts = 10, int rangedParts = 10, string? memorySourceId = null, KeeperMoveMemory? memoryMove = null)
    {
        var body = new List<CreepBodyPartSnapshot>();
        for (var i = 0; i < 17; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.Tough, 100, null));
        for (var i = 0; i < 13; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.Move, 100, null));
        for (var i = 0; i < attackParts; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null));
        for (var i = 0; i < rangedParts; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.RangedAttack, 100, null));

        var keeper = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Creep,
            RoomName: "W1N1",
            Shard: null,
            UserId: NpcUserIds.SourceKeeper,
            X: x,
            Y: y,
            Hits: 5000,
            HitsMax: 5000,
            Fatigue: 0,
            TicksToLive: null,
            Name: $"Keeper{id}",
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
            Body: body,
            MemorySourceId: memorySourceId,
            MemoryMove: memoryMove);

        return keeper;
    }

    private static RoomObjectSnapshot CreateSource(string id, int x, int y)
    {
        var source = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Source,
            RoomName: "W1N1",
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
            StoreCapacity: 0,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: []);

        return source;
    }

    private static RoomObjectSnapshot CreateHostileCreep(string id, string userId, int x, int y, int hits)
    {
        var body = new List<CreepBodyPartSnapshot>
        {
            new(BodyPartType.Move, 100, null),
            new(BodyPartType.Work, 100, null)
        };

        var creep = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Creep,
            RoomName: "W1N1",
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
            StoreCapacity: 50,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: body);

        return creep;
    }

    private static (RoomProcessorContext Context, CapturingMutationWriter Writer) CreateContext(params RoomObjectSnapshot[] objects)
    {
        var result = CreateContext(gameTime: 100, objects);
        return result;
    }

    private static (RoomProcessorContext Context, CapturingMutationWriter Writer) CreateContext(int gameTime, params RoomObjectSnapshot[] objects)
    {
        var objectDict = objects.ToDictionary(o => o.Id, StringComparer.Ordinal);

        var roomState = new RoomState(
            RoomName: "W1N1",
            GameTime: gameTime,
            Info: null,
            Objects: objectDict,
            Users: new Dictionary<string, UserState>(StringComparer.Ordinal),
            Intents: null,
            Terrain: new Dictionary<string, RoomTerrainSnapshot>(StringComparer.Ordinal),
            Flags: []
        );

        var writer = new CapturingMutationWriter();
        var globalWriter = new NoOpGlobalMutationWriter();
        var statsSink = new NoOpCreepStatsSink();

        var context = new RoomProcessorContext(roomState, writer, statsSink, globalWriter, new NullNotificationSink(), null);
        return (context, writer);
    }

    private sealed class NoOpGlobalMutationWriter : IGlobalMutationWriter
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
#pragma warning disable CA1822 // Method cannot be static as it implements interface member
        public bool TryGetPendingPatch(string objectId, out RoomObjectPatchPayload patch) { patch = new RoomObjectPatchPayload(); return false; }

        public void Reset() { }
    }

    private sealed class NoOpCreepStatsSink : ICreepStatsSink
    {
        public void IncrementEnergyCreeps(string userId, int amount) { }
        public void IncrementCreepsLost(string userId, int bodyParts) { }
        public void IncrementCreepsProduced(string userId, int bodyParts) { }
        public void IncrementSpawnRenewals(string userId) { }
        public void IncrementSpawnRecycles(string userId) { }
        public void IncrementSpawnCreates(string userId) { }
        public void IncrementTombstonesCreated(string userId) { }
        public void IncrementEnergyConstruction(string userId, int amount) { }
        public void IncrementEnergyHarvested(string userId, int amount) { }
        public void IncrementEnergyControl(string userId, int amount) { }
        public Task FlushAsync(int gameTime, CancellationToken token = default) => Task.CompletedTask;
    }
}
