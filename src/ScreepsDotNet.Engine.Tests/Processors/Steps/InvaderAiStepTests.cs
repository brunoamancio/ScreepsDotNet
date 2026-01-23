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
/// Unit tests for invader AI mechanics (healer mode, attacker mode, flee, ranged attacks).
/// Tests validate invader behavior without full processor context (using test stubs).
/// </summary>
public sealed class InvaderAiStepTests
{
    private static readonly BodyPartType[] InvaderAttackerBody =
    [
        BodyPartType.Tough,
        BodyPartType.Move,
        BodyPartType.Attack,
        BodyPartType.RangedAttack
    ];

    private static readonly BodyPartType[] InvaderHealerBody =
    [
        BodyPartType.Tough,
        BodyPartType.Move,
        BodyPartType.Heal
    ];

    private static readonly BodyPartType[] InvaderRangedBody =
    [
        BodyPartType.Tough,
        BodyPartType.Move,
        BodyPartType.RangedAttack, BodyPartType.RangedAttack
    ];

    [Fact]
    public async Task ExecuteAsync_InvaderHealer_HealsLowestHpInvader()
    {
        // Arrange - Healer invader with damaged invader nearby
        var healer = CreateInvader("healer1", InvaderHealerBody, x: 25, y: 25);
        var damagedInvader = CreateInvader("invader1", InvaderAttackerBody, x: 26, y: 25, hits: 50);
        var fullHpInvader = CreateInvader("invader2", InvaderAttackerBody, x: 27, y: 25);

        var (context, writer) = CreateContext(healer, damagedInvader, fullHpInvader);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Healer should heal damaged invader
        var healPatches = writer.Patches
            .Where(p => p.ObjectId == "invader1" && p.Payload.Hits.HasValue)
            .ToList();

        Assert.NotEmpty(healPatches);
        var newHits = healPatches.Last().Payload.Hits!.Value;
        Assert.True(newHits > 50, $"Damaged invader should be healed (hits: {newHits})");
    }

    [Fact]
    public async Task ExecuteAsync_InvaderAttacker_MovesTowardHostile()
    {
        // Arrange - Attacker invader with hostile creep
        var invader = CreateInvader("invader1", InvaderAttackerBody, x: 25, y: 25);
        var hostile = CreateHostile("hostile1", userId: "user1", x: 30, y: 25);

        var (context, writer) = CreateContext(invader, hostile);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Invader should move toward hostile
        var movementPatches = writer.Patches
            .Where(p => p.ObjectId == "invader1" && p.Payload.Position is not null)
            .ToList();

        Assert.NotEmpty(movementPatches);
        var newPos = movementPatches.Last().Payload.Position!;
        Assert.True(newPos.X > 25, $"Invader should move toward hostile (new X: {newPos.X})");
    }

    [Fact]
    public async Task ExecuteAsync_InvaderAttacker_AttacksAdjacentHostile()
    {
        // Arrange - Attacker invader adjacent to hostile
        var invader = CreateInvader("invader1", InvaderAttackerBody, x: 25, y: 25);
        var hostile = CreateHostile("hostile1", userId: "user1", x: 26, y: 25);

        var (context, writer) = CreateContext(invader, hostile);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Hostile should take damage
        var damagePatches = writer.Patches
            .Where(p => p.ObjectId == "hostile1" && p.Payload.Hits.HasValue)
            .ToList();

        Assert.NotEmpty(damagePatches);
        var newHits = damagePatches.Last().Payload.Hits!.Value;
        Assert.True(newHits < 200, $"Hostile should take damage (hits: {newHits})");
    }

    [Fact]
    public async Task ExecuteAsync_InvaderRanged_ShootsAtWill()
    {
        // Arrange - Ranged invader with hostile in range 3
        var invader = CreateInvader("invader1", InvaderRangedBody, x: 25, y: 25);
        var hostile = CreateHostile("hostile1", userId: "user1", x: 27, y: 25); // Distance 2

        var (context, writer) = CreateContext(invader, hostile);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Hostile should take ranged damage
        var damagePatches = writer.Patches
            .Where(p => p.ObjectId == "hostile1" && p.Payload.Hits.HasValue)
            .ToList();

        Assert.NotEmpty(damagePatches);
        var newHits = damagePatches.Last().Payload.Hits!.Value;
        Assert.True(newHits < 200, $"Hostile should take ranged damage (hits: {newHits})");
    }

    [Fact]
    public async Task ExecuteAsync_InvaderHealer_FleeWhenDamaged()
    {
        // Arrange - Damaged healer with hostile nearby
        var healer = CreateInvader("healer1", InvaderHealerBody, x: 25, y: 25, hits: 50);
        var hostile = CreateHostile("hostile1", userId: "user1", x: 27, y: 25);

        var (context, writer) = CreateContext(healer, hostile);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Healer should move (fleeing or toward other healer)
        var movementPatches = writer.Patches
            .Where(p => p.ObjectId == "healer1" && p.Payload.Position is not null)
            .ToList();

        Assert.NotEmpty(movementPatches);
    }

    [Fact]
    public async Task ExecuteAsync_InvaderRangedOnly_FleesFromHostile()
    {
        // Arrange - Ranged-only invader (no ATTACK parts) with hostile nearby
        var invader = CreateInvader("invader1", InvaderRangedBody, x: 25, y: 25);
        var hostile = CreateHostile("hostile1", userId: "user1", x: 27, y: 25);

        var (context, writer) = CreateContext(invader, hostile);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Ranged invader should flee or move
        var movementPatches = writer.Patches
            .Where(p => p.ObjectId == "invader1" && p.Payload.Position is not null)
            .ToList();

        Assert.NotEmpty(movementPatches);
    }

    [Fact]
    public async Task ExecuteAsync_Invader_IgnoresSourceKeepers()
    {
        // Arrange - Invader with source keeper nearby
        var invader = CreateInvader("invader1", InvaderAttackerBody, x: 25, y: 25);
        var keeper = CreateKeeper("keeper1", x: 26, y: 25);

        var (context, writer) = CreateContext(invader, keeper);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Keeper should not be damaged (invaders ignore keepers)
        var keeperDamaged = writer.Patches.Any(p => p.ObjectId == "keeper1" && p.Payload.Hits.HasValue);
        Assert.False(keeperDamaged, "Invader should not attack source keeper");
    }

    [Fact]
    public async Task ExecuteAsync_InvaderHealer_TargetsMostDamagedInvader()
    {
        // Arrange - Healer with multiple damaged invaders
        var healer = CreateInvader("healer1", InvaderHealerBody, x: 25, y: 25);
        var slightlyDamaged = CreateInvader("invader1", InvaderAttackerBody, x: 26, y: 25, hits: 150);
        var mostDamaged = CreateInvader("invader2", InvaderAttackerBody, x: 27, y: 25, hits: 50); // Most damaged

        var (context, writer) = CreateContext(healer, slightlyDamaged, mostDamaged);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Most damaged invader should be healed
        var invader2Healed = writer.Patches
            .Any(p => p.ObjectId == "invader2" && p.Payload.Hits.HasValue && p.Payload.Hits > 50);

        Assert.True(invader2Healed, "Healer should target most damaged invader");
    }

    [Fact]
    public async Task ExecuteAsync_InvaderRanged_TargetsLowestHpHostile()
    {
        // Arrange - Ranged invader with multiple hostiles
        var invader = CreateInvader("invader1", InvaderRangedBody, x: 25, y: 25);
        var hostile1 = CreateHostile("hostile1", userId: "user1", x: 26, y: 25, hits: 150);
        var hostile2 = CreateHostile("hostile2", userId: "user1", x: 27, y: 25, hits: 50); // Lowest HP

        var (context, writer) = CreateContext(invader, hostile1, hostile2);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Lowest HP hostile should be damaged
        var hostile2Damaged = writer.Patches
            .Any(p => p.ObjectId == "hostile2" && p.Payload.Hits.HasValue && p.Payload.Hits < 50);

        Assert.True(hostile2Damaged, "Invader should target lowest HP hostile");
    }

    [Fact]
    public async Task ExecuteAsync_NoInvaders_NoActions()
    {
        // Arrange - Room with no invaders
        var keeper = CreateKeeper("keeper1", x: 25, y: 25);
        var hostile = CreateHostile("hostile1", userId: "user1", x: 26, y: 25);

        var (context, writer) = CreateContext(keeper, hostile);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - No patches should be generated
        Assert.Empty(writer.Patches);
    }

    [Fact]
    public async Task ExecuteAsync_InvaderAttacker_CombatAndMovementTogether()
    {
        // Arrange - Invader near hostile (can attack and move)
        var invader = CreateInvader("invader1", InvaderAttackerBody, x: 25, y: 25);
        var nearby = CreateHostile("hostile1", userId: "user1", x: 26, y: 25);
        var faraway = CreateHostile("hostile2", userId: "user1", x: 30, y: 25);

        var (context, writer) = CreateContext(invader, nearby, faraway);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Both combat and movement can occur
        var hostileDamaged = writer.Patches.Any(p =>
            p.ObjectId == "hostile1" && p.Payload.Hits.HasValue && p.Payload.Hits < 200);

        // Either attack occurred or movement occurred (or both)
        Assert.True(hostileDamaged || writer.Patches.Any(p => p.ObjectId == "invader1"),
            "Invader should attack or move (or both)");
    }

    [Fact]
    public async Task ExecuteAsync_InvaderHealer_UsesRangedHealWhenNotAdjacent()
    {
        // Arrange - Healer not adjacent to damaged invader (distance 2)
        var healer = CreateInvader("healer1", InvaderHealerBody, x: 25, y: 25);
        var damaged = CreateInvader("invader1", InvaderAttackerBody, x: 27, y: 25, hits: 50);

        var (context, writer) = CreateContext(healer, damaged);
        var step = new InvaderAiStep();

        // Act
        await step.ExecuteAsync(context, TestContext.Current.CancellationToken);

        // Assert - Ranged heal should occur (less healing than melee)
        var healPatches = writer.Patches
            .Where(p => p.ObjectId == "invader1" && p.Payload.Hits.HasValue)
            .ToList();

        Assert.NotEmpty(healPatches);
        var newHits = healPatches.Last().Payload.Hits!.Value;
        Assert.True(newHits > 50, $"Damaged invader should be healed (hits: {newHits})");
    }

    // Helper methods

    private static RoomObjectSnapshot CreateInvader(
        string id,
        BodyPartType[] bodyTypes,
        int x = 25,
        int y = 25,
        int hits = 200)
    {
        var body = bodyTypes.Select(type => new CreepBodyPartSnapshot(type, 100, null)).ToArray();
        var creep = new RoomObjectSnapshot(
            Id: id,
            Type: RoomObjectTypes.Creep,
            RoomName: "W1N1",
            Shard: null,
            UserId: NpcUserIds.Invader,
            X: x,
            Y: y,
            Hits: hits,
            HitsMax: 200,
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
            Body: body);
        return creep;
    }

    private static RoomObjectSnapshot CreateHostile(
        string id,
        string userId,
        int x,
        int y,
        int hits = 200)
    {
        var body = new[]
        {
            new CreepBodyPartSnapshot(BodyPartType.Move, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Work, 100, null)
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
            HitsMax: 200,
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

    private static RoomObjectSnapshot CreateKeeper(string id, int x, int y)
    {
        var body = new[]
        {
            new CreepBodyPartSnapshot(BodyPartType.Tough, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Move, 100, null),
            new CreepBodyPartSnapshot(BodyPartType.Attack, 100, null)
        };

        var creep = new RoomObjectSnapshot(
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
            Body: body);
        return creep;
    }

    private static (RoomProcessorContext Context, CapturingMutationWriter Writer) CreateContext(params RoomObjectSnapshot[] objects)
    {
        var objectDict = objects.ToDictionary(o => o.Id, StringComparer.Ordinal);

        var roomState = new RoomState(
            RoomName: "W1N1",
            GameTime: 100,
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

        var context = new RoomProcessorContext(roomState, writer, statsSink, globalWriter, null);
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
