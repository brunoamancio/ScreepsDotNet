using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;

namespace ScreepsDotNet.Engine.Tests.Processors.GlobalSteps;

internal sealed class RecordingGlobalMutationWriter : IGlobalMutationWriter
{
    public List<(string Id, PowerCreepMutationPatch Patch)> PowerCreepPatches { get; } = [];
    public List<string> RemovedPowerCreeps { get; } = [];
    public List<MarketOrderMutation> MarketOrderMutations { get; } = [];
    public List<UserMoneyMutation> UserMoneyMutations { get; } = [];
    public List<UserMoneyLogEntry> UserMoneyLogs { get; } = [];
    public List<RoomObjectMutation> RoomObjectMutations { get; } = [];
    public List<TransactionLogEntry> TransactionLogs { get; } = [];
    public List<UserResourceMutation> UserResourceMutations { get; } = [];
    public List<UserResourceLogEntry> UserResourceLogs { get; } = [];

    public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch)
        => PowerCreepPatches.Add((powerCreepId, patch));

    public void RemovePowerCreep(string powerCreepId)
        => RemovedPowerCreeps.Add(powerCreepId);

    public void UpsertPowerCreep(PowerCreepSnapshot snapshot)
    {
    }

    public void UpsertMarketOrder(MarketOrderSnapshot snapshot, bool isInterShard)
        => MarketOrderMutations.Add(new MarketOrderMutation(snapshot.Id, MarketOrderMutationType.Upsert, isInterShard, snapshot));

    public void PatchMarketOrder(string orderId, MarketOrderPatch patch, bool isInterShard)
        => MarketOrderMutations.Add(new MarketOrderMutation(orderId, MarketOrderMutationType.Patch, isInterShard, Patch: patch));

    public void RemoveMarketOrder(string orderId, bool isInterShard)
        => MarketOrderMutations.Add(new MarketOrderMutation(orderId, MarketOrderMutationType.Remove, isInterShard));

    public void AdjustUserMoney(string userId, double newBalance)
        => UserMoneyMutations.Add(new UserMoneyMutation(userId, newBalance));

    public void InsertUserMoneyLog(UserMoneyLogEntry entry)
        => UserMoneyLogs.Add(entry);

    public void UpsertRoomObject(RoomObjectSnapshot snapshot)
        => RoomObjectMutations.Add(new RoomObjectMutation(snapshot.Id, RoomObjectMutationType.Upsert, snapshot));

    public void PatchRoomObject(string objectId, GlobalRoomObjectPatch patch)
    {
        // Test implementation - just record that a patch was requested
        // In real implementation, this would convert to BsonDocument
    }

    public void RemoveRoomObject(string objectId)
        => RoomObjectMutations.Add(new RoomObjectMutation(objectId, RoomObjectMutationType.Remove));

    public void InsertTransaction(TransactionLogEntry entry)
        => TransactionLogs.Add(entry);

    public void AdjustUserResource(string userId, string resourceType, int newBalance)
        => UserResourceMutations.Add(new UserResourceMutation(userId, resourceType, newBalance));

    public void InsertUserResourceLog(UserResourceLogEntry entry)
        => UserResourceLogs.Add(entry);

    public Task FlushAsync(CancellationToken token = default) => Task.CompletedTask;

    public void Reset()
    {
        PowerCreepPatches.Clear();
        RemovedPowerCreeps.Clear();
        MarketOrderMutations.Clear();
        UserMoneyMutations.Clear();
        UserMoneyLogs.Clear();
        RoomObjectMutations.Clear();
        TransactionLogs.Clear();
        UserResourceMutations.Clear();
        UserResourceLogs.Clear();
    }
}
