namespace ScreepsDotNet.Engine.Tests.Processors.Helpers;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Data.GlobalMutations;

/// <summary>
/// Null object pattern for IGlobalMutationWriter - discards all mutations.
/// Use when tests don't need to verify global mutation behavior.
/// </summary>
internal sealed class NullGlobalMutationWriter : IGlobalMutationWriter
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
