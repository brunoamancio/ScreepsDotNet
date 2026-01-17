namespace ScreepsDotNet.Engine.Data.GlobalMutations;

using ScreepsDotNet.Driver.Abstractions.GlobalProcessing;
using ScreepsDotNet.Driver.Contracts;

internal sealed class GlobalMutationWriter(IGlobalMutationDispatcher dispatcher) : IGlobalMutationWriter
{
    private readonly List<PowerCreepMutation> _powerCreepMutations = [];

    public void PatchPowerCreep(string powerCreepId, PowerCreepMutationPatch patch)
    {
        if (string.IsNullOrWhiteSpace(powerCreepId))
            return;

        _powerCreepMutations.Add(new PowerCreepMutation(powerCreepId, PowerCreepMutationType.Patch, Patch: patch));
    }

    public void RemovePowerCreep(string powerCreepId)
    {
        if (string.IsNullOrWhiteSpace(powerCreepId))
            return;

        _powerCreepMutations.Add(new PowerCreepMutation(powerCreepId, PowerCreepMutationType.Remove));
    }

    public void UpsertPowerCreep(PowerCreepSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        _powerCreepMutations.Add(new PowerCreepMutation(snapshot.Id, PowerCreepMutationType.Upsert, Snapshot: snapshot));
    }

    public async Task FlushAsync(CancellationToken token = default)
    {
        if (_powerCreepMutations.Count == 0)
            return;

        var batch = new GlobalMutationBatch(_powerCreepMutations.ToArray());
        await dispatcher.ApplyAsync(batch, token).ConfigureAwait(false);
        Reset();
    }

    public void Reset() => _powerCreepMutations.Clear();
}
