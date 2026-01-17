namespace ScreepsDotNet.Engine.Processors.Steps;

using System;
using System.Collections.Generic;
using System.Linq;
using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Common.Utilities;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;
using ScreepsDotNet.Engine.Processors.Helpers;

/// <summary>
/// Resolves creep movement intents using the legacy prioritization rules (pull chains, swaps, safe-mode ramparts).
/// </summary>
internal sealed class MovementIntentStep(ICreepDeathProcessor deathProcessor) : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    private static readonly HashSet<string> BlockingStructureTypes = new(StringComparer.Ordinal)
    {
        RoomObjectTypes.Spawn,
        RoomObjectTypes.Extension,
        RoomObjectTypes.Lab,
        RoomObjectTypes.Tower,
        RoomObjectTypes.Nuker,
        RoomObjectTypes.Observer,
        RoomObjectTypes.PowerSpawn,
        RoomObjectTypes.Factory,
        RoomObjectTypes.Terminal,
        RoomObjectTypes.Storage,
        RoomObjectTypes.Link,
        RoomObjectTypes.ConstructedWall
    };

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var pullState = CollectPullIntents(context, intents);
        var requests = CollectMoveCandidates(context, intents, pullState);
        if (requests.Count == 0)
            return Task.CompletedTask;

        var tiles = BuildTileMap(context);
        var terrain = BuildTerrainCache(context);
        var safeModeOwner = DetermineSafeModeOwner(context);

        var (acceptedMoves, crashes) = ResolveMoves(requests, tiles, terrain, safeModeOwner);

        ApplyAcceptedMoves(context, acceptedMoves);
        ProcessCrashes(context, crashes);

        return Task.CompletedTask;
    }

    private void ProcessCrashes(RoomProcessorContext context, IReadOnlyList<RoomObjectSnapshot> crashes)
    {
        if (crashes.Count == 0)
            return;

        var energyLedger = new Dictionary<string, int>(Comparer);
        foreach (var creep in crashes)
            deathProcessor.Process(context, creep, new CreepDeathOptions(ViolentDeath: true), energyLedger);
    }

    private static void ApplyAcceptedMoves(
        RoomProcessorContext context,
        IReadOnlyDictionary<string, TileCoord> acceptedMoves)
    {
        foreach (var (creepId, target) in acceptedMoves)
        {
            if (!context.State.Objects.TryGetValue(creepId, out var creep))
                continue;

            var patch = new RoomObjectPatchPayload
            {
                Position = new RoomObjectPositionPatch(target.X, target.Y),
                Fatigue = Math.Max((creep.Fatigue ?? 0) - 2, 0)
            };

            context.MutationWriter.Patch(creepId, patch);
        }
    }

    private static (Dictionary<string, TileCoord>, List<RoomObjectSnapshot>) ResolveMoves(
        IReadOnlyList<MoveCandidate> candidates,
        Dictionary<TileCoord, TileInfo> tiles,
        TerrainCache terrain,
        string? safeModeOwner)
    {
        var matrix = BuildMatrix(candidates);
        if (matrix.Count == 0)
            return (new Dictionary<string, TileCoord>(Comparer), []);

        var resolved = BuildResolvedMatrix(matrix);
        var plannedMoves = resolved.Values.ToDictionary(a => a.Candidate.Creep.Id, a => a.Target, Comparer);
        var crashes = new Dictionary<string, RoomObjectSnapshot>(Comparer);

        foreach (var target in resolved.Keys.ToList())
            ValidateAssignment(target);

        return (plannedMoves, crashes.Values.ToList());

        void ValidateAssignment(TileCoord target)
        {
            if (!resolved.TryGetValue(target, out var assignment))
                return;

            var candidate = assignment.Candidate;

            if (candidate.IsOutOfBounds)
            {
                RemoveAssignment(target, fatal: true);
                return;
            }

            if (!CanMove(candidate))
            {
                RemoveAssignment(target);
                return;
            }

            var obstacle = EvaluateObstacle(target, candidate, tiles, plannedMoves, safeModeOwner, terrain);
            if (obstacle == ObstacleEvaluation.Fatal)
            {
                RemoveAssignment(target, fatal: true);
                return;
            }

            if (obstacle == ObstacleEvaluation.Blocked)
                RemoveAssignment(target);
        }

        void RemoveAssignment(TileCoord target, bool fatal = false)
        {
            if (!resolved.Remove(target, out var assignment))
                return;

            plannedMoves.Remove(assignment.Candidate.Creep.Id);

            if (fatal)
                RegisterCrash(assignment.Candidate);

            var origin = assignment.Candidate.Origin;
            if (resolved.ContainsKey(origin))
                RemoveAssignment(origin);
        }
        void RegisterCrash(MoveCandidate candidate)
        {
            AddCrash(candidate.Creep);
            CrashPartner(candidate.PullTarget);
            CrashPartner(candidate.Puller);
        }

        void CrashPartner(RoomObjectSnapshot? partner)
        {
            if (partner is null)
                return;

            if (RemoveAssignmentByCreep(partner, fatal: true))
                return;

            AddCrash(partner);
        }

        bool RemoveAssignmentByCreep(RoomObjectSnapshot creep, bool fatal)
        {
            if (!plannedMoves.TryGetValue(creep.Id, out var partnerTarget))
                return false;

            if (!resolved.TryGetValue(partnerTarget, out var assignment) ||
                !string.Equals(assignment.Candidate.Creep.Id, creep.Id, StringComparison.Ordinal))
                return false;

            RemoveAssignment(partnerTarget, fatal);
            return true;
        }

        void AddCrash(RoomObjectSnapshot creep)
            => crashes.TryAdd(creep.Id, creep);
    }

    private static Dictionary<TileCoord, List<MoveCandidate>> BuildMatrix(IReadOnlyList<MoveCandidate> candidates)
    {
        var matrix = new Dictionary<TileCoord, List<MoveCandidate>>();
        foreach (var candidate in candidates)
        {
            if (!matrix.TryGetValue(candidate.Target, out var list))
            {
                list = new List<MoveCandidate>(2);
                matrix[candidate.Target] = list;
            }

            list.Add(candidate);
        }

        return matrix;
    }

    private static Dictionary<TileCoord, MoveAssignment> BuildResolvedMatrix(
        Dictionary<TileCoord, List<MoveCandidate>> matrix)
    {
        var resolved = new Dictionary<TileCoord, MoveAssignment>(matrix.Count);
        foreach (var (target, entries) in matrix)
        {
            var winner = entries.Count == 1
                ? entries[0]
                : SelectWinner(entries, target, matrix);

            resolved[target] = new MoveAssignment(winner, target);
        }

        return resolved;
    }

    private static MoveCandidate SelectWinner(
        IReadOnlyList<MoveCandidate> candidates,
        TileCoord contestedTarget,
        IReadOnlyDictionary<TileCoord, List<MoveCandidate>> matrix)
    {
        return candidates
            .OrderByDescending(candidate => ComputeRate1(candidate, contestedTarget, matrix))
            .ThenByDescending(candidate => candidate.IsPulled ? 1 : 0)
            .ThenByDescending(candidate => candidate.IsPulling ? 1 : 0)
            .ThenByDescending(ComputeMoveEfficiency)
            .ThenBy(candidate => candidate.Creep.Id, Comparer)
            .First();
    }

    private static int ComputeRate1(MoveCandidate candidate, TileCoord contestedTarget, IReadOnlyDictionary<TileCoord, List<MoveCandidate>> matrix)
    {
        if (matrix.TryGetValue(candidate.Origin, out var inbound) && inbound.Count > 0)
            return inbound.Any(entry => entry.Target.Equals(contestedTarget)) ? 100 : inbound.Count;

        return 0;
    }

    private static double ComputeMoveEfficiency(MoveCandidate candidate)
    {
        if (string.Equals(candidate.Creep.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal))
            return 0;

        var moves = CountActiveMoveParts(candidate.Creep);
        if (moves == 0)
            return 0;

        var weight = CalculateBodyWeight(candidate.Creep);
        return moves / (double)weight;
    }

    private static bool CanMove(MoveCandidate candidate)
    {
        if (string.Equals(candidate.Creep.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal))
            return true;

        if (candidate.IsPulled)
            return true;

        if (candidate.Creep.Fatigue.GetValueOrDefault() > 0)
            return false;

        return candidate.Creep.Body.Any(part => part.Type == BodyPartType.Move && part.Hits > 0);
    }

    private static ObstacleEvaluation EvaluateObstacle(
        TileCoord target,
        MoveCandidate candidate,
        Dictionary<TileCoord, TileInfo> tiles,
        IReadOnlyDictionary<string, TileCoord> plannedMoves,
        string? safeModeOwner,
        TerrainCache terrain)
    {
        if (!tiles.TryGetValue(target, out var tile))
            return terrain.IsWall(target.X, target.Y) ? ObstacleEvaluation.Fatal : ObstacleEvaluation.None;

        foreach (var structure in tile.Structures)
        {
            var structureEvaluation = EvaluateStructure(structure, candidate.Creep);
            if (structureEvaluation != ObstacleEvaluation.None)
                return structureEvaluation;
        }

        foreach (var occupant in tile.Creeps)
        {
            if (string.Equals(occupant.Id, candidate.Creep.Id, StringComparison.Ordinal))
                continue;

            if (plannedMoves.TryGetValue(occupant.Id, out var future) && !future.Equals(target))
                continue;

            if (CreepBlocks(occupant, candidate.Creep, safeModeOwner))
                return ObstacleEvaluation.Blocked;
        }

        if (terrain.IsWall(target.X, target.Y) && !tile.HasRoad)
            return ObstacleEvaluation.Fatal;

        return ObstacleEvaluation.None;
    }

    private static bool CreepBlocks(RoomObjectSnapshot occupant, RoomObjectSnapshot mover, string? safeModeOwner)
    {
        var isCreep = string.Equals(occupant.Type, RoomObjectTypes.Creep, StringComparison.Ordinal) ||
                      string.Equals(occupant.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal);
        if (!isCreep)
            return false;

        if (safeModeOwner is null)
            return true;

        if (!string.Equals(safeModeOwner, mover.UserId, StringComparison.Ordinal))
            return true;

        return string.Equals(mover.UserId, occupant.UserId, StringComparison.Ordinal);
    }

    private static ObstacleEvaluation EvaluateStructure(RoomObjectSnapshot structure, RoomObjectSnapshot creep)
    {
        var type = structure.Type;
        if (string.Equals(type, RoomObjectTypes.Rampart, StringComparison.Ordinal))
        {
            if (structure.IsPublic == true)
                return ObstacleEvaluation.None;

            return !string.Equals(structure.UserId, creep.UserId, StringComparison.Ordinal)
                ? ObstacleEvaluation.Fatal
                : ObstacleEvaluation.None;
        }

        if (string.Equals(type, RoomObjectTypes.ConstructionSite, StringComparison.Ordinal))
        {
            return BlocksConstructionSite(structure.StructureType, structure.UserId, creep.UserId)
                ? ObstacleEvaluation.Fatal
                : ObstacleEvaluation.None;
        }

        if (string.Equals(type, RoomObjectTypes.Portal, StringComparison.Ordinal))
        {
            if (string.Equals(creep.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal))
                return ObstacleEvaluation.Fatal;

            return ObstacleEvaluation.None;
        }

        if (string.Equals(type, RoomObjectTypes.Exit, StringComparison.Ordinal))
            return ObstacleEvaluation.None;

        return BlockingStructureTypes.Contains(type)
            ? ObstacleEvaluation.Fatal
            : ObstacleEvaluation.None;
    }

    private static bool BlocksConstructionSite(string? structureType, string? siteOwner, string? moverOwner)
    {
        if (string.IsNullOrWhiteSpace(structureType))
            return false;

        if (!string.Equals(siteOwner, moverOwner, StringComparison.Ordinal))
            return false;

        return BlockingStructureTypes.Contains(structureType);
    }

    private static PullState CollectPullIntents(RoomProcessorContext context, RoomIntentSnapshot intents)
    {
        var pullTargets = new Dictionary<string, string>(Comparer);
        var pulledBy = new Dictionary<string, string>(Comparer);

        foreach (var envelope in intents.Users.Values)
        {
            if (envelope?.ObjectIntents is null)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents)
            {
                if (!context.State.Objects.TryGetValue(objectId, out var creep))
                    continue;

                if (!creep.IsCreep(includePowerCreep: false) || creep.IsSpawning == true)
                    continue;

                if (!string.Equals(creep.UserId, envelope.UserId, StringComparison.Ordinal))
                    continue;

                foreach (var record in records)
                {
                    if (!string.Equals(record.Name, IntentKeys.Pull, StringComparison.Ordinal))
                        continue;

                    if (!TryGetTargetId(record, out var targetId))
                        continue;

                    if (!context.State.Objects.TryGetValue(targetId, out var target))
                        continue;

                    if (!target.IsCreep(includePowerCreep: true) || target.IsSpawning == true)
                        continue;

                    if (!string.Equals(target.UserId, creep.UserId, StringComparison.Ordinal))
                        continue;

                    if (Math.Max(Math.Abs(target.X - creep.X), Math.Abs(target.Y - creep.Y)) > 1)
                        continue;

                    if (pulledBy.ContainsKey(target.Id))
                        continue;

                    if (CreatesPullLoop(pullTargets, pulledBy, creep.Id, target.Id))
                        continue;

                    pullTargets[creep.Id] = target.Id;
                    pulledBy[target.Id] = creep.Id;
                }
            }
        }

        return new PullState(pullTargets, pulledBy);
    }

    private static List<MoveCandidate> CollectMoveCandidates(
        RoomProcessorContext context,
        RoomIntentSnapshot intents,
        PullState pullState)
    {
        var result = new List<MoveCandidate>();
        foreach (var envelope in intents.Users.Values)
        {
            if (envelope?.CreepIntents is null || envelope.CreepIntents.Count == 0)
                continue;

            foreach (var (objectId, creepIntent) in envelope.CreepIntents)
            {
                if (creepIntent?.Move is null)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var creep))
                    continue;

                if (!creep.IsCreep(includePowerCreep: true) || creep.IsSpawning == true)
                    continue;

                if (!string.Equals(creep.UserId, envelope.UserId, StringComparison.Ordinal))
                    continue;

                var targetX = creepIntent.Move.X;
                var targetY = creepIntent.Move.Y;
                var clampedX = Math.Clamp(targetX, 0, 49);
                var clampedY = Math.Clamp(targetY, 0, 49);
                var isOutOfBounds = clampedX != targetX || clampedY != targetY;
                if (!isOutOfBounds && clampedX == creep.X && clampedY == creep.Y)
                    continue;

                var origin = new TileCoord(creep.X, creep.Y);
                var target = new TileCoord(clampedX, clampedY);

                var puller = pullState.PulledBy.TryGetValue(creep.Id, out var pullerId) &&
                             context.State.Objects.TryGetValue(pullerId, out var pullerSnapshot)
                    ? pullerSnapshot
                    : null;
                var pullTarget = pullState.PullTargets.TryGetValue(creep.Id, out var pullTargetId) &&
                                 context.State.Objects.TryGetValue(pullTargetId, out var pullTargetSnapshot)
                    ? pullTargetSnapshot
                    : null;

                var candidate = new MoveCandidate(
                    creep,
                    origin,
                    target,
                    puller is not null,
                    pullTarget is not null,
                    isOutOfBounds,
                    puller,
                    pullTarget);

                result.Add(candidate);
            }
        }

        return result;
    }

    private static string? DetermineSafeModeOwner(RoomProcessorContext context)
    {
        foreach (var obj in context.State.Objects.Values)
        {
            if (!string.Equals(obj.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
                continue;

            if (obj.SafeMode is not int safeModeEnd || safeModeEnd <= context.State.GameTime)
                continue;

            if (string.IsNullOrWhiteSpace(obj.UserId))
                continue;

            return obj.UserId;
        }

        return null;
    }

    private static Dictionary<TileCoord, TileInfo> BuildTileMap(RoomProcessorContext context)
    {
        var tiles = new Dictionary<TileCoord, TileInfo>();
        foreach (var obj in context.State.Objects.Values)
        {
            var key = new TileCoord(obj.X, obj.Y);
            var tile = GetOrCreateTile(tiles, key);

            if (obj.IsCreep(includePowerCreep: true))
            {
                if (obj.IsSpawning == true)
                    continue;

                tile.Creeps.Add(obj);
                continue;
            }

            tile.Structures.Add(obj);
            if (string.Equals(obj.Type, RoomObjectTypes.Road, StringComparison.Ordinal))
                tile.HasRoad = true;
        }

        return tiles;
    }

    private static TileInfo GetOrCreateTile(Dictionary<TileCoord, TileInfo> tiles, TileCoord key)
    {
        if (!tiles.TryGetValue(key, out var tile))
        {
            tile = new TileInfo();
            tiles[key] = tile;
        }

        return tile;
    }

    private static TerrainCache BuildTerrainCache(RoomProcessorContext context)
    {
        var terrain = context.State.Terrain?.Values.FirstOrDefault(
            t => string.Equals(t.RoomName, context.State.RoomName, StringComparison.Ordinal));
        return new TerrainCache(terrain?.Terrain);
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

    private static bool CreatesPullLoop(
        IReadOnlyDictionary<string, string> pullTargets,
        IReadOnlyDictionary<string, string> pulledBy,
        string pullerId,
        string targetId)
    {
        var current = targetId;
        while (pullTargets.TryGetValue(current, out var next))
        {
            if (string.Equals(next, pullerId, StringComparison.Ordinal))
                return true;

            current = next;
        }

        current = pullerId;
        while (pulledBy.TryGetValue(current, out var upstream))
        {
            if (string.Equals(upstream, targetId, StringComparison.Ordinal))
                return true;

            current = upstream;
        }

        return false;
    }

    private static int CountActiveMoveParts(RoomObjectSnapshot creep)
    {
        var count = 0;
        foreach (var part in creep.Body)
        {
            if (part.Type == BodyPartType.Move && part.Hits > 0)
                count++;
        }

        return count;
    }

    private static int CalculateBodyWeight(RoomObjectSnapshot creep)
    {
        if (string.Equals(creep.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal))
            return 1;

        var weight = 0;
        foreach (var part in creep.Body)
        {
            if (part.Hits <= 0)
                continue;

            if (part.Type == BodyPartType.Move || part.Type == BodyPartType.Carry)
                continue;

            weight++;
        }

        weight += CalculateCarryWeight(creep);

        return Math.Max(weight, 1);
    }

    private static int CalculateCarryWeight(RoomObjectSnapshot creep)
    {
        if (creep.Store.Count == 0)
            return 0;

        var total = creep.Store.Values.Sum();
        if (total <= 0)
            return 0;

        var remaining = total;
        var weight = 0;
        for (var i = creep.Body.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var part = creep.Body[i];
            if (part.Type != BodyPartType.Carry || part.Hits <= 0)
                continue;

            var capacity = ScreepsGameConstants.CarryCapacity;
            remaining -= Math.Min(remaining, capacity);
            weight++;
        }

        return weight;
    }

    private enum ObstacleEvaluation
    {
        None,
        Blocked,
        Fatal
    }

    private sealed record MoveCandidate(
        RoomObjectSnapshot Creep,
        TileCoord Origin,
        TileCoord Target,
        bool IsPulled,
        bool IsPulling,
        bool IsOutOfBounds,
        RoomObjectSnapshot? Puller,
        RoomObjectSnapshot? PullTarget);

    private sealed record MoveAssignment(MoveCandidate Candidate, TileCoord Target);

    private sealed record PullState(
        IReadOnlyDictionary<string, string> PullTargets,
        IReadOnlyDictionary<string, string> PulledBy);

    private readonly record struct TileCoord(int X, int Y);

    private sealed class TileInfo
    {
        public List<RoomObjectSnapshot> Creeps { get; } = [];
        public List<RoomObjectSnapshot> Structures { get; } = [];
        public bool HasRoad { get; set; }
    }

    private sealed record TerrainCache(string? Terrain)
    {
        public bool IsWall(int x, int y)
        {
            if (string.IsNullOrEmpty(Terrain))
                return false;

            var index = (y * 50) + x;
            if (index < 0 || index >= Terrain.Length)
                return false;

            var mask = TerrainEncoding.Decode(Terrain[index]);
            return (mask & ScreepsGameConstants.TerrainMaskWall) != 0;
        }
    }
}
