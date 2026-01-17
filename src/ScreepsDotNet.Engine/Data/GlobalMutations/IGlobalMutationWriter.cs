namespace ScreepsDotNet.Engine.Data.GlobalMutations;

using ScreepsDotNet.Driver.Contracts;

public interface IGlobalMutationWriter
{
    void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch);
    void RemovePowerCreep(string powerCreepId);
    void UpsertPowerCreep(PowerCreepSnapshot snapshot);
    Task FlushAsync(CancellationToken token = default);
    void Reset();
}
