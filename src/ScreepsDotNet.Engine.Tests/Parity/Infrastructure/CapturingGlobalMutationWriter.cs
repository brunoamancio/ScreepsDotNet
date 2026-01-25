namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;

/// <summary>
/// Test implementation of IGlobalMutationWriter that captures all mutations in memory for parity comparison.
/// Used by multi-room parity tests to validate global processor output against Node.js engine.
/// </summary>
public sealed class CapturingGlobalMutationWriter : IGlobalMutationWriter
{
    public List<(string PowerCreepId, PowerCreepMutationPatch Patch)> PowerCreepPatches { get; } = [];
    public List<string> PowerCreepRemovals { get; } = [];
    public List<PowerCreepSnapshot> PowerCreepUpserts { get; } = [];
    public List<(MarketOrderSnapshot Snapshot, bool IsInterShard)> MarketOrderUpserts { get; } = [];
    public List<(string OrderId, MarketOrderPatch Patch, bool IsInterShard)> MarketOrderPatches { get; } = [];
    public List<(string OrderId, bool IsInterShard)> MarketOrderRemovals { get; } = [];
    public List<(string UserId, double NewBalance)> UserMoneyAdjustments { get; } = [];
    public List<UserMoneyLogEntry> UserMoneyLogs { get; } = [];
    public List<RoomObjectSnapshot> RoomObjectUpserts { get; } = [];
    public List<(string ObjectId, GlobalRoomObjectPatch Patch)> RoomObjectPatches { get; } = [];
    public List<string> RoomObjectRemovals { get; } = [];
    public List<TransactionLogEntry> Transactions { get; } = [];
    public List<(string UserId, string ResourceType, int NewBalance)> UserResourceAdjustments { get; } = [];
    public List<UserResourceLogEntry> UserResourceLogs { get; } = [];
    public List<(string UserId, int Amount)> UserGclIncrements { get; } = [];
    public List<(string UserId, double Amount)> UserPowerIncrements { get; } = [];
    public List<(string UserId, double Amount)> UserPowerDecrements { get; } = [];

    public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch)
        => PowerCreepPatches.Add((powerCreepId, patch));

    public void RemovePowerCreep(string powerCreepId)
        => PowerCreepRemovals.Add(powerCreepId);

    public void UpsertPowerCreep(PowerCreepSnapshot snapshot)
        => PowerCreepUpserts.Add(snapshot);

    public void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard)
        => MarketOrderUpserts.Add((snapshot, isInterShard));

    public void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard)
        => MarketOrderPatches.Add((orderId, patch, isInterShard));

    public void RemoveMarketOrder(string orderId, bool isInterShard)
        => MarketOrderRemovals.Add((orderId, isInterShard));

    public void AdjustUserMoney(string userId, double newBalance)
        => UserMoneyAdjustments.Add((userId, newBalance));

    public void InsertUserMoneyLog(UserMoneyLogEntry entry)
        => UserMoneyLogs.Add(entry);

    public void UpsertRoomObject(RoomObjectSnapshot snapshot)
        => RoomObjectUpserts.Add(snapshot);

    public void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch)
        => RoomObjectPatches.Add((objectId, patch));

    public void RemoveRoomObject(string objectId)
        => RoomObjectRemovals.Add(objectId);

    public void InsertTransaction(TransactionLogEntry entry)
        => Transactions.Add(entry);

    public void AdjustUserResource(string userId, string resourceType, int newBalance)
        => UserResourceAdjustments.Add((userId, resourceType, newBalance));

    public void InsertUserResourceLog(UserResourceLogEntry entry)
        => UserResourceLogs.Add(entry);

    public void IncrementUserGcl(string userId, int amount)
        => UserGclIncrements.Add((userId, amount));

    public void IncrementUserPower(string userId, double amount)
        => UserPowerIncrements.Add((userId, amount));

    public void DecrementUserPower(string userId, double amount)
        => UserPowerDecrements.Add((userId, amount));

    public Task FlushAsync(CancellationToken token = default)
        => Task.CompletedTask;  // Capturing writer doesn't persist, just collects in memory

    public void Reset()
    {
        PowerCreepPatches.Clear();
        PowerCreepRemovals.Clear();
        PowerCreepUpserts.Clear();
        MarketOrderUpserts.Clear();
        MarketOrderPatches.Clear();
        MarketOrderRemovals.Clear();
        UserMoneyAdjustments.Clear();
        UserMoneyLogs.Clear();
        RoomObjectUpserts.Clear();
        RoomObjectPatches.Clear();
        RoomObjectRemovals.Clear();
        Transactions.Clear();
        UserResourceAdjustments.Clear();
        UserResourceLogs.Clear();
        UserGclIncrements.Clear();
        UserPowerIncrements.Clear();
        UserPowerDecrements.Clear();
    }
}
