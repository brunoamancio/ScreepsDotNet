using ScreepsDotNet.Common.Constants;
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

        var mutation = Assert.Single(writer.MarketOrderMutations);
        Assert.Equal(MarketOrderMutationType.Patch, mutation.Type);
        Assert.Equal(existingOrder.Id, mutation.Id);
        Assert.Equal(6_000, mutation.Patch!.Price);

        var userMoney = Assert.Single(writer.UserMoneyMutations);
        Assert.Equal(48_750, userMoney.NewMoney);
        Assert.Equal(48_750, context.UsersById[userId].Money);

        var log = Assert.Single(writer.UserMoneyLogs);
        Assert.Equal(MoneyLogTypes.MarketFee, log.Type);
        Assert.Equal(-1.25, log.Change);
    }

    private static GlobalState CreateState(
        IReadOnlyList<RoomObjectSnapshot> specialObjects,
        string userId,
        double money,
        IReadOnlyList<MarketOrderSnapshot>? orders = null,
        IReadOnlyList<IntentRecord>? intentRecords = null)
    {
        orders ??= [];
        intentRecords ??= [];

        var market = new GlobalMarketSnapshot(
            orders,
            new Dictionary<string, UserState>(StringComparer.Ordinal)
            {
                [userId] = new(userId, "user", 0, 0, money, true, 0)
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
            new Dictionary<string, object?>(StringComparer.Ordinal),
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
