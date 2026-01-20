using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.Models;
using ScreepsDotNet.Engine.Processors.GlobalSteps;

namespace ScreepsDotNet.Engine.Tests.Processors.GlobalSteps;

public sealed class MarketIntentStepTests
{
    [Fact]
    public async Task ExecuteAsync_CreateOrderStagesMutationAndFee()
    {
        var userId = "user1";
        var createIntent = new IntentRecord(
            GlobalIntentTypes.CreateOrder,
            [
                new IntentArgument(new Dictionary<string, IntentFieldValue>
                {
                    [MarketIntentFields.Type] = new(IntentFieldValueKind.Text, TextValue: MarketOrderTypes.Sell),
                    [MarketIntentFields.ResourceType] = new(IntentFieldValueKind.Text, TextValue: ResourceTypes.Energy),
                    [MarketIntentFields.Price] = new(IntentFieldValueKind.Number, NumberValue: 5000),
                    [MarketIntentFields.TotalAmount] = new(IntentFieldValueKind.Number, NumberValue: 100),
                    [MarketIntentFields.RoomName] = new(IntentFieldValueKind.Text, TextValue: "W1N1")
                })
            ]);

        var terminal = CreateTerminal("terminal1", "W1N1", userId);
        var state = CreateState(
            [terminal],
            userId,
            money: 100_000,
            intentRecords: [createIntent]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => 1_000);

        await step.ExecuteAsync(context, CancellationToken.None);

        var mutation = Assert.Single(writer.MarketOrderMutations);
        Assert.Equal(MarketOrderMutationType.Upsert, mutation.Type);
        Assert.False(mutation.IsInterShard);
        Assert.NotNull(mutation.Snapshot);
        Assert.Equal(ResourceTypes.Energy, mutation.Snapshot!.ResourceType);
        Assert.Equal(MarketOrderTypes.Sell, mutation.Snapshot.Type);
        Assert.Equal("W1N1", mutation.Snapshot.RoomName);
        Assert.False(mutation.Snapshot.Active);
        Assert.Equal(100, mutation.Snapshot.RemainingAmount);

        var userMoney = Assert.Single(writer.UserMoneyMutations);
        Assert.Equal(userId, userMoney.UserId);
        Assert.Equal(75_000, userMoney.NewMoney);
        Assert.Equal(75_000, context.UsersById[userId].Money);

        var log = Assert.Single(writer.UserMoneyLogs);
        Assert.Equal(MoneyLogTypes.MarketFee, log.Type);
        Assert.Equal(-25, log.Change); // fee converted to credits
    }

    [Fact]
    public async Task ExecuteAsync_ChangeOrderPriceChargesFee()
    {
        var userId = "user1";
        var existingOrder = new MarketOrderSnapshot(
            "order1",
            userId,
            MarketOrderTypes.Sell,
            "W1N1",
            ResourceTypes.Energy,
            5_500,
            0,
            50,
            50,
            123,
            900,
            true);

        var changeIntent = new IntentRecord(
            GlobalIntentTypes.ChangeOrderPrice,
            [
                new IntentArgument(new Dictionary<string, IntentFieldValue>
                {
                    [MarketIntentFields.OrderId] = new(IntentFieldValueKind.Text, TextValue: existingOrder.Id),
                    [MarketIntentFields.NewPrice] = new(IntentFieldValueKind.Number, NumberValue: 6_000)
                })
            ]);

        var state = CreateState(
            [],
            userId,
            money: 50_000,
            orders: [existingOrder],
            intentRecords: [changeIntent]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => 2_000);

        await step.ExecuteAsync(context, CancellationToken.None);

        // Expect 2 mutations: 1 for price change, 1 for maintenance (deactivation due to no resources)
        Assert.Equal(2, writer.MarketOrderMutations.Count);

        var priceChangeMutation = writer.MarketOrderMutations.First(m => m.Patch?.Price.HasValue == true);
        Assert.Equal(MarketOrderMutationType.Patch, priceChangeMutation.Type);
        Assert.Equal(existingOrder.Id, priceChangeMutation.Id);
        Assert.Equal(6_000, priceChangeMutation.Patch!.Price);

        var userMoney = Assert.Single(writer.UserMoneyMutations);
        Assert.Equal(48_750, userMoney.NewMoney);
        Assert.Equal(48_750, context.UsersById[userId].Money);

        var log = Assert.Single(writer.UserMoneyLogs);
        Assert.Equal(MoneyLogTypes.MarketFee, log.Type);
        Assert.Equal(-1.25, log.Change);
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredOrderRefundsFee()
    {
        var userId = "user1";
        var now = 100_000_000L; // Current timestamp
        var orderAge = 31L * 24 * 60 * 60 * 1000; // 31 days in milliseconds
        var createdTimestamp = now - orderAge;

        var order = new MarketOrderSnapshot(
            "order1",
            userId,
            MarketOrderTypes.Sell,
            "W1N1",
            ResourceTypes.Energy,
            5000,
            0,
            100,
            100,
            123,
            createdTimestamp,
            Active: true);

        var state = CreateState(
            [],
            userId,
            money: 10_000,
            orders: [order]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => now);

        await step.ExecuteAsync(context, CancellationToken.None);

        var removal = Assert.Single(writer.MarketOrderMutations, m => m.Type == MarketOrderMutationType.Remove);
        Assert.Equal("order1", removal.Id);

        var moneyMutation = Assert.Single(writer.UserMoneyMutations);
        Assert.Equal(35_000, moneyMutation.NewMoney); // 10k + 25k refund (100 × 5000 × 0.05)

        var log = Assert.Single(writer.UserMoneyLogs);
        Assert.Equal(MoneyLogTypes.MarketFee, log.Type);
        Assert.Equal(25, log.Change); // Fee in credits
    }

    [Fact]
    public async Task ExecuteAsync_SellOrderDeactivatesWhenResourcesGone()
    {
        var order = new MarketOrderSnapshot(
            "order1",
            "user1",
            MarketOrderTypes.Sell,
            "W1N1",
            ResourceTypes.Energy,
            5000,
            100,
            100,
            100,
            123,
            null,
            Active: true);

        var terminal = CreateTerminal("term1", "W1N1", "user1");
        var state = CreateState([terminal], "user1", 10_000, orders: [order]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => 1_000);

        await step.ExecuteAsync(context, CancellationToken.None);

        var patch = Assert.Single(writer.MarketOrderMutations);
        Assert.Equal("order1", patch.Id);
        Assert.False(patch.Patch!.Active);
        Assert.Equal(0, patch.Patch.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_SellOrderActivatesWhenResourcesAvailable()
    {
        var order = new MarketOrderSnapshot(
            "order1",
            "user1",
            MarketOrderTypes.Sell,
            "W1N1",
            ResourceTypes.Energy,
            5000,
            0,
            100,
            100,
            123,
            null,
            Active: false);

        var terminal = CreateTerminalWithStore("term1", "W1N1", "user1", new Dictionary<string, int>
        {
            [ResourceTypes.Energy] = 500
        });

        var state = CreateState([terminal], "user1", 10_000, orders: [order]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => 1_000);

        await step.ExecuteAsync(context, CancellationToken.None);

        var patch = Assert.Single(writer.MarketOrderMutations);
        Assert.True(patch.Patch!.Active);
        Assert.Equal(500, patch.Patch.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_SellOrderUsesUserResourcesForIntershard()
    {
        var order = new MarketOrderSnapshot(
            "order1",
            "user1",
            MarketOrderTypes.Sell,
            "W1N1",
            ResourceTypes.Pixel,
            5000,
            0,
            100,
            100,
            123,
            null,
            Active: false);

        var state = CreateState(
            [],
            userId: "user1",
            money: 10_000,
            orders: [order],
            userResources: new Dictionary<string, int> { [ResourceTypes.Pixel] = 50 });

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => 1_000);

        await step.ExecuteAsync(context, CancellationToken.None);

        var patch = Assert.Single(writer.MarketOrderMutations);
        Assert.True(patch.Patch!.Active);
        Assert.Equal(50, patch.Patch.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_BuyOrderActivatesWhenMoneyAndSpaceAvailable()
    {
        var order = new MarketOrderSnapshot(
            "order1",
            "user1",
            MarketOrderTypes.Buy,
            "W1N1",
            ResourceTypes.Energy,
            1000,
            0,
            100,
            100,
            123,
            null,
            Active: false);

        var terminal = CreateTerminalWithStore(
            "term1",
            "W1N1",
            "user1",
            new Dictionary<string, int> { [ResourceTypes.Energy] = 100_000 },
            storeCapacity: 300_000);

        var state = CreateState([terminal], "user1", money: 50_000, orders: [order]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => 1_000);

        await step.ExecuteAsync(context, CancellationToken.None);

        // Affordable: floor(50k / 1k) = 50
        // Remaining: 100
        // Space: 300k - 100k = 200k
        // Amount: min(50, 100, 200k) = 50
        var patch = Assert.Single(writer.MarketOrderMutations);
        Assert.True(patch.Patch!.Active);
        Assert.Equal(50, patch.Patch.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_BuyOrderConstrainedByTerminalSpace()
    {
        var order = new MarketOrderSnapshot(
            "order1",
            "user1",
            MarketOrderTypes.Buy,
            "W1N1",
            ResourceTypes.Energy,
            1000,
            0,
            100,
            100,
            123,
            null,
            Active: false);

        var terminal = CreateTerminalWithStore(
            "term1",
            "W1N1",
            "user1",
            new Dictionary<string, int> { [ResourceTypes.Energy] = 100_000 },
            storeCapacity: 100_020);

        var state = CreateState([terminal], "user1", money: 100_000, orders: [order]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => 1_000);

        await step.ExecuteAsync(context, CancellationToken.None);

        // Constrained by space (only 20 free)
        var patch = Assert.Single(writer.MarketOrderMutations);
        Assert.Equal(20, patch.Patch!.Amount);
    }

    [Fact]
    public async Task ExecuteAsync_SkipsNpcOrders()
    {
        var now = 100_000_000L;
        var orderAge = 50L * 24 * 60 * 60 * 1000; // 50 days (well expired)
        var createdTimestamp = now - orderAge;

        var order = new MarketOrderSnapshot(
            "order1",
            null, // NPC order (no userId)
            MarketOrderTypes.Sell,
            "W1N1",
            ResourceTypes.Energy,
            5000,
            100,
            100,
            100,
            123,
            createdTimestamp,
            Active: true);

        var state = CreateState(
            [],
            userId: "user1",
            money: 10_000,
            orders: [order]);

        var writer = new RecordingGlobalMutationWriter();
        var context = new GlobalProcessorContext(state, writer);
        var step = new MarketIntentStep(() => now);

        await step.ExecuteAsync(context, CancellationToken.None);

        // No mutations (NPC order skipped)
        Assert.Empty(writer.MarketOrderMutations);
    }

    private static GlobalState CreateState(
        IReadOnlyList<RoomObjectSnapshot> specialObjects,
        string userId,
        double money,
        IReadOnlyList<MarketOrderSnapshot>? orders = null,
        IReadOnlyList<IntentRecord>? intentRecords = null,
        Dictionary<string, int>? userResources = null)
    {
        orders ??= [];
        intentRecords ??= [];
        userResources ??= [];

        var market = new GlobalMarketSnapshot(
            orders,
            new Dictionary<string, UserState>(StringComparer.Ordinal)
            {
                [userId] = new(userId, "user", 0, 0, money, true, 0, userResources)
            },
            [],
            [
                new GlobalUserIntentSnapshot("intentDoc", userId, intentRecords)
            ],
            TestConstants.DefaultShardName);

        return new GlobalState(
            12345,
            [],
            new Dictionary<string, RoomInfoSnapshot>(0, StringComparer.Ordinal),
            new Dictionary<string, RoomExitTopology>(0, StringComparer.Ordinal),
            specialObjects,
            market);
    }

    private static RoomObjectSnapshot CreateTerminal(string id, string roomName, string userId)
        => new(
            id,
            RoomObjectTypes.Terminal,
            roomName,
            TestConstants.DefaultShardName,
            userId,
            0,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, int>(StringComparer.Ordinal),
            ScreepsGameConstants.TerminalCapacity,
            new Dictionary<string, int>(StringComparer.Ordinal),
            null,
            null,
            null,
            new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            null,
            Array.Empty<CreepBodyPartSnapshot>(),
            IsSpawning: false,
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
            ResourceAmount: null);

    private static RoomObjectSnapshot CreateTerminalWithStore(
        string id,
        string roomName,
        string userId,
        Dictionary<string, int> store,
        int? storeCapacity = null)
        => new(
            id,
            RoomObjectTypes.Terminal,
            roomName,
            TestConstants.DefaultShardName,
            userId,
            0,
            0,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            store,
            storeCapacity ?? ScreepsGameConstants.TerminalCapacity,
            new Dictionary<string, int>(StringComparer.Ordinal),
            null,
            null,
            null,
            new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            null,
            Array.Empty<CreepBodyPartSnapshot>(),
            IsSpawning: false,
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
            ResourceAmount: null);
}
