namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors.Helpers;

internal sealed class TowerIntentStep(ICreepDeathProcessor deathProcessor) : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var energyLedger = new Dictionary<string, int>(Comparer);
        var deathEnergyLedger = new Dictionary<string, int>(Comparer);

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var tower))
                    continue;

                if (!string.Equals(tower.Type, RoomObjectTypes.Tower, StringComparison.Ordinal))
                    continue;

                // Check structure activation (requires controller ownership and RCL limits)
                var controller = StructureActivationHelper.FindController(context.State.Objects);
                if (!StructureActivationHelper.IsStructureActive(tower, context.State.Objects, controller))
                    continue;

                foreach (var record in records)
                    ProcessRecord(context, tower, record, energyLedger, deathEnergyLedger);
            }
        }

        return Task.CompletedTask;
    }

    private void ProcessRecord(RoomProcessorContext context, RoomObjectSnapshot tower, IntentRecord record, Dictionary<string, int> energyLedger, Dictionary<string, int> deathEnergyLedger)
    {
        if (record?.Arguments is null || record.Arguments.Count == 0)
            return;

        if (!TryGetTargetId(record, out var targetId))
            return;

        if (!context.State.Objects.TryGetValue(targetId, out var target))
            return;

        switch (record.Name) {
            case "attack":
                HandleAttack(context, tower, target, energyLedger, deathEnergyLedger);
                break;
            case "heal":
                HandleHeal(context, tower, target, energyLedger);
                break;
            case "repair":
                HandleRepair(context, tower, target, energyLedger);
                break;
            default:
                break;
        }
    }

    private void HandleAttack(RoomProcessorContext context, RoomObjectSnapshot tower, RoomObjectSnapshot target, Dictionary<string, int> energyLedger, Dictionary<string, int> deathEnergyLedger)
    {
        if (!TryConsumeEnergy(context, tower, energyLedger))
            return;

        var resolvedTarget = ResolveRampartTarget(context, target);
        var amount = CalculateTowerEffect(ScreepsGameConstants.TowerPowerAttack, tower, resolvedTarget);
        if (amount <= 0)
            return;

        ApplyDamage(context, resolvedTarget, amount, deathEnergyLedger);
    }

    private static void HandleHeal(RoomProcessorContext context, RoomObjectSnapshot tower, RoomObjectSnapshot target, Dictionary<string, int> energyLedger)
    {
        if (!IsHealingTarget(target))
            return;

        if (!TryConsumeEnergy(context, tower, energyLedger))
            return;

        var amount = CalculateTowerEffect(ScreepsGameConstants.TowerPowerHeal, tower, target);
        if (amount <= 0)
            return;

        var hits = target.Hits ?? 0;
        var hitsMax = target.HitsMax ?? hits;
        var newHits = Math.Min(hits + amount, hitsMax);

        context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
        {
            Hits = newHits,
            ActionLog = new RoomObjectActionLogPatch(
                Healed: new RoomObjectActionLogHealed(tower.X, tower.Y))
        });
    }

    private static void HandleRepair(RoomProcessorContext context, RoomObjectSnapshot tower, RoomObjectSnapshot target, Dictionary<string, int> energyLedger)
    {
        if (!HasStructureHits(target))
            return;

        if (!TryConsumeEnergy(context, tower, energyLedger))
            return;

        var amount = CalculateTowerEffect(ScreepsGameConstants.TowerPowerRepair, tower, target);
        if (amount <= 0)
            return;

        var hits = target.Hits ?? 0;
        var hitsMax = target.HitsMax ?? hits;
        var newHits = Math.Min(hits + amount, hitsMax);

        context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
        {
            Hits = newHits
        });

        if (!string.IsNullOrWhiteSpace(tower.UserId))
            context.Stats.IncrementEnergyConstruction(tower.UserId!, ScreepsGameConstants.TowerEnergyCost);
    }

    private void ApplyDamage(RoomProcessorContext context, RoomObjectSnapshot target, int damage, Dictionary<string, int> deathEnergyLedger)
    {
        if (damage <= 0)
            return;

        var hits = target.Hits ?? 0;
        var remaining = Math.Max(hits - damage, 0);
        if (remaining > 0) {
            context.MutationWriter.Patch(target.Id, new RoomObjectPatchPayload
            {
                Hits = remaining
            });
            return;
        }

        if (target.IsCreep())
            deathProcessor.Process(context, target, new CreepDeathOptions(ViolentDeath: true), deathEnergyLedger);
        else
            context.MutationWriter.Remove(target.Id);
    }

    private static bool TryConsumeEnergy(RoomProcessorContext context, RoomObjectSnapshot tower, Dictionary<string, int> energyLedger)
    {
        const string key = RoomDocumentFields.RoomObject.Store.Energy;
        var current = energyLedger.TryGetValue(tower.Id, out var overrideValue) ? overrideValue : tower.Store.GetValueOrDefault(key, 0);
        if (current < ScreepsGameConstants.TowerEnergyCost)
            return false;

        var remaining = current - ScreepsGameConstants.TowerEnergyCost;
        energyLedger[tower.Id] = remaining;

        context.MutationWriter.Patch(tower.Id, new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(1, Comparer)
            {
                [key] = remaining
            }
        });

        return true;
    }

    private static int CalculateTowerEffect(int basePower, RoomObjectSnapshot tower, RoomObjectSnapshot target)
    {
        var range = Math.Max(Math.Abs(tower.X - target.X), Math.Abs(tower.Y - target.Y));
        if (range <= ScreepsGameConstants.TowerOptimalRange)
            return basePower;

        const int falloffRange = ScreepsGameConstants.TowerFalloffRange;
        const int optimalRange = ScreepsGameConstants.TowerOptimalRange;
        var clampedRange = Math.Min(range, falloffRange);
        var numerator = clampedRange - optimalRange;
        var denominator = Math.Max(falloffRange - optimalRange, 1);
        var amount = basePower - (basePower * ScreepsGameConstants.TowerFalloff * numerator / denominator);
        return (int)Math.Floor(amount);
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        targetId = string.Empty;
        if (record?.Arguments is null || record.Arguments.Count == 0)
            return false;

        var fields = record.Arguments[0].Fields;
        if (!fields.TryGetValue("id", out var value))
            return false;

        targetId = value.Kind switch
        {
            IntentFieldValueKind.Text => value.TextValue ?? string.Empty,
            IntentFieldValueKind.Number => value.NumberValue?.ToString() ?? string.Empty,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(targetId);
    }

    private static RoomObjectSnapshot ResolveRampartTarget(RoomProcessorContext context, RoomObjectSnapshot target)
    {
        foreach (var obj in context.State.Objects.Values) {
            if (string.Equals(obj.Type, RoomObjectTypes.Rampart, StringComparison.Ordinal) &&
                obj.X == target.X &&
                obj.Y == target.Y &&
                string.Equals(obj.RoomName, target.RoomName, StringComparison.Ordinal)) {
                return obj;
            }
        }

        return target;
    }

    private static bool IsHealingTarget(RoomObjectSnapshot obj)
        => obj.IsCreep();

    private static bool HasStructureHits(RoomObjectSnapshot obj)
        => obj.Hits.HasValue && obj.HitsMax.GetValueOrDefault() > 0;
}
