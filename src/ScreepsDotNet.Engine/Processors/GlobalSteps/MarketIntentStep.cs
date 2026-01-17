namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

using System;
using System.Collections.Generic;
using MongoDB.Bson;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Handles market-related global intents (create/cancel/extend/change price).
/// </summary>
internal sealed class MarketIntentStep : IGlobalProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;
    private static readonly HashSet<string> IntershardResources = new(ScreepsGameConstants.IntershardResources, Comparer);
    private static readonly HashSet<string> ValidResources = new(ScreepsGameConstants.ResourceOrder, Comparer);
    private readonly Func<long> _timestampProvider;

    public MarketIntentStep()
        : this(() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    {
    }

    internal MarketIntentStep(Func<long> timestampProvider)
        => _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));

    public Task ExecuteAsync(GlobalProcessorContext context, CancellationToken token = default)
    {
        var orderMap = BuildOrderMap(context.State.Market.Orders);
        var terminalsByRoom = BuildTerminalMap(context);

        foreach (var (userId, intentSnapshot) in context.UserIntentsByUser) {
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            if (!context.UsersById.TryGetValue(userId, out var userState))
                continue;

            foreach (var record in intentSnapshot.Intents) {
                switch (record.Name) {
                    case GlobalIntentTypes.CreateOrder:
                        ProcessCreateOrders(record.Arguments, context, userId, terminalsByRoom, orderMap);
                        break;
                    case GlobalIntentTypes.CancelOrder:
                        ProcessCancelOrders(record.Arguments, context, userId, orderMap);
                        break;
                    case GlobalIntentTypes.ChangeOrderPrice:
                        ProcessChangePrice(record.Arguments, context, userId, orderMap);
                        break;
                    case GlobalIntentTypes.ExtendOrder:
                        ProcessExtendOrders(record.Arguments, context, userId, orderMap);
                        break;
                    default:
                        break;
                }
            }
        }

        return Task.CompletedTask;
    }

    private void ProcessCreateOrders(
        IReadOnlyList<IntentArgument> arguments,
        GlobalProcessorContext context,
        string userId,
        IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom,
        Dictionary<string, OrderState> orderMap)
    {
        foreach (var argument in arguments) {
            var orderType = GetText(argument, "type");
            if (!IsValidOrderType(orderType))
                continue;

            var resourceType = GetText(argument, "resourceType");
            if (!IsValidResource(resourceType))
                continue;

            if (!TryGetInt(argument, "totalAmount", out var totalAmount) || totalAmount <= 0)
                continue;

            if (!TryGetLong(argument, "price", out var price) || price <= 0)
                continue;

            var roomName = GetText(argument, "roomName");
            var isIntershard = IntershardResources.Contains(resourceType!);
            if (!isIntershard && string.IsNullOrWhiteSpace(roomName))
                continue;

            if (!isIntershard && !IsRoomTerminalOwnedByUser(roomName!, userId, terminalsByRoom))
                continue;

            var fee = Math.Ceiling(price * totalAmount * ScreepsGameConstants.MarketFee);
            if (!TryDebitUser(context, userId, fee, out var newBalance))
                continue;

            var orderId = ObjectId.GenerateNewId().ToString();
            var snapshot = new MarketOrderSnapshot(
                orderId,
                userId,
                NormalizeOrderType(orderType!),
                roomName,
                resourceType,
                price,
                0,
                totalAmount,
                totalAmount,
                isIntershard ? null : context.GameTime,
                _timestampProvider(),
                false);

            context.Mutations.UpsertMarketOrder(snapshot, isIntershard);
            context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(userId, context.GameTime, newBalance, -fee, "market.fee", new Dictionary<string, object?>(Comparer)
            {
                ["order"] = new Dictionary<string, object?>
                {
                    ["resourceType"] = resourceType,
                    ["roomName"] = roomName,
                    ["type"] = snapshot.Type,
                    ["price"] = price / 1000.0,
                    ["amount"] = totalAmount
                }
            }));

            orderMap[orderId] = new OrderState(snapshot, isIntershard);
        }
    }

    private static void ProcessCancelOrders(
        IReadOnlyList<IntentArgument> arguments,
        GlobalProcessorContext context,
        string userId,
        Dictionary<string, OrderState> orderMap)
    {
        foreach (var argument in arguments) {
            var orderId = GetText(argument, "orderId");
            if (string.IsNullOrWhiteSpace(orderId))
                continue;

            if (!orderMap.TryGetValue(orderId!, out var state))
                continue;

            if (!string.Equals(state.Snapshot.UserId, userId, StringComparison.Ordinal))
                continue;

            orderMap.Remove(orderId!);
            context.Mutations.RemoveMarketOrder(orderId!, state.IsInterShard);
        }
    }

    private void ProcessChangePrice(
        IReadOnlyList<IntentArgument> arguments,
        GlobalProcessorContext context,
        string userId,
        Dictionary<string, OrderState> orderMap)
    {
        foreach (var argument in arguments) {
            var orderId = GetText(argument, "orderId");
            if (string.IsNullOrWhiteSpace(orderId))
                continue;

            if (!orderMap.TryGetValue(orderId!, out var state))
                continue;

            if (!string.Equals(state.Snapshot.UserId, userId, StringComparison.Ordinal))
                continue;

            if (!TryGetLong(argument, "newPrice", out var newPrice) || newPrice <= 0)
                continue;

            var currentOrder = state.Snapshot;
            var priceDelta = newPrice - currentOrder.Price;
            if (priceDelta > 0) {
                var fee = Math.Ceiling(priceDelta * currentOrder.RemainingAmount * ScreepsGameConstants.MarketFee);
                if (!TryDebitUser(context, userId, fee, out var newBalance))
                    continue;

                context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(userId, context.GameTime, newBalance, -fee, "market.fee", new Dictionary<string, object?>(Comparer)
                {
                    ["changeOrderPrice"] = new Dictionary<string, object?>
                    {
                        ["orderId"] = orderId,
                        ["oldPrice"] = currentOrder.Price / 1000.0,
                        ["newPrice"] = newPrice / 1000.0
                    }
                }));
            }

            var patch = new MarketOrderPatch(Price: newPrice);
            context.Mutations.PatchMarketOrder(orderId!, patch, state.IsInterShard);
            orderMap[orderId!] = state with { Snapshot = currentOrder with { Price = newPrice } };
        }
    }

    private void ProcessExtendOrders(
        IReadOnlyList<IntentArgument> arguments,
        GlobalProcessorContext context,
        string userId,
        Dictionary<string, OrderState> orderMap)
    {
        foreach (var argument in arguments) {
            var orderId = GetText(argument, "orderId");
            if (string.IsNullOrWhiteSpace(orderId))
                continue;

            if (!orderMap.TryGetValue(orderId!, out var state))
                continue;

            if (!string.Equals(state.Snapshot.UserId, userId, StringComparison.Ordinal))
                continue;

            if (!TryGetInt(argument, "addAmount", out var addAmount) || addAmount <= 0)
                continue;

            var currentOrder = state.Snapshot;
            var fee = Math.Ceiling(currentOrder.Price * addAmount * ScreepsGameConstants.MarketFee);
            if (!TryDebitUser(context, userId, fee, out var newBalance))
                continue;

            var newRemaining = currentOrder.RemainingAmount + addAmount;
            var newTotal = currentOrder.TotalAmount + addAmount;

            var patch = new MarketOrderPatch(RemainingAmount: newRemaining, TotalAmount: newTotal);
            context.Mutations.PatchMarketOrder(orderId!, patch, state.IsInterShard);
            context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(userId, context.GameTime, newBalance, -fee, "market.fee", new Dictionary<string, object?>(Comparer)
            {
                ["extendOrder"] = new Dictionary<string, object?>
                {
                    ["orderId"] = orderId,
                    ["addAmount"] = addAmount
                }
            }));

            orderMap[orderId!] = state with
            {
                Snapshot = currentOrder with
                {
                    RemainingAmount = newRemaining,
                    TotalAmount = newTotal
                }
            };
        }
    }

    private static Dictionary<string, OrderState> BuildOrderMap(IReadOnlyList<MarketOrderSnapshot> orders)
    {
        var map = new Dictionary<string, OrderState>(StringComparer.Ordinal);
        foreach (var order in orders) {
            if (string.IsNullOrWhiteSpace(order.Id))
                continue;

            var isIntershard = order.ResourceType is not null && IntershardResources.Contains(order.ResourceType);
            map[order.Id] = new OrderState(order, isIntershard);
        }
        return map;
    }

    private static IReadOnlyDictionary<string, RoomObjectSnapshot> BuildTerminalMap(GlobalProcessorContext context)
    {
        var terminals = context.GetObjectsOfType(RoomObjectTypes.Terminal);
        var dictionary = new Dictionary<string, RoomObjectSnapshot>(StringComparer.Ordinal);
        foreach (var terminal in terminals) {
            if (string.IsNullOrWhiteSpace(terminal.RoomName))
                continue;
            dictionary[terminal.RoomName] = terminal;
        }
        return dictionary;
    }

    private static bool TryDebitUser(GlobalProcessorContext context, string userId, double fee, out double newBalance)
    {
        newBalance = 0;
        if (fee <= 0)
            return true;

        if (!context.UsersById.TryGetValue(userId, out var user))
            return false;

        if (user.Money < fee)
            return false;

        newBalance = user.Money - fee;
        context.UsersById[userId] = user with { Money = newBalance };
        context.Mutations.AdjustUserMoney(userId, newBalance);
        return true;
    }

    private UserMoneyLogEntry CreateMoneyLogEntry(
        string userId,
        int tick,
        double balance,
        double change,
        string type,
        IReadOnlyDictionary<string, object?> metadata)
    {
        var timestamp = _timestampProvider();
        return new UserMoneyLogEntry(
            userId,
            DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime,
            tick,
            type,
            balance / 1000.0,
            change / 1000.0,
            metadata);
    }

    private static bool IsValidOrderType(string? type)
        => string.Equals(type, MarketOrderTypes.Buy, StringComparison.OrdinalIgnoreCase) ||
           string.Equals(type, MarketOrderTypes.Sell, StringComparison.OrdinalIgnoreCase);

    private static string NormalizeOrderType(string type)
        => string.Equals(type, MarketOrderTypes.Buy, StringComparison.OrdinalIgnoreCase)
            ? MarketOrderTypes.Buy
            : MarketOrderTypes.Sell;

    private static bool IsValidResource(string? resourceType)
    {
        if (string.IsNullOrWhiteSpace(resourceType))
            return false;

        return ValidResources.Contains(resourceType!) || IntershardResources.Contains(resourceType!);
    }

    private static bool IsRoomTerminalOwnedByUser(string roomName, string userId, IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom)
        => terminalsByRoom.TryGetValue(roomName, out var terminal) &&
           string.Equals(terminal.UserId, userId, StringComparison.Ordinal);

    private static string? GetText(IntentArgument argument, string field)
    {
        if (!argument.Fields.TryGetValue(field, out var value))
            return null;

        return value.Kind switch
        {
            IntentFieldValueKind.Text => value.TextValue,
            IntentFieldValueKind.Number => value.NumberValue?.ToString(),
            IntentFieldValueKind.Boolean => throw new NotImplementedException(),
            IntentFieldValueKind.TextArray => throw new NotImplementedException(),
            IntentFieldValueKind.NumberArray => throw new NotImplementedException(),
            IntentFieldValueKind.BodyPartArray => throw new NotImplementedException(),
            _ => value.TextValue
        };
    }

    private static bool TryGetInt(IntentArgument argument, string field, out int result)
    {
        result = 0;
        if (!argument.Fields.TryGetValue(field, out var value))
            return false;

        return value.Kind switch
        {
            IntentFieldValueKind.Number when value.NumberValue.HasValue => TryConvertInt(value.NumberValue.Value, out result),
            IntentFieldValueKind.Text => throw new NotImplementedException(),
            IntentFieldValueKind.Boolean => throw new NotImplementedException(),
            IntentFieldValueKind.TextArray => throw new NotImplementedException(),
            IntentFieldValueKind.NumberArray => throw new NotImplementedException(),
            IntentFieldValueKind.BodyPartArray => throw new NotImplementedException(),
            _ => false
        };
    }

    private static bool TryConvertInt(int source, out int result)
    {
        result = source;
        return true;
    }

    private static bool TryGetLong(IntentArgument argument, string field, out long result)
    {
        result = 0;
        if (!argument.Fields.TryGetValue(field, out var value))
            return false;

        if (value.Kind == IntentFieldValueKind.Number && value.NumberValue.HasValue) {
            result = value.NumberValue.Value;
            return true;
        }

        return false;
    }

    private sealed record OrderState(MarketOrderSnapshot Snapshot, bool IsInterShard);
}
