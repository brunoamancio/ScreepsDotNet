namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Structures;
using ScreepsDotNet.Common.Utilities;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

internal sealed class CreepBuildRepairStep(IStructureBlueprintProvider blueprintProvider, IStructureSnapshotFactory structureFactory) : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var energyLedger = new Dictionary<string, int>(Comparer);
        var objectOverrides = new Dictionary<string, ObjectStateOverride>(Comparer);
        var terrainCache = BuildTerrainCache(context);

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var creep))
                    continue;

                if (!creep.IsCreep(includePowerCreep: false))
                    continue;

                if (creep.IsSpawning == true)
                    continue;

                if (!string.Equals(creep.UserId, envelope.UserId, StringComparison.Ordinal))
                    continue;

                foreach (var record in records) {
                    switch (record.Name) {
                        case IntentKeys.Repair:
                            HandleRepair(context, creep, record, energyLedger, objectOverrides);
                            break;
                        case IntentKeys.Build:
                            HandleBuild(context, creep, record, energyLedger, objectOverrides, terrainCache);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void HandleRepair(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, int> energyLedger,
                                     Dictionary<string, ObjectStateOverride> objectOverrides)
    {
        if (!TryResolveTarget(context.State.Objects, record, objectOverrides, out var target))
            return;

        if (target.Hits is null || target.HitsMax is null || target.Hits >= target.HitsMax)
            return;

        if (!WorkPartHelper.TryGetActiveWorkParts(creep, out var workParts))
            return;

        var availableEnergy = GetAvailableEnergy(creep, energyLedger);
        if (availableEnergy <= 0)
            return;

        if (!IsInRange(creep, target))
            return;

        var repairPower = workParts.Count * ScreepsGameConstants.RepairPower;
        if (repairPower <= 0)
            return;

        var repairHitsRemaining = target.HitsMax.Value - target.Hits.Value;
        if (repairHitsRemaining <= 0)
            return;

        var energyLimitedEffect = Math.Min(repairPower, repairHitsRemaining);
        var energyLimitedByEnergy = availableEnergy / ScreepsGameConstants.RepairEnergyCost;
        var repairEffect = Math.Min(energyLimitedEffect, energyLimitedByEnergy);
        if (repairEffect <= 0)
            return;

        var boostedEffect = WorkPartHelper.ApplyWorkBoosts(workParts, repairEffect, WorkBoostEffect.Repair, ScreepsGameConstants.RepairPower);
        var totalEffect = (int)Math.Min(repairHitsRemaining, boostedEffect);
        if (totalEffect <= 0)
            return;

        var repairCost = Math.Min(availableEnergy, (int)Math.Ceiling(repairEffect * ScreepsGameConstants.RepairEnergyCost));
        var remainingEnergy = availableEnergy - repairCost;
        CommitEnergy(context, creep, energyLedger, remainingEnergy);

        var newHits = Math.Min(target.Hits.Value + totalEffect, target.HitsMax.Value);
        context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
        {
            Hits = newHits
        });
        UpdateObjectOverrides(objectOverrides, target.Id, hits: newHits, progress: null);

        if (!string.IsNullOrWhiteSpace(creep.UserId))
            context.Stats.IncrementEnergyConstruction(creep.UserId!, repairCost);

        context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
        {
            ActionLog = new RoomObjectActionLogPatch(
                Repair: new RoomObjectActionLogRepair(target.X, target.Y))
        });
    }

    private void HandleBuild(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, int> energyLedger,
                             Dictionary<string, ObjectStateOverride> objectOverrides, TerrainCache terrainCache)
    {
        if (!TryResolveTarget(context.State.Objects, record, objectOverrides, out var target))
            return;

        if (!string.Equals(target.Type, RoomObjectTypes.ConstructionSite, StringComparison.Ordinal))
            return;

        if (target.Progress is null || target.ProgressTotal is null)
            return;

        if (target.Progress >= target.ProgressTotal)
            return;

        if (!WorkPartHelper.TryGetActiveWorkParts(creep, out var workParts))
            return;

        var availableEnergy = GetAvailableEnergy(creep, energyLedger);
        if (availableEnergy <= 0)
            return;

        if (!IsInRange(creep, target))
            return;

        var buildPower = workParts.Count * ScreepsGameConstants.BuildPower;
        if (buildPower <= 0)
            return;

        var buildRemaining = target.ProgressTotal.Value - target.Progress.Value;
        var buildEffect = Math.Min(buildPower, Math.Min(availableEnergy, buildRemaining));
        if (buildEffect <= 0)
            return;

        var boostedEffect = WorkPartHelper.ApplyWorkBoosts(workParts, buildEffect, WorkBoostEffect.Build, ScreepsGameConstants.BuildPower);
        var totalProgress = (int)Math.Min(buildRemaining, boostedEffect);
        if (totalProgress <= 0)
            return;

        var energySpent = buildEffect;
        var remainingEnergy = availableEnergy - energySpent;
        CommitEnergy(context, creep, energyLedger, remainingEnergy);
        if (!string.IsNullOrWhiteSpace(creep.UserId))
            context.Stats.IncrementEnergyConstruction(creep.UserId!, energySpent);

        var updatedProgress = target.Progress.Value + totalProgress;
        if (updatedProgress < target.ProgressTotal.Value) {
            context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
            {
                Progress = updatedProgress
            });
            UpdateObjectOverrides(objectOverrides, target.Id, hits: null, progress: updatedProgress);
        }
        else {
            context.MutationWriter.Remove(target.Id);
            objectOverrides.Remove(target.Id);
            var blueprint = ResolveBlueprint(target.StructureType);
            if (blueprint is null)
                return;

            var controllerLevel = context.State.Info?.ControllerLevel;
            var terrainMask = DetermineTerrainMask(terrainCache, target.X, target.Y);
            var options = new StructureCreationOptions(
                context.State.RoomName,
                context.State.Info?.Shard,
                target.UserId,
                target.X,
                target.Y,
                context.State.GameTime,
                controllerLevel,
                (terrainMask & ScreepsGameConstants.TerrainMaskSwamp) != 0,
                (terrainMask & ScreepsGameConstants.TerrainMaskWall) != 0,
                Name: target.Name);

            var newStructure = structureFactory.CreateStructureSnapshot(blueprint, options);
            context.MutationWriter.Upsert(newStructure);
        }

        context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
        {
            ActionLog = new RoomObjectActionLogPatch(
                Build: new RoomObjectActionLogBuild(target.X, target.Y))
        });
    }

    private static bool TryResolveTarget(
        IReadOnlyDictionary<string, RoomObjectSnapshot> objects,
        IntentRecord record,
        Dictionary<string, ObjectStateOverride> overrides,
        [NotNullWhen(true)] out RoomObjectSnapshot? target)
    {
        target = null;
        if (!TryGetTargetId(record, out var targetId))
            return false;

        if (!objects.TryGetValue(targetId, out var current) || current is null)
            return false;

        if (overrides.TryGetValue(targetId, out var state)) {
            if (state?.Hits.HasValue == true)
                current = current with { Hits = state.Hits };
            if (state?.Progress.HasValue == true)
                current = current with { Progress = state.Progress };
        }

        target = current;
        return target is not null;
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        targetId = string.Empty;
        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(IntentKeys.TargetId, out var value))
            return false;

        targetId = value.Kind switch
        {
            IntentFieldValueKind.Text => value.TextValue ?? string.Empty,
            IntentFieldValueKind.Number => value.NumberValue?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(targetId);
    }

    private static bool IsInRange(RoomObjectSnapshot creep, RoomObjectSnapshot target)
        => Math.Max(Math.Abs(creep.X - target.X), Math.Abs(creep.Y - target.Y)) <= 3;

    private static int GetAvailableEnergy(RoomObjectSnapshot creep, Dictionary<string, int> ledger)
    {
        if (ledger.TryGetValue(creep.Id, out var cached))
            return cached;

        if (creep.Store.TryGetValue(RoomDocumentFields.RoomObject.Store.Energy, out var energy)) {
            ledger[creep.Id] = energy;
            return energy;
        }

        ledger[creep.Id] = 0;
        return 0;
    }

    private static void CommitEnergy(RoomProcessorContext context, RoomObjectSnapshot creep, Dictionary<string, int> ledger, int newAmount)
    {
        ledger[creep.Id] = Math.Max(newAmount, 0);
        context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(1, Comparer)
            {
                [RoomDocumentFields.RoomObject.Store.Energy] = ledger[creep.Id]
            }
        });
    }

    private StructureBlueprint? ResolveBlueprint(string? structureType)
    {
        if (string.IsNullOrWhiteSpace(structureType))
            return null;

        if (blueprintProvider.TryGet(structureType, out var blueprint))
            return blueprint;

        return null;
    }

    private static TerrainCache BuildTerrainCache(RoomProcessorContext context)
    {
        var terrain = context.State.Terrain?.Values.FirstOrDefault(t => string.Equals(t.RoomName, context.State.RoomName, StringComparison.Ordinal));
        return new TerrainCache(terrain?.Terrain);
    }

    private static int DetermineTerrainMask(TerrainCache cache, int x, int y)
    {
        if (string.IsNullOrEmpty(cache.Terrain))
            return 0;

        var index = (y * 50) + x;
        if (index < 0 || index >= cache.Terrain.Length)
            return 0;

        return TerrainEncoding.Decode(cache.Terrain[index]);
    }

    private static void UpdateObjectOverrides(
        Dictionary<string, ObjectStateOverride> overrides,
        string objectId,
        int? hits,
        int? progress)
    {
        overrides.TryGetValue(objectId, out var existing);
        overrides[objectId] = new ObjectStateOverride(
            hits ?? existing?.Hits,
            progress ?? existing?.Progress);
    }

    private sealed record TerrainCache(string? Terrain);

    private sealed record ObjectStateOverride(int? Hits, int? Progress);
}
