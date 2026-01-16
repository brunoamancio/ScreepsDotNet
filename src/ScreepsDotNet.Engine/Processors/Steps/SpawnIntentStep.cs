namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Processes spawn intents (create, setDirections, cancel) using the shared helpers.
/// </summary>
internal sealed class SpawnIntentStep(
    ISpawnIntentParser parser,
    ISpawnStateReader stateReader,
    ISpawnEnergyAllocator energyAllocator) : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var energyLedger = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (userId, envelope) in intents.Users)
        {
            if (envelope?.SpawnIntents is null || envelope.SpawnIntents.Count == 0)
                continue;

            foreach (var (objectId, spawnIntent) in envelope.SpawnIntents)
            {
                if (string.IsNullOrWhiteSpace(objectId))
                    continue;

                if (!TryResolveSpawn(context, objectId, userId, out var spawn))
                    continue;

                ProcessSpawn(context, spawn, spawnIntent, energyLedger);
            }
        }

        return Task.CompletedTask;
    }

    private void ProcessSpawn(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnIntentEnvelope rawIntent,
        Dictionary<string, int> energyLedger)
    {
        var parsed = parser.Parse(rawIntent);
        if (!parsed.Success || !parsed.HasIntents)
            return;

        var runtime = stateReader.GetState(context.State, spawn);

        if (parsed.CancelSpawning)
        {
            HandleCancel(context, spawn, runtime);
            return;
        }

        if (parsed.DirectionsIntent is not null)
            HandleSetDirections(context, spawn, runtime, parsed.DirectionsIntent);

        if (parsed.CreateIntent is not null)
            HandleCreate(context, spawn, runtime, parsed.CreateIntent, energyLedger);

        if (parsed.RenewIntent is not null)
            HandleRenew(context, spawn, runtime, parsed.RenewIntent, energyLedger);

        if (parsed.RecycleIntent is not null)
            HandleRecycle(context, spawn, runtime, parsed.RecycleIntent);
    }

    private void HandleCreate(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnRuntimeState runtime,
        ParsedCreateCreepIntent intent,
        Dictionary<string, int> energyLedger)
    {
        if (runtime.IsSpawning)
            return;

        var requiredEnergy = intent.Body.TotalEnergyCost;
        if (requiredEnergy <= 0)
            return;

        var allocation = energyAllocator.AllocateEnergy(
            context.State.Objects,
            spawn,
            requiredEnergy,
            intent.EnergyStructureIds,
            energyLedger);

        if (!allocation.Success)
            return;

        ApplyEnergyDraws(context, allocation.Draws, energyLedger);

        var spawnTime = context.State.GameTime + intent.Body.SpawnTime;
        var spawning = new RoomSpawnSpawningSnapshot(
            intent.Name,
            intent.Body.SpawnTime,
            spawnTime,
            intent.Directions);

        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            Spawning = spawning
        });
    }

    private static void HandleSetDirections(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnRuntimeState runtime,
        ParsedSetDirectionsIntent intent)
    {
        if (!runtime.IsSpawning || runtime.Spawning is null)
            return;

        var updated = runtime.Spawning with { Directions = intent.Directions };
        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            Spawning = updated
        });
    }

    private static void HandleCancel(RoomProcessorContext context, RoomObjectSnapshot spawn, SpawnRuntimeState runtime)
    {
        if (!runtime.IsSpawning)
            return;

        context.MutationWriter.Patch(spawn.Id, new RoomObjectPatchPayload
        {
            ClearSpawning = true
        });
    }

    private void HandleRenew(RoomProcessorContext context, RoomObjectSnapshot spawn,
                             SpawnRuntimeState runtime, ParsedRenewIntent intent,
                             Dictionary<string, int> energyLedger)
    {
        if (runtime.IsSpawning)
            return;

        if (!TryResolveCreep(context, intent.TargetId, spawn.UserId, out var target))
            return;

        if (!IsAdjacent(spawn, target))
            return;

        if (target.Body.Count == 0 || HasClaimParts(target.Body))
            return;

        var effect = CalculateRenewEffect(target.Body.Count);
        if (effect <= 0)
            return;

        var currentTtl = Math.Clamp(target.TicksToLive ?? 0, 0, ScreepsGameConstants.CreepLifeTime);
        var allowed = ScreepsGameConstants.CreepLifeTime - currentTtl;
        if (allowed <= 0)
            return;

        effect = Math.Min(effect, allowed);

        var cost = CalculateRenewEnergyCost(target.Body);
        if (cost <= 0)
            return;

        var allocation = energyAllocator.AllocateEnergy(
            context.State.Objects,
            spawn,
            cost,
            null,
            energyLedger);

        if (!allocation.Success)
            return;

        ApplyEnergyDraws(context, allocation.Draws, energyLedger);

        IReadOnlyList<CreepBodyPartSnapshot>? cleanedBody = null;
        if (HasBoosts(target.Body))
            cleanedBody = ClearBoosts(target.Body);

        var patch = new RoomObjectPatchPayload
        {
            TicksToLive = currentTtl + effect,
            Body = cleanedBody
        };

        context.MutationWriter.Patch(target.Id, patch);
    }

    private static void HandleRecycle(
        RoomProcessorContext context,
        RoomObjectSnapshot spawn,
        SpawnRuntimeState runtime,
        ParsedRecycleIntent intent)
    {
        if (runtime.IsSpawning)
            return;

        if (!TryResolveCreep(context, intent.TargetId, spawn.UserId, out var target))
            return;

        if (!IsAdjacent(spawn, target))
            return;

        context.MutationWriter.Remove(target.Id);
    }

    private static void ApplyEnergyDraws(
        RoomProcessorContext context,
        IReadOnlyList<EnergyDraw> draws,
        Dictionary<string, int> energyLedger)
    {
        if (draws.Count == 0)
            return;

        foreach (var draw in draws)
        {
            if (draw.Amount <= 0)
                continue;

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
    }

    private static bool TryResolveSpawn(RoomProcessorContext context, string objectId, string userId, out RoomObjectSnapshot spawn)
    {
        spawn = null!;
        if (!context.State.Objects.TryGetValue(objectId, out var candidate))
            return false;

        if (!string.Equals(candidate.Type, RoomObjectTypes.Spawn, StringComparison.Ordinal))
            return false;

        if (!string.Equals(candidate.UserId, userId, StringComparison.Ordinal))
            return false;

        spawn = candidate;
        return true;
    }

    private static bool TryResolveCreep(RoomProcessorContext context, string objectId, string? userId, out RoomObjectSnapshot creep)
    {
        creep = null!;
        if (!context.State.Objects.TryGetValue(objectId, out var candidate))
            return false;

        if (!string.Equals(candidate.Type, RoomObjectTypes.Creep, StringComparison.Ordinal))
            return false;

        if (!string.Equals(candidate.UserId, userId, StringComparison.Ordinal))
            return false;

        creep = candidate;
        return true;
    }

    private static bool IsAdjacent(RoomObjectSnapshot a, RoomObjectSnapshot b)
        => string.Equals(a.RoomName, b.RoomName, StringComparison.Ordinal) &&
           Math.Abs(a.X - b.X) <= 1 &&
           Math.Abs(a.Y - b.Y) <= 1;

    private static bool HasClaimParts(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (body[i].Type == BodyPartType.Claim)
                return true;
        }

        return false;
    }

    private static bool HasBoosts(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        for (var i = 0; i < body.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(body[i].Boost))
                return true;
        }

        return false;
    }

    private static IReadOnlyList<CreepBodyPartSnapshot> ClearBoosts(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return Array.Empty<CreepBodyPartSnapshot>();

        var result = new CreepBodyPartSnapshot[body.Count];
        for (var i = 0; i < body.Count; i++)
        {
            var part = body[i];
            result[i] = string.IsNullOrWhiteSpace(part.Boost)
                ? part
                : part with { Boost = null };
        }

        return result;
    }

    private static int CalculateRenewEffect(int bodyLength)
    {
        if (bodyLength <= 0)
            return 0;

        var effect = (int)Math.Floor(
            ScreepsGameConstants.SpawnRenewRatio *
            ScreepsGameConstants.CreepLifeTime /
            ScreepsGameConstants.CreepSpawnTime /
            bodyLength);

        return Math.Max(effect, 0);
    }

    private static int CalculateRenewEnergyCost(IReadOnlyList<CreepBodyPartSnapshot> body)
    {
        if (body.Count == 0)
            return 0;

        var totalCost = 0;
        for (var i = 0; i < body.Count; i++)
        {
            if (!ScreepsGameConstants.TryGetBodyPartEnergyCost(body[i].Type, out var cost))
                return 0;

            totalCost += cost;
        }

        var value = ScreepsGameConstants.SpawnRenewRatio * totalCost /
                    ScreepsGameConstants.CreepSpawnTime /
                    body.Count;

        var costResult = (int)Math.Ceiling(value);
        return Math.Max(costResult, 0);
    }

    private static int GetEnergy(RoomObjectSnapshot obj)
        => obj.Store.GetValueOrDefault(RoomDocumentFields.RoomObject.Store.Energy, 0);
}
