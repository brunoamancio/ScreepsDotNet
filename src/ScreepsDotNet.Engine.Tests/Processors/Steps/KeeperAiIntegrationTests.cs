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

/// <summary>
/// Integration tests for keeper AI behavior across multiple ticks and interactions with other systems.
/// These tests verify the full keeper lifecycle: spawn → assign target → move → attack.
/// </summary>
public sealed class KeeperAiIntegrationTests
{
    [Fact]
    public async Task KeeperLifecycle_SpawnToSourceAssignment_MovesAndDefends()
    {
        // Arrange - Keeper spawns near source (within 5 tiles), hostile appears
        var source = CreateSource("source1", x: 28, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25); // 3 tiles away (within range)
        var hostile = CreateHostileCreep("hostile1", userId: "user1", x: 29, y: 25, hits: 100); // Near source

        var gameTime = 100;
        var (context, writer) = CreateContext(gameTime, keeper, source, hostile);
        var step = new KeeperAiStep();

        // Act - Tick 1: Keeper should assign source and start moving
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Tick 1: Source assigned, path cached, keeper moved
        var tick1Patches = writer.Patches.Where(p => p.ObjectId == keeper.Id).ToList();
        Assert.True(tick1Patches.Count >= 2, "Expected at least 2 patches: source assignment + movement");

        var (ObjectId, Payload) = tick1Patches.SingleOrDefault(p => p.Payload.MemorySourceId is not null);
        Assert.True(ObjectId is not null, "Expected source assignment patch");
        Assert.Equal(source.Id, Payload.MemorySourceId);

        var movement = tick1Patches.SingleOrDefault(p => p.Payload.Position is not null);
        Assert.True(movement.ObjectId is not null, "Expected movement patch");
        Assert.True(movement.Payload.Position!.X > keeper.X, "Keeper should move toward source (east)");
    }

    [Fact]
    public async Task KeeperCombat_HostileEntersRange_AttacksImmediately()
    {
        // Arrange - Keeper already has target, hostile enters melee range
        var source = CreateSource("source1", x: 26, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id, attackParts: 2, rangedParts: 1);
        var hostile = CreateHostileCreep("hostile1", userId: "user1", x: 26, y: 25, hits: 100); // Melee range (also in ranged range)

        var (context, writer) = CreateContext(100, keeper, source, hostile);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Hostile should be damaged
        var hostilePatches = writer.Patches.Where(p => p.ObjectId == hostile.Id && p.Payload.Hits.HasValue).ToList();
        Assert.NotEmpty(hostilePatches);
        // Verify keeper attacked the hostile (damage was dealt)
        var (ObjectId, Payload) = hostilePatches.Last();
        Assert.True(Payload.Hits < 100, "Hostile should have taken damage");
    }

    [Fact]
    public async Task KeeperPathCaching_MultipleTicksToSource_ReusesPath()
    {
        // Arrange - Keeper near source (within 5 tiles), needs multiple ticks to reach
        var source = CreateSource("source1", x: 29, y: 25);
        var keeper1 = CreateKeeper("keeper1", x: 25, y: 25); // 4 tiles away (within range)

        var gameTime = 100;

        // Tick 1: Initial path calculation
        var (context1, writer1) = CreateContext(gameTime, keeper1, source);
        var step = new KeeperAiStep();
        await step.ExecuteAsync(context1, TestContext.Current.CancellationToken);

        var (ObjectId, Payload) = writer1.Patches.Single(p => p.ObjectId == keeper1.Id && p.Payload.Position is not null);
        var tick1MemoryMove = writer1.Patches.Single(p => p.ObjectId == keeper1.Id && p.Payload.MemoryMove is not null);
        Assert.NotNull(tick1MemoryMove.Payload.MemoryMove);
        Assert.Equal(gameTime, tick1MemoryMove.Payload.MemoryMove!.Time);

        // Tick 2: Keeper at new position, should reuse cached path
        var keeper2 = keeper1 with
        {
            X = Payload.Position!.X!.Value,
            Y = Payload.Position!.Y!.Value,
            MemorySourceId = source.Id,
            MemoryMove = tick1MemoryMove.Payload.MemoryMove
        };

        var (context2, writer2) = CreateContext(gameTime + 10, keeper2, source); // Within 50-tick cache window
        await step.ExecuteAsync(context2, TestContext.Current.CancellationToken);

        // Assert - Should move but NOT recalculate path (no new MemoryMove patch)
        var tick2Patches = writer2.Patches.Where(p => p.ObjectId == keeper2.Id).ToList();
        var tick2Movement = tick2Patches.SingleOrDefault(p => p.Payload.Position is not null);
        var tick2MemoryMove = tick2Patches.SingleOrDefault(p => p.Payload.MemoryMove is not null);

        Assert.True(tick2Movement.ObjectId is not null, "Still moving"); // Still moving
        Assert.True(tick2MemoryMove.ObjectId is null, "No new path calculation (reused cached path)"); // No new path calculation (reused cached path)
    }

    [Fact]
    public async Task KeeperPathCaching_PathExpires_RecalculatesAfter50Ticks()
    {
        // Arrange - Keeper with cached path that will expire
        var source = CreateSource("source1", x: 30, y: 25);
        var packedDest = PathCaching.PackPosition(source.X, source.Y);
        var memoryMove = new KeeperMoveMemory(
            Dest: packedDest,
            Path: packedDest,
            Time: 100,
            LastMove: null
        );
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id, memoryMove: memoryMove);

        var gameTime = 160; // 60 ticks later (> 50 tick cache window)
        var (context, writer) = CreateContext(gameTime, keeper, source);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Should recalculate path (new MemoryMove patch with updated time)
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == keeper.Id && p.Payload.MemoryMove is not null);
        Assert.NotNull(Payload.MemoryMove);
        Assert.Equal(gameTime, Payload.MemoryMove!.Time); // New timestamp
    }

    [Fact]
    public async Task KeeperCombat_MultipleHostilesRangedRange_UsesMassAttack()
    {
        // Arrange - Multiple hostiles in ranged range, high total damage
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, rangedParts: 2);
        var hostile1 = CreateHostileCreep("h1", userId: "user1", x: 25, y: 27, hits: 100); // Range 2
        var hostile2 = CreateHostileCreep("h2", userId: "user1", x: 27, y: 25, hits: 100); // Range 2
        var hostile3 = CreateHostileCreep("h3", userId: "user1", x: 27, y: 27, hits: 100); // Range 2
        // Total mass damage = 3 hostiles × 2 ranged parts × 4 damage (range 2) = 24 > 13 threshold

        var (context, writer) = CreateContext(100, keeper, hostile1, hostile2, hostile3);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - All hostiles should be damaged (mass attack)
        var damagedHostiles = writer.Patches.Where(p => p.Payload.Hits.HasValue).ToList();
        Assert.Equal(3, damagedHostiles.Count);

        foreach (var (ObjectId, Payload) in damagedHostiles) {
            var expectedDamage = 2 * 4; // 2 ranged parts × 4 damage (range 2)
            var expectedHits = 100 - expectedDamage;
            Assert.Equal(expectedHits, Payload.Hits);
        }
    }

    [Fact]
    public async Task KeeperDefense_InvaderAppears_IgnoresInvader()
    {
        // Arrange - Keeper with source, invader appears nearby
        var source = CreateSource("source1", x: 26, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id);
        var invader = CreateInvaderCreep("invader1", x: 26, y: 25, hits: 100); // Adjacent to keeper

        var (context, writer) = CreateContext(100, keeper, source, invader);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Invader should NOT be attacked
        var invaderPatches = writer.Patches.Where(p => p.ObjectId == invader.Id).ToList();
        Assert.Empty(invaderPatches);
    }

    [Fact]
    public async Task KeeperCombat_MeleeAndRangedTargets_AttacksBoth()
    {
        // Arrange - One hostile in melee range, another in ranged range
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, attackParts: 2, rangedParts: 2);
        var meleeHostile = CreateHostileCreep("melee", userId: "user1", x: 26, y: 25, hits: 100); // Range 1
        var rangedHostile = CreateHostileCreep("ranged", userId: "user1", x: 28, y: 25, hits: 50); // Range 3

        var (context, writer) = CreateContext(100, keeper, meleeHostile, rangedHostile);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Both hostiles should be damaged
        var meleePatches = writer.Patches.Where(p => p.ObjectId == meleeHostile.Id && p.Payload.Hits.HasValue).ToList();
        var rangedPatches = writer.Patches.Where(p => p.ObjectId == rangedHostile.Id && p.Payload.Hits.HasValue).ToList();

        Assert.NotEmpty(meleePatches);
        Assert.NotEmpty(rangedPatches);

        var (ObjectId, Payload) = meleePatches.Last(); // Last patch has final value
        var rangedPatch = rangedPatches.Last();

        // Verify both hostiles were attacked
        Assert.True(Payload.Hits < 100, "Melee hostile should have taken damage");
        Assert.True(rangedPatch.Payload.Hits < 50, "Ranged hostile should have taken damage");
    }

    [Fact]
    public async Task KeeperTargeting_SourceDisappears_FindsNewSource()
    {
        // Arrange - Keeper has memory of source that no longer exists, new source nearby
        var newSource = CreateSource("source2", x: 28, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: "dead_source");

        var (context, writer) = CreateContext(100, keeper, newSource);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Keeper should assign new source
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == keeper.Id && p.Payload.MemorySourceId is not null);
        Assert.Equal(newSource.Id, Payload.MemorySourceId);
    }

    [Fact]
    public async Task KeeperTargeting_MultipleSourcesNearby_ChoosesNearest()
    {
        // Arrange - Multiple sources at different distances
        var nearSource = CreateSource("source1", x: 27, y: 25); // 2 tiles away
        var farSource = CreateSource("source2", x: 35, y: 25);  // 10 tiles away
        var keeper = CreateKeeper("keeper1", x: 25, y: 25);

        var (context, writer) = CreateContext(100, keeper, nearSource, farSource);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Keeper should choose nearest source
        var (ObjectId, Payload) = writer.Patches.Single(p => p.ObjectId == keeper.Id && p.Payload.MemorySourceId is not null);
        Assert.Equal(nearSource.Id, Payload.MemorySourceId);
    }

    [Fact]
    public async Task KeeperMovement_ReachesSource_StopsMoving()
    {
        // Arrange - Keeper adjacent to source (distance = 1)
        var source = CreateSource("source1", x: 26, y: 25);
        var keeper = CreateKeeper("keeper1", x: 25, y: 25, memorySourceId: source.Id);

        var (context, writer) = CreateContext(100, keeper, source);
        var step = new KeeperAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Keeper should NOT move (already at source)
        var movementPatches = writer.Patches.Where(p => p.ObjectId == keeper.Id && p.Payload.Position is not null).ToList();
        Assert.Empty(movementPatches);
    }

    // Helper methods
    private static RoomObjectSnapshot CreateKeeper(
        string id,
        int x,
        int y,
        int attackParts = 1,
        int rangedParts = 1,
        string? memorySourceId = null,
        KeeperMoveMemory? memoryMove = null)
    {
        var body = new List<CreepBodyPartSnapshot>();
        for (var i = 0; i < attackParts; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null));
        for (var i = 0; i < rangedParts; i++)
            body.Add(new CreepBodyPartSnapshot(BodyPartType.RangedAttack, 100, null));

        body.Add(new CreepBodyPartSnapshot(BodyPartType.Move, 100, null));

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

    private static RoomObjectSnapshot CreateSource(string id, int x, int y, int energy = 3000)
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

    private static RoomObjectSnapshot CreateInvaderCreep(string id, int x, int y, int hits)
    {
        var body = new List<CreepBodyPartSnapshot>
        {
            new(BodyPartType.Move, 100, null),
            new(BodyPartType.Attack, 100, null)
        };

        var creep = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Creep,
            RoomName: "W1N1",
            Shard: null,
            UserId: NpcUserIds.Invader, // Invader user ID
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
