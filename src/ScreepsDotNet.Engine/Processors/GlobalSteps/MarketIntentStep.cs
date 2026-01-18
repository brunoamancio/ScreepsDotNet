namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

using System.Text.RegularExpressions;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Constants;
using ScreepsDotNet.Driver.Contracts;
using static ScreepsDotNet.Common.Constants.MarketIntentFields;

/// <summary>
/// Handles market-related global intents (create/cancel/extend/change price).
/// </summary>
internal sealed partial class MarketIntentStep : IGlobalProcessorStep
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

        ProcessTerminalSends(context, terminalsByRoom);

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
            var orderType = GetText(argument, Type);
            if (!IsValidOrderType(orderType))
                continue;

            var resourceType = GetText(argument, ResourceType);
            if (!IsValidResource(resourceType))
                continue;

            if (!TryGetInt(argument, TotalAmount, out var totalAmount) || totalAmount <= 0)
                continue;

            if (!TryGetLong(argument, Price, out var price) || price <= 0)
                continue;

            var roomName = GetText(argument, RoomName);
            var isIntershard = IntershardResources.Contains(resourceType!);
            if (!isIntershard && string.IsNullOrWhiteSpace(roomName))
                continue;

            if (!isIntershard && !IsRoomTerminalOwnedByUser(roomName!, userId, terminalsByRoom))
                continue;

            var fee = Math.Ceiling(price * totalAmount * ScreepsGameConstants.MarketFee);
            if (!TryDebitUser(context, userId, fee, out var newBalance))
                continue;

            var orderId = Guid.NewGuid().ToString("N");
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
            context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(userId, context.GameTime, newBalance, -fee, MoneyLogTypes.MarketFee, new Dictionary<string, object?>(Comparer)
            {
                [Order] = new Dictionary<string, object?>
                {
                    [ResourceType] = resourceType,
                    [RoomName] = roomName,
                    [Type] = snapshot.Type,
                    [Price] = price / 1000.0,
                    [Amount] = totalAmount
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
            var orderId = GetText(argument, OrderId);
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
            var orderId = GetText(argument, OrderId);
            if (string.IsNullOrWhiteSpace(orderId))
                continue;

            if (!orderMap.TryGetValue(orderId!, out var state))
                continue;

            if (!string.Equals(state.Snapshot.UserId, userId, StringComparison.Ordinal))
                continue;

            if (!TryGetLong(argument, NewPrice, out var newPrice) || newPrice <= 0)
                continue;

            var currentOrder = state.Snapshot;
            var priceDelta = newPrice - currentOrder.Price;
            if (priceDelta > 0) {
                var fee = Math.Ceiling(priceDelta * currentOrder.RemainingAmount * ScreepsGameConstants.MarketFee);
                if (!TryDebitUser(context, userId, fee, out var newBalance))
                    continue;

                context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(userId, context.GameTime, newBalance, -fee, MoneyLogTypes.MarketFee, new Dictionary<string, object?>(Comparer)
                {
                    [ChangeOrderPrice] = new Dictionary<string, object?>
                    {
                        [OrderId] = orderId,
                        [OldPrice] = currentOrder.Price / 1000.0,
                        [NewPrice] = newPrice / 1000.0
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
            var orderId = GetText(argument, OrderId);
            if (string.IsNullOrWhiteSpace(orderId))
                continue;

            if (!orderMap.TryGetValue(orderId!, out var state))
                continue;

            if (!string.Equals(state.Snapshot.UserId, userId, StringComparison.Ordinal))
                continue;

            if (!TryGetInt(argument, AddAmount, out var addAmount) || addAmount <= 0)
                continue;

            var currentOrder = state.Snapshot;
            var fee = Math.Ceiling(currentOrder.Price * addAmount * ScreepsGameConstants.MarketFee);
            if (!TryDebitUser(context, userId, fee, out var newBalance))
                continue;

            var newRemaining = currentOrder.RemainingAmount + addAmount;
            var newTotal = currentOrder.TotalAmount + addAmount;

            var patch = new MarketOrderPatch(RemainingAmount: newRemaining, TotalAmount: newTotal);
            context.Mutations.PatchMarketOrder(orderId!, patch, state.IsInterShard);
            context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(userId, context.GameTime, newBalance, -fee, MoneyLogTypes.MarketFee, new Dictionary<string, object?>(Comparer)
            {
                [ExtendOrder] = new Dictionary<string, object?>
                {
                    [OrderId] = orderId,
                    [AddAmount] = addAmount
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

    private static bool IsValidResource(string? resourceType) => !string.IsNullOrWhiteSpace(resourceType) && (ValidResources.Contains(resourceType!) || IntershardResources.Contains(resourceType!));

    private static bool IsRoomTerminalOwnedByUser(string roomName, string userId, IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom)
        => terminalsByRoom.TryGetValue(roomName, out var terminal) &&
           string.Equals(terminal.UserId, userId, StringComparison.Ordinal);

    private static string? GetText(IntentArgument argument, string field)
    {
        return !argument.Fields.TryGetValue(field, out var value)
            ? null
            : value.Kind switch
            {
                IntentFieldValueKind.Text => value.TextValue,
                IntentFieldValueKind.Number => value.NumberValue?.ToString(),
                _ => value.TextValue
            };
    }

    private static bool TryGetInt(IntentArgument argument, string field, out int result)
    {
        result = 0;
        return argument.Fields.TryGetValue(field, out var value) && value.Kind switch
        {
            IntentFieldValueKind.Number when value.NumberValue.HasValue => TryConvertInt(value.NumberValue.Value, out result),
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

    private void ProcessTerminalSends(GlobalProcessorContext context, IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom)
    {
        var terminals = context.GetObjectsOfType(RoomObjectTypes.Terminal);
        foreach (var terminal in terminals) {
            var sendIntent = TryExtractSendIntent(terminal);
            if (sendIntent is null)
                continue;

            var patch = new GlobalRoomObjectPatch(ClearSend: true);
            context.Mutations.PatchRoomObject(terminal.Id, patch);

            var cooldownTime = terminal.CooldownTime ?? 0;
            if (cooldownTime > context.GameTime)
                continue;

            if (string.IsNullOrWhiteSpace(sendIntent.TargetRoomName))
                continue;

            if (!terminalsByRoom.TryGetValue(sendIntent.TargetRoomName, out var targetTerminal))
                continue;

            if (string.IsNullOrWhiteSpace(targetTerminal.UserId))
                continue;

            var cooldown = ScreepsGameConstants.TerminalCooldown;
            var operateEffect = GetPowerEffect(terminal, PowerTypes.OperateTerminal, context.GameTime);
            if (operateEffect.HasValue) {
                var effectMultiplier = GetPowerEffectMultiplier(PowerTypes.OperateTerminal, operateEffect.Value.Level);
                var result = Math.Round(cooldown * effectMultiplier);
                cooldown = (int)result;
            }

            var transferSuccess = ExecuteTerminalTransfer(
                terminal,
                targetTerminal,
                sendIntent.ResourceType,
                sendIntent.Amount,
                terminal,
                sendIntent.Description,
                context,
                terminalsByRoom);

            if (transferSuccess) {
                var newCooldownTime = context.GameTime + cooldown;
                var cooldownPatch = new GlobalRoomObjectPatch(CooldownTime: newCooldownTime);
                context.Mutations.PatchRoomObject(terminal.Id, cooldownPatch);
            }
        }
    }

    private bool ExecuteTerminalTransfer(
        RoomObjectSnapshot fromTerminal,
        RoomObjectSnapshot toTerminal,
        string resourceType,
        int amount,
        RoomObjectSnapshot transferFeeTerminal,
        string? description,
        GlobalProcessorContext context,
        IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom)
    {
        if (!string.IsNullOrWhiteSpace(fromTerminal.UserId)) {
            if (!fromTerminal.Store.TryGetValue(resourceType, out var available) || available < amount)
                return false;
        }

        if (!string.IsNullOrWhiteSpace(toTerminal.UserId)) {
            var targetResourceTotal = CalculateResourceTotal(toTerminal);
            var freeSpace = Math.Max(0, toTerminal.StoreCapacity.GetValueOrDefault() - targetResourceTotal);
            amount = Math.Min(amount, freeSpace);
        }

        if (amount <= 0)
            return false;

        var range = CalculateRoomDistance(fromTerminal.RoomName, toTerminal.RoomName, continuous: true);
        var transferCost = CalculateTerminalEnergyCost(amount, range);

        var operateEffect = GetPowerEffect(transferFeeTerminal, PowerTypes.OperateTerminal, context.GameTime);
        if (operateEffect.HasValue) {
            var effectMultiplier = GetPowerEffectMultiplier(PowerTypes.OperateTerminal, operateEffect.Value.Level);
            var result = Math.Ceiling(transferCost * effectMultiplier);
            transferCost = (int)result;
        }

        var energyAvailable = fromTerminal.Store.GetValueOrDefault(ResourceTypes.Energy, 0);
        var isEnergyTransfer = string.Equals(resourceType, ResourceTypes.Energy, StringComparison.Ordinal);
        var hasSufficientEnergy = transferFeeTerminal == fromTerminal
            ? (isEnergyTransfer ? energyAvailable >= amount + transferCost : energyAvailable >= transferCost)
            : toTerminal.Store.TryGetValue(ResourceTypes.Energy, out var targetEnergy) && targetEnergy >= transferCost;

        if (!hasSufficientEnergy)
            return false;

        if (!string.IsNullOrWhiteSpace(toTerminal.UserId)) {
            var newTargetAmount = toTerminal.Store.TryGetValue(resourceType, out var current) ? current + amount : amount;
            var targetPatch = new GlobalRoomObjectPatch(Store: new Dictionary<string, int> { [resourceType] = newTargetAmount });
            context.Mutations.PatchRoomObject(toTerminal.Id, targetPatch);
        }

        var newSourceAmount = fromTerminal.Store.TryGetValue(resourceType, out var sourceAmount) ? sourceAmount - amount : 0;
        var sourcePatch = new GlobalRoomObjectPatch(Store: new Dictionary<string, int> { [resourceType] = newSourceAmount });
        context.Mutations.PatchRoomObject(fromTerminal.Id, sourcePatch);

        var newFeeEnergy = fromTerminal.Store.TryGetValue(ResourceTypes.Energy, out var feeEnergy) ? feeEnergy - transferCost : 0;
        var feePatch = new GlobalRoomObjectPatch(Store: new Dictionary<string, int> { [ResourceTypes.Energy] = newFeeEnergy });
        context.Mutations.PatchRoomObject(transferFeeTerminal.Id, feePatch);

        var sanitizedDescription = description?.Replace("<", "&lt;");
        var timestamp = _timestampProvider();
        var transaction = new TransactionLogEntry(
            DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime,
            context.GameTime,
            !string.IsNullOrWhiteSpace(fromTerminal.UserId) ? fromTerminal.UserId : null,
            !string.IsNullOrWhiteSpace(toTerminal.UserId) ? toTerminal.UserId : null,
            resourceType,
            amount,
            fromTerminal.RoomName,
            toTerminal.RoomName,
            OrderId: null,
            sanitizedDescription);

        context.Mutations.InsertTransaction(transaction);
        return true;
    }

    private static int CalculateResourceTotal(RoomObjectSnapshot terminal)
    {
        var total = 0;
        foreach (var kvp in terminal.Store)
            total += kvp.Value;
        return total;
    }

    private static int CalculateRoomDistance(string roomName1, string roomName2, bool continuous)
    {
        var coords1 = ParseRoomCoordinates(roomName1);
        var coords2 = ParseRoomCoordinates(roomName2);

        if (!coords1.HasValue || !coords2.HasValue)
            return 0;

        var dx = Math.Abs(coords2.Value.X - coords1.Value.X);
        var dy = Math.Abs(coords2.Value.Y - coords1.Value.Y);

        if (continuous) {
            const int worldSize = 255;
            dx = Math.Min(worldSize - dx, dx);
            dy = Math.Min(worldSize - dy, dy);
        }

        var distance = Math.Max(dx, dy);
        return distance;
    }

    private static (int X, int Y)? ParseRoomCoordinates(string roomName)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return null;

        var match = RoomNameRegex().Match(roomName);
        if (!match.Success)
            return null;

        var xDir = match.Groups[1].Value;
        var xValue = int.Parse(match.Groups[2].Value);
        var yDir = match.Groups[3].Value;
        var yValue = int.Parse(match.Groups[4].Value);

        var x = string.Equals(xDir, "W", StringComparison.Ordinal) ? -xValue - 1 : xValue;
        var y = string.Equals(yDir, "N", StringComparison.Ordinal) ? -yValue - 1 : yValue;

        return (x, y);
    }

    private static int CalculateTerminalEnergyCost(int amount, int range)
    {
        var cost = amount * (1 - Math.Exp(-range / 30.0));
        var result = (int)Math.Ceiling(cost);
        return result;
    }

    private static (int Level, int EndTime)? GetPowerEffect(RoomObjectSnapshot obj, PowerTypes powerType, int gameTime)
    {
        if (obj.Effects.Count == 0)
            return null;

        foreach (var kvp in obj.Effects) {
            var effect = kvp.Value;

            if (effect.Power != (int)powerType)
                continue;

            if (effect.EndTime <= gameTime)
                continue;

            return (effect.Level, effect.EndTime);
        }

        return null;
    }

    private static double GetPowerEffectMultiplier(PowerTypes powerType, int level)
    {
        if (!PowerInfo.Abilities.TryGetValue(powerType, out var abilityInfo))
            return 1.0;

        if (abilityInfo.EffectMultipliers is null)
            return 1.0;

        var index = level - 1;
        if (index < 0 || index >= abilityInfo.EffectMultipliers.Length)
            return 1.0;

        var multiplier = abilityInfo.EffectMultipliers[index];
        return multiplier;
    }

    private static TerminalSendSnapshot? TryExtractSendIntent(RoomObjectSnapshot terminal)
        => terminal.Send;

    [GeneratedRegex(@"^([WE])(\d+)([NS])(\d+)$")]
    private static partial Regex RoomNameRegex();

    private sealed record OrderState(MarketOrderSnapshot Snapshot, bool IsInterShard);
}
