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
    private const long MarketOrderLifeTimeMs = 1000L * 60 * 60 * 24 * 30; // 30 days
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

        var terminalDeals = new List<DealIntent>();
        var directDeals = new List<DealIntent>();

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
                    case GlobalIntentTypes.Deal:
                        ProcessDealIntents(record.Arguments, userId, orderMap, terminalsByRoom, terminalDeals, directDeals);
                        break;
                    default:
                        break;
                }
            }
        }

        ProcessTerminalSends(context, terminalsByRoom);
        ProcessTerminalDeals(context, terminalDeals, terminalsByRoom, orderMap);
        ProcessDirectDeals(context, directDeals, orderMap);
        ProcessOrderMaintenance(context, orderMap, terminalsByRoom);

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

    private UserResourceLogEntry CreateResourceLogEntry(
        string resourceType,
        string userId,
        int change,
        int balance,
        IReadOnlyDictionary<string, object?>? metadata = null)
    {
        var timestamp = _timestampProvider();
        return new UserResourceLogEntry(
            DateTimeOffset.FromUnixTimeMilliseconds(timestamp).UtcDateTime,
            userId,
            resourceType,
            change,
            balance,
            null,
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

    private static void ProcessDealIntents(
        IReadOnlyList<IntentArgument> arguments,
        string userId,
        Dictionary<string, OrderState> orderMap,
        IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom,
        List<DealIntent> terminalDeals,
        List<DealIntent> directDeals)
    {
        foreach (var argument in arguments) {
            var orderId = GetText(argument, OrderId);
            if (string.IsNullOrWhiteSpace(orderId) || !orderMap.TryGetValue(orderId, out var orderState))
                continue;

            if (!TryGetInt(argument, Amount, out var amount) || amount <= 0)
                continue;

            var order = orderState.Snapshot;
            if (order.ResourceType is null)
                continue;

            var deal = new DealIntent(
                UserId: userId,
                OrderId: orderId!,
                Amount: amount,
                TargetRoomName: GetText(argument, TargetRoomName) ?? string.Empty);

            if (IntershardResources.Contains(order.ResourceType)) {
                directDeals.Add(deal);
            }
            else {
                if (!terminalsByRoom.TryGetValue(deal.TargetRoomName, out var terminal))
                    continue;

                if (!string.Equals(terminal.UserId, userId, StringComparison.Ordinal))
                    continue;

                terminalDeals.Add(deal);
            }
        }
    }

    private void ProcessTerminalDeals(
        GlobalProcessorContext context,
        List<DealIntent> terminalDeals,
        IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom,
        Dictionary<string, OrderState> orderMap)
    {
        terminalDeals.Sort((a, b) => {
            if (!orderMap.TryGetValue(a.OrderId, out var orderA) || string.IsNullOrWhiteSpace(orderA.Snapshot.RoomName))
                return 1;
            if (!orderMap.TryGetValue(b.OrderId, out var orderB) || string.IsNullOrWhiteSpace(orderB.Snapshot.RoomName))
                return -1;

            var distanceA = CalculateRoomDistance(a.TargetRoomName, orderA.Snapshot.RoomName, continuous: true);
            var distanceB = CalculateRoomDistance(b.TargetRoomName, orderB.Snapshot.RoomName, continuous: true);
            return distanceA.CompareTo(distanceB);
        });

        foreach (var deal in terminalDeals) {
            if (!orderMap.TryGetValue(deal.OrderId, out var orderState))
                continue;

            var order = orderState.Snapshot;
            if (!terminalsByRoom.TryGetValue(order.RoomName ?? string.Empty, out var orderTerminal))
                continue;

            if (!terminalsByRoom.TryGetValue(deal.TargetRoomName, out var targetTerminal))
                continue;

            if (targetTerminal.CooldownTime > context.GameTime)
                continue;

            var isSellOrder = string.Equals(order.Type, MarketOrderTypes.Sell, StringComparison.Ordinal);
            var buyer = isSellOrder ? targetTerminal : orderTerminal;
            var seller = isSellOrder ? orderTerminal : targetTerminal;

            var amount = Math.Min(deal.Amount, order.RemainingAmount);

            if (!string.IsNullOrWhiteSpace(seller.UserId)) {
                var sellerResource = seller.Store.GetValueOrDefault(order.ResourceType!, 0);
                amount = Math.Min(amount, sellerResource);
            }

            if (!string.IsNullOrWhiteSpace(buyer.UserId)) {
                var buyerTotal = CalculateResourceTotal(buyer);
                var buyerFreeSpace = Math.Max(0, buyer.StoreCapacity.GetValueOrDefault() - buyerTotal);
                amount = Math.Min(amount, buyerFreeSpace);
            }

            if (amount <= 0)
                continue;

            var dealCost = amount * order.Price;

            if (!string.IsNullOrWhiteSpace(buyer.UserId)) {
                if (!context.UsersById.TryGetValue(buyer.UserId, out var buyerUser))
                    continue;

                dealCost = Math.Min(dealCost, (long)buyerUser.Money);
                amount = (int)Math.Floor((double)dealCost / order.Price);
                dealCost = amount * order.Price;

                if (amount <= 0)
                    continue;
            }

            var orderDescription = $"{{\"order\":{{\"id\":\"{order.Id}\",\"type\":\"{order.Type}\",\"price\":{order.Price / 1000.0}}}}}";

            var transferSuccess = ExecuteTerminalTransfer(
                seller,
                buyer,
                order.ResourceType!,
                amount,
                targetTerminal,
                orderDescription,
                context,
                terminalsByRoom);

            if (!transferSuccess)
                continue;

            if (!string.IsNullOrWhiteSpace(seller.UserId)) {
                var newBalance = context.UsersById[seller.UserId].Money + dealCost;
                context.Mutations.AdjustUserMoney(seller.UserId, newBalance);

                context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(
                    seller.UserId,
                    context.GameTime,
                    newBalance,
                    dealCost,
                    MoneyLogTypes.MarketSell,
                    new Dictionary<string, object?>(Comparer)
                    {
                        [Market] = new Dictionary<string, object?>
                        {
                            [ResourceType] = order.ResourceType!,
                            [RoomName] = order.RoomName,
                            [TargetRoomName] = deal.TargetRoomName,
                            [Price] = order.Price / 1000.0,
                            [Npc] = string.IsNullOrWhiteSpace(buyer.UserId),
                            [Owner] = order.UserId,
                            [Dealer] = deal.UserId,
                            [Amount] = amount
                        }
                    }));

                context.UsersById[seller.UserId] = context.UsersById[seller.UserId] with { Money = newBalance };
            }

            if (!string.IsNullOrWhiteSpace(buyer.UserId)) {
                var newBalance = context.UsersById[buyer.UserId].Money - dealCost;
                context.Mutations.AdjustUserMoney(buyer.UserId, newBalance);

                context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(
                    buyer.UserId,
                    context.GameTime,
                    newBalance,
                    -dealCost,
                    MoneyLogTypes.MarketBuy,
                    new Dictionary<string, object?>(Comparer)
                    {
                        [Market] = new Dictionary<string, object?>
                        {
                            [ResourceType] = order.ResourceType!,
                            [RoomName] = order.RoomName,
                            [TargetRoomName] = deal.TargetRoomName,
                            [Price] = order.Price / 1000.0,
                            [Npc] = string.IsNullOrWhiteSpace(seller.UserId),
                            [Owner] = order.UserId,
                            [Dealer] = deal.UserId,
                            [Amount] = amount
                        }
                    }));

                context.UsersById[buyer.UserId] = context.UsersById[buyer.UserId] with { Money = newBalance };
            }

            var newAmount = order.Amount - amount;
            var newRemainingAmount = order.RemainingAmount - amount;
            var patch = new MarketOrderPatch(Amount: newAmount, RemainingAmount: newRemainingAmount);
            context.Mutations.PatchMarketOrder(order.Id, patch, orderState.IsInterShard);

            orderMap[order.Id] = orderState with { Snapshot = orderState.Snapshot with { Amount = newAmount, RemainingAmount = newRemainingAmount } };

            var cooldown = ScreepsGameConstants.TerminalCooldown;
            var operateEffect = GetPowerEffect(targetTerminal, PowerTypes.OperateTerminal, context.GameTime);
            if (operateEffect.HasValue) {
                var effectMultiplier = GetPowerEffectMultiplier(PowerTypes.OperateTerminal, operateEffect.Value.Level);
                var result = Math.Round(cooldown * effectMultiplier);
                cooldown = (int)result;
            }

            var newCooldownTime = context.GameTime + cooldown;
            context.Mutations.PatchRoomObject(targetTerminal.Id, new GlobalRoomObjectPatch(CooldownTime: newCooldownTime));
        }
    }

    private void ProcessDirectDeals(
        GlobalProcessorContext context,
        List<DealIntent> directDeals,
        Dictionary<string, OrderState> orderMap)
    {
        var random = new Random();
        var shuffled = directDeals.OrderBy(_ => random.Next()).ToList();

        foreach (var deal in shuffled) {
            if (!orderMap.TryGetValue(deal.OrderId, out var orderState))
                continue;

            var order = orderState.Snapshot;

            if (!context.UsersById.TryGetValue(deal.UserId, out var dealerUser))
                continue;

            if (!context.UsersById.TryGetValue(order.UserId ?? string.Empty, out var orderOwnerUser))
                continue;

            var isSellOrder = string.Equals(order.Type, MarketOrderTypes.Sell, StringComparison.Ordinal);
            var buyer = isSellOrder ? dealerUser : orderOwnerUser;
            var seller = isSellOrder ? orderOwnerUser : dealerUser;
            var buyerId = isSellOrder ? deal.UserId : order.UserId ?? string.Empty;
            var sellerId = isSellOrder ? order.UserId ?? string.Empty : deal.UserId;

            var sellerResource = seller.Resources.GetValueOrDefault(order.ResourceType!, 0);
            var amount = Math.Min(deal.Amount, Math.Min(order.Amount, Math.Min(order.RemainingAmount, sellerResource)));

            if (amount <= 0)
                continue;

            var dealCost = amount * order.Price;

            if (buyer.Money < dealCost)
                continue;

            var sellerNewMoney = seller.Money + dealCost;
            var sellerNewResource = seller.Resources.GetValueOrDefault(order.ResourceType!, 0) - amount;
            context.Mutations.AdjustUserMoney(sellerId, sellerNewMoney);
            context.Mutations.AdjustUserResource(sellerId, order.ResourceType!, sellerNewResource);

            context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(
                sellerId,
                context.GameTime,
                sellerNewMoney,
                dealCost,
                MoneyLogTypes.MarketSell,
                new Dictionary<string, object?>(Comparer)
                {
                    [Market] = new Dictionary<string, object?>
                    {
                        [ResourceType] = order.ResourceType!,
                        [Price] = order.Price / 1000.0,
                        [Amount] = amount
                    }
                }));

            context.Mutations.InsertUserResourceLog(CreateResourceLogEntry(
                order.ResourceType!,
                sellerId,
                -amount,
                sellerNewResource,
                new Dictionary<string, object?>(Comparer)
                {
                    [Market] = new Dictionary<string, object?>
                    {
                        [OrderId] = order.Id,
                        [AnotherUser] = buyerId
                    }
                }));

            var buyerNewMoney = buyer.Money - dealCost;
            var buyerNewResource = buyer.Resources.GetValueOrDefault(order.ResourceType!, 0) + amount;
            context.Mutations.AdjustUserMoney(buyerId, buyerNewMoney);
            context.Mutations.AdjustUserResource(buyerId, order.ResourceType!, buyerNewResource);

            context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(
                buyerId,
                context.GameTime,
                buyerNewMoney,
                -dealCost,
                MoneyLogTypes.MarketBuy,
                new Dictionary<string, object?>(Comparer)
                {
                    [Market] = new Dictionary<string, object?>
                    {
                        [ResourceType] = order.ResourceType!,
                        [Price] = order.Price / 1000.0,
                        [Amount] = amount
                    }
                }));

            context.Mutations.InsertUserResourceLog(CreateResourceLogEntry(
                order.ResourceType!,
                buyerId,
                amount,
                buyerNewResource,
                new Dictionary<string, object?>(Comparer)
                {
                    [Market] = new Dictionary<string, object?>
                    {
                        [OrderId] = order.Id,
                        [AnotherUser] = sellerId
                    }
                }));

            var newAmount = order.Amount - amount;
            var newRemainingAmount = order.RemainingAmount - amount;
            var patch = new MarketOrderPatch(Amount: newAmount, RemainingAmount: newRemainingAmount);
            context.Mutations.PatchMarketOrder(order.Id, patch, orderState.IsInterShard);

            orderMap[order.Id] = orderState with { Snapshot = orderState.Snapshot with { Amount = newAmount, RemainingAmount = newRemainingAmount } };

            context.UsersById[sellerId] = context.UsersById[sellerId] with
            {
                Money = sellerNewMoney,
                Resources = new Dictionary<string, int>(seller.Resources, Comparer)
                {
                    [order.ResourceType!] = sellerNewResource
                }
            };

            context.UsersById[buyerId] = context.UsersById[buyerId] with
            {
                Money = buyerNewMoney,
                Resources = new Dictionary<string, int>(buyer.Resources, Comparer)
                {
                    [order.ResourceType!] = buyerNewResource
                }
            };
        }
    }

    private void ProcessOrderMaintenance(
        GlobalProcessorContext context,
        Dictionary<string, OrderState> orderMap,
        IReadOnlyDictionary<string, RoomObjectSnapshot> terminalsByRoom)
    {
        foreach (var (orderId, orderState) in orderMap) {
            var order = orderState.Snapshot;

            if (string.IsNullOrWhiteSpace(order.UserId))
                continue;

            if (order.CreatedTimestamp.HasValue) {
                var currentTimestamp = _timestampProvider();
                var age = currentTimestamp - order.CreatedTimestamp.Value;

                if (age > MarketOrderLifeTimeMs) {
                    var feeRefund = order.RemainingAmount * order.Price * ScreepsGameConstants.MarketFee;

                    var user = context.UsersById[order.UserId];
                    var newBalance = user.Money + feeRefund;
                    context.Mutations.AdjustUserMoney(order.UserId, newBalance);

                    context.Mutations.InsertUserMoneyLog(CreateMoneyLogEntry(
                        order.UserId,
                        context.GameTime,
                        newBalance,
                        feeRefund,
                        MoneyLogTypes.MarketFee,
                        new Dictionary<string, object?>(Comparer)
                        {
                            [Market] = new Dictionary<string, object?>
                            {
                                [Order] = new Dictionary<string, object?>
                                {
                                    [Type] = order.Type,
                                    [ResourceType] = order.ResourceType,
                                    [Price] = order.Price / 1000.0,
                                    [TotalAmount] = order.TotalAmount,
                                    [RoomName] = order.RoomName
                                }
                            }
                        }));

                    context.Mutations.RemoveMarketOrder(order.Id, orderState.IsInterShard);

                    context.UsersById[order.UserId] = user with { Money = newBalance };

                    continue;
                }
            }

            if (string.Equals(order.Type, MarketOrderTypes.Sell, StringComparison.Ordinal)) {
                int availableAmount;

                if (IntershardResources.Contains(order.ResourceType!)) {
                    var user = context.UsersById[order.UserId];
                    var result = user.Resources.TryGetValue(order.ResourceType!, out var amount);
                    availableAmount = result ? amount : 0;
                }
                else {
                    var hasTerminal = terminalsByRoom.TryGetValue(order.RoomName!, out var terminal);
                    var ownsTerminal = hasTerminal && terminal is not null && string.Equals(terminal.UserId, order.UserId, StringComparison.Ordinal);

                    availableAmount = ownsTerminal && terminal!.Store.TryGetValue(order.ResourceType!, out var storeAmount) ? storeAmount : 0;
                }

                var newActive = availableAmount > 0;
                var newAmount = availableAmount;

                if (order.Active != newActive || order.Amount != newAmount) {
                    var patch = new MarketOrderPatch(Active: newActive, Amount: newAmount);
                    context.Mutations.PatchMarketOrder(order.Id, patch, orderState.IsInterShard);
                }
            }

            if (string.Equals(order.Type, MarketOrderTypes.Buy, StringComparison.Ordinal)) {
                var user = context.UsersById[order.UserId];

                var affordableAmount = (int)Math.Floor(user.Money / order.Price);

                var newAmount = Math.Min(affordableAmount, order.RemainingAmount);

                var hasTerminal = terminalsByRoom.TryGetValue(order.RoomName!, out var terminal);
                var ownsTerminal = hasTerminal && terminal is not null && string.Equals(terminal.UserId, order.UserId, StringComparison.Ordinal);

                if (ownsTerminal) {
                    var storeTotal = terminal!.Store.Values.Sum();
                    var freeSpace = Math.Max(0, terminal.StoreCapacity.GetValueOrDefault() - storeTotal);
                    newAmount = Math.Min(newAmount, freeSpace);
                }

                var newActive = ownsTerminal && newAmount > 0;

                if (order.Active != newActive || order.Amount != newAmount) {
                    var patch = new MarketOrderPatch(Active: newActive, Amount: newAmount);
                    context.Mutations.PatchMarketOrder(order.Id, patch, orderState.IsInterShard);
                }
            }
        }
    }

    [GeneratedRegex(@"^([WE])(\d+)([NS])(\d+)$")]
    private static partial Regex RoomNameRegex();

    private sealed record OrderState(MarketOrderSnapshot Snapshot, bool IsInterShard);
    private sealed record DealIntent(string UserId, string OrderId, int Amount, string TargetRoomName);
}
