namespace ScreepsDotNet.Engine.Processors.GlobalSteps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Handles player-issued power creep intents (rename/delete/etc.).
/// </summary>
internal sealed class PowerCreepIntentStep : IGlobalProcessorStep
{
    private const int MaxNameLength = 50;
    private readonly Func<long> _timestampProvider;

    public PowerCreepIntentStep()
        : this(() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
    {
    }

    internal PowerCreepIntentStep(Func<long> timestampProvider)
        => _timestampProvider = timestampProvider ?? throw new ArgumentNullException(nameof(timestampProvider));

    public Task ExecuteAsync(GlobalProcessorContext context, CancellationToken token = default)
    {
        foreach (var (userId, intents) in context.UserIntentsByUser) {
            if (string.IsNullOrWhiteSpace(userId))
                continue;

            if (!context.UsersById.TryGetValue(userId, out var user))
                continue;

            ProcessCreateIntents(context, userId, user, intents);
            ProcessRenameIntents(context, userId, intents);
            ProcessDeleteIntents(context, userId, user, intents);
            ProcessSuicideIntents(context, userId, intents);
            ProcessSpawnIntents(context, userId, intents);
            ProcessUpgradeIntents(context, userId, intents);
        }

        return Task.CompletedTask;
    }

    private static void ProcessCreateIntents(GlobalProcessorContext context, string userId, UserState user, GlobalUserIntentSnapshot snapshot)
    {
        foreach (var record in snapshot.Intents) {
            if (!string.Equals(record.Name, GlobalIntentTypes.CreatePowerCreep, StringComparison.Ordinal))
                continue;

            foreach (var argument in record.Arguments) {
                var requestedName = NormalizeName(GetTextArgument(argument, PowerCreepIntentFields.Name));
                if (string.IsNullOrWhiteSpace(requestedName))
                    continue;

                var className = GetTextArgument(argument, PowerCreepIntentFields.ClassName);
                if (!string.Equals(className, PowerClass.Operator, StringComparison.Ordinal))
                    continue;

                var userPowerCreeps = context.PowerCreepsById.Values
                    .Where(pc => string.Equals(pc.UserId, userId, StringComparison.Ordinal))
                    .ToList();

                if (HasDuplicateName(userPowerCreeps, userId, requestedName))
                    continue;

                var powerLevel = Math.Floor(Math.Pow(user.Power / ScreepsGameConstants.PowerLevelMultiply, 1.0 / ScreepsGameConstants.PowerLevelPow));
                var usedLevels = userPowerCreeps.Count + userPowerCreeps.Sum(pc => pc.Level ?? 0);

                if (usedLevels >= powerLevel)
                    continue;

                var newId = Guid.NewGuid().ToString("N");
                var newPowerCreep = new PowerCreepSnapshot(
                    newId,
                    userId,
                    requestedName,
                    className,
                    0,
                    1000,
                    new Dictionary<string, int>(),
                    100,
                    0,
                    null,
                    null,
                    new Dictionary<string, PowerCreepPowerSnapshot>());

                context.Mutations.UpsertPowerCreep(newPowerCreep);
                context.UpdatePowerCreep(newPowerCreep);
            }
        }
    }

    private static void ProcessRenameIntents(GlobalProcessorContext context, string userId, GlobalUserIntentSnapshot snapshot)
    {
        foreach (var record in snapshot.Intents) {
            if (!string.Equals(record.Name, GlobalIntentTypes.RenamePowerCreep, StringComparison.Ordinal))
                continue;

            foreach (var argument in record.Arguments) {
                var requestedId = GetTextArgument(argument, PowerCreepIntentFields.Id);
                var requestedName = NormalizeName(GetTextArgument(argument, PowerCreepIntentFields.Name));

                if (string.IsNullOrWhiteSpace(requestedId) || string.IsNullOrWhiteSpace(requestedName))
                    continue;

                if (!context.PowerCreepsById.TryGetValue(requestedId, out var snapshotCreep))
                    continue;

                if (!string.Equals(snapshotCreep.UserId, userId, StringComparison.Ordinal))
                    continue;

                if (snapshotCreep.SpawnCooldownTime is null)
                    continue;

                if (HasDuplicateName(context.PowerCreepsById.Values, userId, requestedName))
                    continue;

                context.Mutations.PatchPowerCreep(requestedId, new PowerCreepMutationPatch(Name: requestedName));
                var updated = snapshotCreep with { Name = requestedName };
                context.UpdatePowerCreep(updated);
            }
        }
    }

    private void ProcessDeleteIntents(GlobalProcessorContext context, string userId, UserState user, GlobalUserIntentSnapshot snapshot)
    {
        foreach (var record in snapshot.Intents) {
            if (!string.Equals(record.Name, GlobalIntentTypes.DeletePowerCreep, StringComparison.Ordinal))
                continue;

            foreach (var argument in record.Arguments) {
                var creepId = GetTextArgument(argument, PowerCreepIntentFields.Id);
                if (string.IsNullOrWhiteSpace(creepId))
                    continue;

                if (!context.PowerCreepsById.TryGetValue(creepId, out var creep))
                    continue;

                if (!string.Equals(creep.UserId, userId, StringComparison.Ordinal))
                    continue;

                if (creep.SpawnCooldownTime is null)
                    continue;

                var cancel = GetBooleanArgument(argument, PowerCreepIntentFields.Cancel);
                if (cancel) {
                    context.Mutations.PatchPowerCreep(creepId, new PowerCreepMutationPatch(ClearDeleteTime: true));
                    context.UpdatePowerCreep(creep with { DeleteTime = null });
                    continue;
                }

                var now = _timestampProvider();
                if (user.PowerExperimentationTime > now) {
                    context.Mutations.RemovePowerCreep(creepId);
                    context.RemovePowerCreep(creepId);
                    continue;
                }

                if (creep.DeleteTime.HasValue)
                    continue;

                var deleteTime = now + ScreepsGameConstants.PowerCreepDeleteCooldownMilliseconds;
                context.Mutations.PatchPowerCreep(creepId, new PowerCreepMutationPatch(DeleteTime: deleteTime));
                context.UpdatePowerCreep(creep with { DeleteTime = deleteTime });
            }
        }
    }

    private static void ProcessSuicideIntents(GlobalProcessorContext context, string userId, GlobalUserIntentSnapshot snapshot)
    {
        foreach (var record in snapshot.Intents) {
            if (!string.Equals(record.Name, GlobalIntentTypes.SuicidePowerCreep, StringComparison.Ordinal))
                continue;

            foreach (var argument in record.Arguments) {
                var creepId = GetTextArgument(argument, PowerCreepIntentFields.Id);
                if (string.IsNullOrWhiteSpace(creepId))
                    continue;

                if (!context.PowerCreepsById.TryGetValue(creepId, out var creep))
                    continue;

                if (!string.Equals(creep.UserId, userId, StringComparison.Ordinal))
                    continue;

                if (creep.Shard is null)
                    continue;

                var roomObjects = context.GetObjectsOfType(RoomObjectTypes.PowerCreep);
                var roomObject = roomObjects.FirstOrDefault(obj => string.Equals(obj.Id, creepId, StringComparison.Ordinal));
                if (roomObject is not null) {
                    context.Mutations.RemoveRoomObject(roomObject.Id);
                }

                context.Mutations.PatchPowerCreep(creepId, new PowerCreepMutationPatch(Shard: null));
                context.UpdatePowerCreep(creep with { Shard = null });
            }
        }
    }

    private static void ProcessSpawnIntents(GlobalProcessorContext context, string userId, GlobalUserIntentSnapshot snapshot)
    {
        foreach (var record in snapshot.Intents) {
            if (!string.Equals(record.Name, GlobalIntentTypes.SpawnPowerCreep, StringComparison.Ordinal))
                continue;

            foreach (var argument in record.Arguments) {
                var creepId = GetTextArgument(argument, PowerCreepIntentFields.Id);
                var spawnId = GetTextArgument(argument, PowerCreepIntentFields.SpawnId);

                if (string.IsNullOrWhiteSpace(creepId) || string.IsNullOrWhiteSpace(spawnId))
                    continue;

                if (!context.PowerCreepsById.TryGetValue(creepId, out var creep))
                    continue;

                if (!string.Equals(creep.UserId, userId, StringComparison.Ordinal))
                    continue;

                if (creep.Shard is not null)
                    continue;

                var spawns = context.GetObjectsOfType(RoomObjectTypes.Spawn);
                var spawn = spawns.FirstOrDefault(obj => string.Equals(obj.Id, spawnId, StringComparison.Ordinal));
                if (spawn is null || string.IsNullOrWhiteSpace(spawn.RoomName))
                    continue;

                if (!string.Equals(spawn.UserId, userId, StringComparison.Ordinal))
                    continue;

                var newRoomObject = new RoomObjectSnapshot(
                    creepId,
                    RoomObjectTypes.PowerCreep,
                    spawn.RoomName,
                    spawn.Shard,
                    userId,
                    spawn.X,
                    spawn.Y,
                    creep.HitsMax,
                    creep.HitsMax,
                    Fatigue: null,
                    TicksToLive: null,
                    Name: creep.Name,
                    Level: creep.Level,
                    Density: null,
                    MineralType: null,
                    DepositType: null,
                    StructureType: null,
                    Store: new Dictionary<string, int>(creep.Store),
                    StoreCapacity: creep.StoreCapacity,
                    StoreCapacityResource: new Dictionary<string, int>(0, StringComparer.Ordinal),
                    Reservation: null,
                    Sign: null,
                    Structure: null,
                    Effects: new Dictionary<string, PowerEffectSnapshot>(0, StringComparer.Ordinal),
                    Spawning: null,
                    Body: [],
                    IsSpawning: null,
                    UserSummoned: null,
                    IsPublic: null,
                    StrongholdId: null,
                    DeathTime: null,
                    DecayTime: null,
                    CreepId: null,
                    CreepName: null,
                    CreepTicksToLive: null,
                    CreepSaying: null,
                    ResourceType: null,
                    ResourceAmount: null,
                    Progress: null,
                    ProgressTotal: null,
                    ActionLog: null,
                    Energy: null,
                    MineralAmount: null,
                    InvaderHarvested: null,
                    Harvested: null,
                    Cooldown: null,
                    CooldownTime: null,
                    SafeMode: null,
                    PortalDestination: null,
                    Send: null);

                context.Mutations.UpsertRoomObject(newRoomObject);
                context.Mutations.PatchPowerCreep(creepId, new PowerCreepMutationPatch(Shard: spawn.RoomName));
                context.UpdatePowerCreep(creep with { Shard = spawn.RoomName });
            }
        }
    }

    private static void ProcessUpgradeIntents(GlobalProcessorContext context, string userId, GlobalUserIntentSnapshot snapshot)
    {
        foreach (var record in snapshot.Intents) {
            if (!string.Equals(record.Name, GlobalIntentTypes.UpgradePowerCreep, StringComparison.Ordinal))
                continue;

            foreach (var argument in record.Arguments) {
                var creepId = GetTextArgument(argument, PowerCreepIntentFields.Id);
                var powerField = GetTextArgument(argument, PowerCreepIntentFields.Power);

                if (string.IsNullOrWhiteSpace(creepId) || string.IsNullOrWhiteSpace(powerField))
                    continue;

                if (!int.TryParse(powerField, out var powerInt) || !Enum.IsDefined(typeof(PowerTypes), powerInt))
                    continue;

                var powerType = (PowerTypes)powerInt;

                if (!context.PowerCreepsById.TryGetValue(creepId, out var creep))
                    continue;

                if (!string.Equals(creep.UserId, userId, StringComparison.Ordinal))
                    continue;

                if (creep.Shard is not null)
                    continue;

                if (!PowerInfo.Abilities.TryGetValue(powerType, out var abilityInfo))
                    continue;

                var requiredLevel = abilityInfo.Level[0];
                if (creep.Level < requiredLevel)
                    continue;

                var powerKey = powerInt.ToString();
                if (creep.Powers.ContainsKey(powerKey))
                    continue;

                var newPowers = new Dictionary<string, PowerCreepPowerSnapshot>(creep.Powers, StringComparer.Ordinal)
                {
                    [powerKey] = new(Level: 0)
                };

                context.Mutations.PatchPowerCreep(creepId, new PowerCreepMutationPatch(Powers: newPowers));
                context.UpdatePowerCreep(creep with { Powers = newPowers });
            }
        }
    }

    private static bool HasDuplicateName(IEnumerable<PowerCreepSnapshot> creeps, string userId, string name)
        => creeps.Any(creep =>
            string.Equals(creep.UserId, userId, StringComparison.Ordinal) &&
            string.Equals(creep.Name, name, StringComparison.Ordinal));

    private static string? GetTextArgument(IntentArgument argument, string field)
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

    private static bool GetBooleanArgument(IntentArgument argument, string field, bool defaultValue = false)
    {
        return !argument.Fields.TryGetValue(field, out var value)
            ? defaultValue
            : value.Kind switch
            {
                IntentFieldValueKind.Boolean => value.BooleanValue ?? defaultValue,
                IntentFieldValueKind.Number => value.NumberValue is { } number && number != 0,
                IntentFieldValueKind.Text => bool.TryParse(value.TextValue, out var parsed) ? parsed : defaultValue,
                _ => defaultValue
            };
    }

    private static string? NormalizeName(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return null;

        var trimmed = source.Trim();
        if (trimmed.Length > MaxNameLength)
            trimmed = trimmed[..MaxNameLength];
        return trimmed;
    }
}
