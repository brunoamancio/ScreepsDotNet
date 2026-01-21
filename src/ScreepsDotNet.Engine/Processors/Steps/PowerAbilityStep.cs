using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Processes power creep ability usage intents (usePower).
/// Handles validation, ops deduction, cooldown setting, and effect application.
/// </summary>
internal sealed class PowerAbilityStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var storeLedger = new Dictionary<string, Dictionary<string, int>>(Comparer);
        var effectsLedger = new Dictionary<string, Dictionary<PowerTypes, PowerEffectSnapshot>>(Comparer);
        var powersLedger = new Dictionary<string, Dictionary<PowerTypes, PowerCreepPowerSnapshot>>(Comparer);
        var actionLogLedger = new Dictionary<string, RoomObjectActionLogPatch>(Comparer);
        var modifiedObjects = new HashSet<string>(Comparer);

        foreach (var envelope in intents.Users.Values)
        {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents)
            {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var powerCreep))
                    continue;

                if (!string.Equals(powerCreep.Type, RoomObjectTypes.PowerCreep, StringComparison.Ordinal))
                    continue;

                foreach (var record in records)
                {
                    if (!string.Equals(record.Name, IntentKeys.Power, StringComparison.Ordinal))
                        continue;

                    ProcessUsePower(context, powerCreep, record, storeLedger, effectsLedger, powersLedger, actionLogLedger, modifiedObjects);
                }
            }
        }

        EmitPatches(context, storeLedger, effectsLedger, powersLedger, actionLogLedger, modifiedObjects);

        return Task.CompletedTask;
    }

    private static void ProcessUsePower(
        RoomProcessorContext context,
        RoomObjectSnapshot powerCreep,
        IntentRecord record,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, Dictionary<PowerTypes, PowerEffectSnapshot>> effectsLedger,
        Dictionary<string, Dictionary<PowerTypes, PowerCreepPowerSnapshot>> powersLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        // Validate room has power enabled
        var controller = context.State.Objects.Values.FirstOrDefault(o => string.Equals(o.Type, RoomObjectTypes.Controller, StringComparison.Ordinal));
        if (controller is not null)
        {
            var isPowerEnabled = controller.Store.GetValueOrDefault(RoomDocumentFields.Controller.IsPowerEnabled, 0) == 1;
            if (!isPowerEnabled)
                return;

            // Check enemy safe mode
            if (!string.Equals(controller.UserId, powerCreep.UserId, StringComparison.Ordinal))
            {
                var safeMode = controller.SafeMode ?? 0;
                if (safeMode > context.State.GameTime)
                    return;
            }
        }

        // Extract power type from intent
        if (!TryGetPowerType(record, out var powerType))
            return;

        // Validate power exists in PowerInfo
        if (!PowerInfo.Abilities.TryGetValue(powerType, out var powerInfo))
            return;

        // Validate power creep has ability and level > 0
        var powers = powerCreep.Powers ?? new Dictionary<PowerTypes, PowerCreepPowerSnapshot>();
        if (!powers.TryGetValue(powerType, out var creepPower) || creepPower.Level == 0)
            return;

        // Check cooldown
        var gameTime = context.State.GameTime;
        var currentCooldown = creepPower.CooldownTime ?? 0;
        if (currentCooldown > gameTime)
            return;

        // Calculate ops cost
        var opsCost = CalculateOpsCost(powerInfo, creepPower.Level);

        // Check sufficient ops
        var currentOps = storeLedger.TryGetValue(powerCreep.Id, out var creepStore)
            ? creepStore.GetValueOrDefault(ResourceTypes.Ops, powerCreep.Store.GetValueOrDefault(ResourceTypes.Ops, 0))
            : powerCreep.Store.GetValueOrDefault(ResourceTypes.Ops, 0);

        if (currentOps < opsCost)
            return;

        // Extract target if range-based ability
        RoomObjectSnapshot? target = null;
        if (powerInfo.Range.HasValue)
        {
            if (!TryGetTargetId(record, out var targetId))
                return;

            if (!context.State.Objects.TryGetValue(targetId, out target))
                return;

            // Check range
            if (!IsTargetInRange(powerCreep, target, powerInfo.Range.Value))
                return;

            // Check for higher-level effect
            if (HasHigherLevelEffect(target, powerType, creepPower.Level, gameTime))
                return;

            // Special validation for operateExtension: must be storage or terminal
            if (powerType == PowerTypes.OperateExtension)
            {
                var isStorage = string.Equals(target.Type, RoomObjectTypes.Storage, StringComparison.Ordinal);
                var isTerminal = string.Equals(target.Type, RoomObjectTypes.Terminal, StringComparison.Ordinal);
                if (!isStorage && !isTerminal)
                    return;
            }
        }

        // Deduct ops
        if (!storeLedger.TryGetValue(powerCreep.Id, out var ledgerStore))
        {
            ledgerStore = new Dictionary<string, int>(powerCreep.Store);
            storeLedger[powerCreep.Id] = ledgerStore;
        }

        ledgerStore[ResourceTypes.Ops] = currentOps - opsCost;
        modifiedObjects.Add(powerCreep.Id);

        // Set power cooldown
        if (!powersLedger.TryGetValue(powerCreep.Id, out var ledgerPowers))
        {
            ledgerPowers = new Dictionary<PowerTypes, PowerCreepPowerSnapshot>(powers);
            powersLedger[powerCreep.Id] = ledgerPowers;
        }

        var newCooldownTime = gameTime + (powerInfo.Cooldown ?? 0);
        ledgerPowers[powerType] = new PowerCreepPowerSnapshot(creepPower.Level, newCooldownTime);

        // Record action log
        var targetX = target?.X ?? powerCreep.X;
        var targetY = target?.Y ?? powerCreep.Y;

        actionLogLedger[powerCreep.Id] = new RoomObjectActionLogPatch(
            UsePower: new RoomObjectActionLogUsePower((int)powerType, targetX, targetY));

        // Handle direct-action abilities
        if (powerType == PowerTypes.OperateExtension)
        {
            ProcessOperateExtension(context, powerCreep, target, creepPower.Level, powerInfo, storeLedger, modifiedObjects);
            return;
        }

        if (powerType == PowerTypes.Shield)
        {
            ProcessShield(context, powerCreep, creepPower.Level, gameTime, powerInfo, storeLedger, modifiedObjects);
            return;
        }

        // Apply power effects for effect-based abilities
        ApplyPowerEffect(powerType, target, creepPower.Level, gameTime, powerInfo, effectsLedger, modifiedObjects);
    }

    private static void ApplyPowerEffect(
        PowerTypes powerType,
        RoomObjectSnapshot? target,
        int level,
        int gameTime,
        PowerAbilityInfo powerInfo,
        Dictionary<string, Dictionary<PowerTypes, PowerEffectSnapshot>> effectsLedger,
        HashSet<string> modifiedObjects)
    {
        if (target is null)
            return;

        var applyEffect =
            // Validate target type for each power
            powerType switch
        {
            PowerTypes.OperateSpawn => string.Equals(target.Type, RoomObjectTypes.Spawn, StringComparison.Ordinal),
            PowerTypes.OperateTower => string.Equals(target.Type, RoomObjectTypes.Tower, StringComparison.Ordinal),
            PowerTypes.OperateStorage => string.Equals(target.Type, RoomObjectTypes.Storage, StringComparison.Ordinal),
            PowerTypes.OperateLab => string.Equals(target.Type, RoomObjectTypes.Lab, StringComparison.Ordinal),
            PowerTypes.OperateObserver => string.Equals(target.Type, RoomObjectTypes.Observer, StringComparison.Ordinal),
            PowerTypes.OperateTerminal => string.Equals(target.Type, RoomObjectTypes.Terminal, StringComparison.Ordinal),
            PowerTypes.OperatePower => string.Equals(target.Type, RoomObjectTypes.PowerSpawn, StringComparison.Ordinal),
            PowerTypes.OperateController => string.Equals(target.Type, RoomObjectTypes.Controller, StringComparison.Ordinal),
            PowerTypes.DisruptSpawn => string.Equals(target.Type, RoomObjectTypes.Spawn, StringComparison.Ordinal),
            PowerTypes.DisruptTower => string.Equals(target.Type, RoomObjectTypes.Tower, StringComparison.Ordinal),
            PowerTypes.DisruptSource => string.Equals(target.Type, RoomObjectTypes.Source, StringComparison.Ordinal),
            PowerTypes.DisruptTerminal => string.Equals(target.Type, RoomObjectTypes.Terminal, StringComparison.Ordinal),
            PowerTypes.RegenSource => string.Equals(target.Type, RoomObjectTypes.Source, StringComparison.Ordinal),
            PowerTypes.RegenMineral => ValidateRegenMineral(target),
            PowerTypes.Fortify => ValidateFortifyTarget(target),
            PowerTypes.OperateFactory => string.Equals(target.Type, RoomObjectTypes.Factory, StringComparison.Ordinal),
            _ => false
        };

        if (!applyEffect)
            return;

        // Calculate effect duration
        var duration = CalculateDuration(powerInfo, level);
        var endTime = gameTime + duration;

        // Add effect to target
        if (!effectsLedger.TryGetValue(target.Id, out var effects))
        {
            effects = new Dictionary<PowerTypes, PowerEffectSnapshot>(target.Effects);
            effectsLedger[target.Id] = effects;
        }

        effects[powerType] = new PowerEffectSnapshot(
            Power: powerType,
            Level: level,
            EndTime: endTime);

        modifiedObjects.Add(target.Id);
    }

    private static bool ValidateRegenMineral(RoomObjectSnapshot target)
    {
        if (!string.Equals(target.Type, RoomObjectTypes.Mineral, StringComparison.Ordinal))
            return false;

        var mineralAmount = target.MineralAmount ?? 0;
        var result = mineralAmount > 0;
        return result;
    }

    private static bool ValidateFortifyTarget(RoomObjectSnapshot target)
    {
        var isWall = string.Equals(target.Type, RoomObjectTypes.ConstructedWall, StringComparison.Ordinal);
        var isRampart = string.Equals(target.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal);
        var result = isWall || isRampart;
        return result;
    }

    private static int CalculateDuration(PowerAbilityInfo powerInfo, int level)
    {
        if (powerInfo.DurationLevels is not null)
            return powerInfo.DurationLevels[level - 1];

        var result = powerInfo.Duration ?? 0;
        return result;
    }

    private static bool TryGetPowerType(IntentRecord record, out PowerTypes powerType)
    {
        powerType = default;

        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(PowerCreepIntentFields.Power, out var value))
            return false;

        if (value.Kind != IntentFieldValueKind.Number || !value.NumberValue.HasValue)
            return false;

        powerType = (PowerTypes)value.NumberValue.Value;
        return true;
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        targetId = string.Empty;

        if (record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue(PowerCreepIntentFields.Id, out var value))
            return false;

        if (value.Kind != IntentFieldValueKind.Text)
            return false;

        targetId = value.TextValue ?? string.Empty;
        return !string.IsNullOrWhiteSpace(targetId);
    }

    private static int CalculateOpsCost(PowerAbilityInfo powerInfo, int level)
    {
        if (powerInfo.OpsLevels is not null)
            return powerInfo.OpsLevels[level - 1];

        return powerInfo.Ops ?? 0;
    }

    private static bool IsTargetInRange(RoomObjectSnapshot powerCreep, RoomObjectSnapshot target, int range)
    {
        var dx = Math.Abs(powerCreep.X - target.X);
        var dy = Math.Abs(powerCreep.Y - target.Y);
        var distance = Math.Max(dx, dy);
        return distance <= range;
    }

    private static bool HasHigherLevelEffect(RoomObjectSnapshot target, PowerTypes power, int level, int gameTime)
    {
        if (!target.Effects.TryGetValue(power, out var currentEffect))
            return false;

        return currentEffect.Level > level && currentEffect.EndTime > gameTime;
    }

    private static void EmitPatches(
        RoomProcessorContext context,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        Dictionary<string, Dictionary<PowerTypes, PowerEffectSnapshot>> effectsLedger,
        Dictionary<string, Dictionary<PowerTypes, PowerCreepPowerSnapshot>> powersLedger,
        Dictionary<string, RoomObjectActionLogPatch> actionLogLedger,
        HashSet<string> modifiedObjects)
    {
        foreach (var objectId in modifiedObjects)
        {
            var patch = new RoomObjectPatchPayload
            {
                Store = storeLedger.GetValueOrDefault(objectId),
                Effects = effectsLedger.GetValueOrDefault(objectId),
                Powers = powersLedger.GetValueOrDefault(objectId),
                ActionLog = actionLogLedger.GetValueOrDefault(objectId)
            };

            context.MutationWriter.Patch(objectId, patch);
        }
    }

    private static void ProcessOperateExtension(
        RoomProcessorContext context,
        RoomObjectSnapshot powerCreep,
        RoomObjectSnapshot? target,
        int level,
        PowerAbilityInfo powerInfo,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        HashSet<string> modifiedObjects)
    {
        if (target is null)
            return;

        // Validate target is storage or terminal
        var isStorage = string.Equals(target.Type, RoomObjectTypes.Storage, StringComparison.Ordinal);
        var isTerminal = string.Equals(target.Type, RoomObjectTypes.Terminal, StringComparison.Ordinal);
        if (!isStorage && !isTerminal)
            return;

        // Get available energy from target
        var targetEnergy = storeLedger.TryGetValue(target.Id, out var targetStore)
            ? targetStore.GetValueOrDefault(ResourceTypes.Energy, target.Store.GetValueOrDefault(ResourceTypes.Energy, 0))
            : target.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (targetEnergy == 0)
            return;

        // Find all non-full extensions
        var extensions = context.State.Objects.Values
            .Where(o => string.Equals(o.Type, RoomObjectTypes.Extension, StringComparison.Ordinal))
            .Where(o => string.Equals(o.UserId, powerCreep.UserId, StringComparison.Ordinal))
            .ToList();

        if (extensions.Count == 0)
            return;

        // Calculate fill amount based on level
        var effectMultiplier = powerInfo.EffectMultipliers![level - 1];
        var totalExtensionCapacity = extensions.Sum(e => e.StoreCapacity ?? 0);
        var totalFillAmount = (int)(totalExtensionCapacity * effectMultiplier);
        var actualFillAmount = Math.Min(totalFillAmount, targetEnergy);

        if (actualFillAmount == 0)
            return;

        // Deduct energy from source
        if (!storeLedger.TryGetValue(target.Id, out var sourceStore))
        {
            sourceStore = new Dictionary<string, int>(target.Store);
            storeLedger[target.Id] = sourceStore;
        }
        sourceStore[ResourceTypes.Energy] = targetEnergy - actualFillAmount;
        modifiedObjects.Add(target.Id);

        // Distribute energy to extensions proportionally
        var remainingEnergy = actualFillAmount;
        foreach (var extension in extensions)
        {
            if (remainingEnergy == 0)
                break;

            var currentEnergy = storeLedger.TryGetValue(extension.Id, out var extStore)
                ? extStore.GetValueOrDefault(ResourceTypes.Energy, extension.Store.GetValueOrDefault(ResourceTypes.Energy, 0))
                : extension.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

            var capacity = extension.StoreCapacity ?? 0;
            var freeSpace = capacity - currentEnergy;

            if (freeSpace == 0)
                continue;

            var transferAmount = Math.Min(freeSpace, remainingEnergy);

            if (!storeLedger.TryGetValue(extension.Id, out var ledgerExtStore))
            {
                ledgerExtStore = new Dictionary<string, int>(extension.Store);
                storeLedger[extension.Id] = ledgerExtStore;
            }

            ledgerExtStore[ResourceTypes.Energy] = currentEnergy + transferAmount;
            modifiedObjects.Add(extension.Id);

            remainingEnergy -= transferAmount;
        }
    }

    private static void ProcessShield(
        RoomProcessorContext context,
        RoomObjectSnapshot powerCreep,
        int level,
        int gameTime,
        PowerAbilityInfo powerInfo,
        Dictionary<string, Dictionary<string, int>> storeLedger,
        HashSet<string> modifiedObjects)
    {
        // Shield uses energy, not ops
        var energyCost = powerInfo.Energy ?? 0;

        // Check energy availability
        var currentEnergy = storeLedger.TryGetValue(powerCreep.Id, out var creepStore)
            ? creepStore.GetValueOrDefault(ResourceTypes.Energy, powerCreep.Store.GetValueOrDefault(ResourceTypes.Energy, 0))
            : powerCreep.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (currentEnergy < energyCost)
            return;

        // Check if rampart already exists at position
        var existingRampart = context.State.Objects.Values.FirstOrDefault(o =>
            string.Equals(o.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal) &&
            o.X == powerCreep.X &&
            o.Y == powerCreep.Y);

        if (existingRampart is not null)
            return;

        // Deduct energy
        if (!storeLedger.TryGetValue(powerCreep.Id, out var ledgerStore))
        {
            ledgerStore = new Dictionary<string, int>(powerCreep.Store);
            storeLedger[powerCreep.Id] = ledgerStore;
        }

        ledgerStore[ResourceTypes.Energy] = currentEnergy - energyCost;
        modifiedObjects.Add(powerCreep.Id);

        // Create temporary rampart
        var hits = powerInfo.Effect![level - 1];
        var duration = powerInfo.Duration ?? 0;
        var decayTime = gameTime + duration;

        var rampart = new RoomObjectSnapshot(
            Id: Guid.NewGuid().ToString(),
            Type: RoomObjectTypes.Rampart,
            RoomName: powerCreep.RoomName,
            Shard: powerCreep.Shard,
            UserId: powerCreep.UserId,
            X: powerCreep.X,
            Y: powerCreep.Y,
            Hits: hits,
            HitsMax: hits,
            Fatigue: null,
            TicksToLive: null,
            Name: null,
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(Comparer),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(Comparer),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Spawning: null,
            Body: [],
            DecayTime: decayTime);

        context.MutationWriter.Upsert(rampart);
    }
}
