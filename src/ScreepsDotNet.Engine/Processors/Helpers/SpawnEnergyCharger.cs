namespace ScreepsDotNet.Engine.Processors.Helpers;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;

internal interface ISpawnEnergyCharger
{
    EnergyChargeResult TryCharge(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        int requiredEnergy,
        IReadOnlyList<string>? preferredStructureIds,
        Dictionary<string, int> energyLedger);
}

internal sealed class SpawnEnergyCharger(ISpawnEnergyAllocator allocator) : ISpawnEnergyCharger
{
    public EnergyChargeResult TryCharge(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        int requiredEnergy,
        IReadOnlyList<string>? preferredStructureIds,
        Dictionary<string, int> energyLedger)
    {
        if (requiredEnergy <= 0)
            return EnergyChargeResult.SuccessResult;

        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(spawn);
        ArgumentNullException.ThrowIfNull(energyLedger);

        var allocation = allocator.AllocateEnergy(
            context.State.Objects,
            spawn,
            requiredEnergy,
            preferredStructureIds,
            energyLedger);

        if (!allocation.Success)
            return EnergyChargeResult.Failure(allocation.Error ?? "Unable to allocate energy.");

        foreach (var draw in allocation.Draws)
            ApplyEnergyDraw(context, draw, energyLedger);

        if (!string.IsNullOrWhiteSpace(spawn.UserId))
            context.Stats.IncrementEnergyCreeps(spawn.UserId!, requiredEnergy);

        return EnergyChargeResult.SuccessResult;
    }

    private static void ApplyEnergyDraw(
        RoomProcessorContext context,
        EnergyDraw draw,
        Dictionary<string, int> energyLedger)
    {
        if (draw.Amount <= 0)
            return;

        var current = energyLedger.TryGetValue(draw.Source.Id, out var overrideValue)
            ? overrideValue
            : GetEnergy(draw.Source);

        var remaining = Math.Max(current - draw.Amount, 0);
        energyLedger[draw.Source.Id] = remaining;

        context.MutationWriter.Patch(draw.Source.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(1, StringComparer.Ordinal)
            {
                [RoomDocumentFields.RoomObject.Store.Energy] = remaining
            }
        });
    }

    private static int GetEnergy(RoomObjectSnapshot obj)
        => obj.Store.GetValueOrDefault(RoomDocumentFields.RoomObject.Store.Energy, 0);
}

internal sealed record EnergyChargeResult(bool Success, string? Error)
{
    public static EnergyChargeResult SuccessResult { get; } = new(true, null);

    public static EnergyChargeResult Failure(string error)
        => new(false, error);
}
