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
}
