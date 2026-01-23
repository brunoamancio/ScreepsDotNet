namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

internal sealed class ControllerIntentStep : IRoomProcessorStep
{
    private static readonly StringComparer Comparer = StringComparer.Ordinal;

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var intents = context.State.Intents;
        if (intents?.Users is null || intents.Users.Count == 0)
            return Task.CompletedTask;

        var energyLedger = new Dictionary<string, int>(Comparer);
        var controllerProgressLedger = new Dictionary<string, int>(Comparer);

        foreach (var envelope in intents.Users.Values) {
            if (envelope?.ObjectIntents is null || envelope.ObjectIntents.Count == 0)
                continue;

            foreach (var (objectId, records) in envelope.ObjectIntents) {
                if (string.IsNullOrWhiteSpace(objectId) || records.Count == 0)
                    continue;

                if (!context.State.Objects.TryGetValue(objectId, out var actor))
                    continue;

                foreach (var record in records) {
                    switch (record.Name) {
                        case IntentKeys.UpgradeController:
                            ProcessUpgrade(context, actor, record, energyLedger, controllerProgressLedger);
                            break;
                        case IntentKeys.ReserveController:
                            ProcessReserve(context, actor, record);
                            break;
                        case IntentKeys.AttackController:
                            ProcessAttack(context, actor, record);
                            break;
                    }
                }
            }
        }

        foreach (var (creepId, energyRemaining) in energyLedger) {
            context.MutationWriter.Patch(creepId, new RoomObjectPatchPayload
            {
                Store = new Dictionary<string, int>(Comparer)
                {
                    [ResourceTypes.Energy] = energyRemaining
                }
            });
        }

        foreach (var (controllerId, newProgress) in controllerProgressLedger) {
            if (!context.State.Objects.TryGetValue(controllerId, out var controller))
                continue;

            var patch = new RoomObjectPatchPayload
            {
                Progress = newProgress,
                ActionLog = new RoomObjectActionLogPatch()
            };

            var level = controller.Level ?? 0;
            if (level < 8 && TryCalculateLevelUp(context, controller, newProgress, out var levelUpPatch))
                patch = levelUpPatch;

            context.MutationWriter.Patch(controllerId, patch);
        }

        return Task.CompletedTask;
    }

    private static void ProcessUpgrade(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record, Dictionary<string, int> energyLedger, Dictionary<string, int> controllerProgressLedger)
    {
        if (!creep.IsCreep(includePowerCreep: false))
            return;

        if (creep.IsSpawning == true)
            return;

        if (!TryGetTargetId(record, out var controllerId))
            return;

        if (!context.State.Objects.TryGetValue(controllerId, out var controller))
            return;

        if (!string.Equals(controller.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
            return;

        if (!IsInRange(creep, controller, 3))
        {
            // Emit ActionLog for out-of-range attempt
            context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
            {
                ActionLog = new RoomObjectActionLogPatch()
            });
            return;
        }

        if (!string.Equals(controller.UserId, creep.UserId, StringComparison.Ordinal))
        {
            // Emit ActionLog for wrong-owner attempt
            context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
            {
                ActionLog = new RoomObjectActionLogPatch()
            });
            return;
        }

        var upgradeBlocked = controller.Store.GetValueOrDefault(RoomDocumentFields.RoomObject.UpgradeBlocked, 0);
        if (upgradeBlocked > 0)
        {
            // Emit ActionLog for upgrade-blocked attempt
            context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
            {
                ActionLog = new RoomObjectActionLogPatch()
            });
            return;
        }

        var workParts = CalculateBaseWorkParts(creep);
        if (workParts <= 0)
        {
            // Emit ActionLog for no-work-parts attempt
            context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
            {
                ActionLog = new RoomObjectActionLogPatch()
            });
            return;
        }

        var availableEnergy = energyLedger.TryGetValue(creep.Id, out var ledgerEnergy)
            ? ledgerEnergy
            : creep.Store.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (availableEnergy <= 0)
        {
            // Emit ActionLog for no-energy attempt
            context.MutationWriter.Patch(creep.Id, new RoomObjectPatchPayload
            {
                ActionLog = new RoomObjectActionLogPatch()
            });
            return;
        }

        var level = controller.Level ?? 0;
        var maxPerTick = level == 8 ? ScreepsGameConstants.ControllerMaxUpgradePerTick : workParts;
        var energyToConsume = Math.Min(workParts, Math.Min(availableEnergy, maxPerTick));

        var boostEffect = CalculateBoostEffect(creep, energyToConsume);
        var progressGain = energyToConsume + boostEffect;

        var newEnergy = availableEnergy - energyToConsume;
        energyLedger[creep.Id] = newEnergy;

        var currentProgress = controllerProgressLedger.TryGetValue(controllerId, out var ledgerProgress)
            ? ledgerProgress
            : controller.Progress ?? 0;

        var newProgress = currentProgress + progressGain;
        controllerProgressLedger[controllerId] = newProgress;

        context.GlobalMutationWriter.IncrementUserGcl(creep.UserId!, progressGain);

        context.Stats.IncrementEnergyControl(creep.UserId!, energyToConsume);
    }

    private static void ProcessReserve(RoomProcessorContext context, RoomObjectSnapshot creep, IntentRecord record)
    {
        if (!creep.IsCreep(includePowerCreep: false))
            return;

        if (creep.IsSpawning == true)
            return;

        if (!TryGetTargetId(record, out var controllerId))
            return;

        if (!context.State.Objects.TryGetValue(controllerId, out var controller))
            return;

        if (!string.Equals(controller.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
            return;

        if (!IsInRange(creep, controller, 1))
            return;

        if (!string.IsNullOrWhiteSpace(controller.UserId))
            return;

        if (controller.Reservation is not null &&
            !string.Equals(controller.Reservation.UserId, creep.UserId, StringComparison.Ordinal)) {
            return;
        }

        var claimParts = creep.Body.Count(p => p.Type == BodyPartType.Claim && p.Hits > 0);
        var effect = claimParts * ScreepsGameConstants.ControllerReserve;

        if (effect <= 0)
            return;

        var gameTime = context.State.GameTime;
        var currentEndTime = controller.Reservation?.EndTime ?? gameTime;
        var newEndTime = currentEndTime + effect;

        if (newEndTime > gameTime + ScreepsGameConstants.ControllerReserveMax)
            return;

        var reservation = new RoomReservationSnapshot(creep.UserId, newEndTime);

        context.MutationWriter.Patch(controller.Id, new RoomObjectPatchPayload
        {
            Reservation = reservation,
            ActionLog = new RoomObjectActionLogPatch()
        });
    }

    private static void ProcessAttack(RoomProcessorContext context, RoomObjectSnapshot invaderCore, IntentRecord record)
    {
        if (!string.Equals(invaderCore.Type, RoomObjectTypes.InvaderCore, StringComparison.Ordinal))
            return;

        if (invaderCore.IsSpawning == true)
            return;

        if (!TryGetTargetId(record, out var controllerId))
            return;

        if (!context.State.Objects.TryGetValue(controllerId, out var controller))
            return;

        if (!string.Equals(controller.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
            return;

        if (string.IsNullOrWhiteSpace(controller.UserId) && controller.Reservation is null)
            return;

        var safeMode = controller.SafeMode;
        if (safeMode.HasValue && safeMode.Value > 0)
            return;

        var upgradeBlocked = controller.Store.GetValueOrDefault(RoomDocumentFields.RoomObject.UpgradeBlocked, 0);
        if (upgradeBlocked > 0)
            return;

        var gameTime = context.State.GameTime;
        const int invaderCorePower = 300;

        var patch = new RoomObjectPatchPayload
        {
            UpgradeBlocked = true,
            ActionLog = new RoomObjectActionLogPatch()
        };

        if (controller.Reservation is not null) {
            var reduction = invaderCorePower * ScreepsGameConstants.ControllerReserve;
            var newEndTime = Math.Max(0, controller.Reservation.EndTime!.Value - reduction);
            patch = patch with
            {
                Reservation = controller.Reservation with { EndTime = newEndTime }
            };
        }

        if (!string.IsNullOrWhiteSpace(controller.UserId)) {
            var downgradeTimer = controller.ControllerDowngradeTimer ?? 0;
            var reduction = invaderCorePower * ScreepsGameConstants.ControllerClaimDowngrade;
            var newDowngradeTimer = Math.Max(0, downgradeTimer - reduction);
            patch = patch with
            {
                DowngradeTimer = newDowngradeTimer
            };
        }

        context.MutationWriter.Patch(controller.Id, patch);
    }

    private static bool TryGetTargetId(IntentRecord record, out string targetId)
    {
        if (record.Arguments is null || record.Arguments.Count == 0) {
            targetId = string.Empty;
            return false;
        }

        var argument = record.Arguments[0];
        if (!argument.Fields.TryGetValue(IntentKeys.TargetId, out var field) ||
            field.Kind != IntentFieldValueKind.Text ||
            string.IsNullOrWhiteSpace(field.TextValue)) {
            targetId = string.Empty;
            return false;
        }

        targetId = field.TextValue;
        return true;
    }

    private static int CalculateBaseWorkParts(RoomObjectSnapshot creep)
    {
        var workParts = 0;

        foreach (var part in creep.Body) {
            if (part.Type != BodyPartType.Work)
                continue;

            if (part.Hits <= 0)
                continue;

            workParts += ScreepsGameConstants.UpgradeControllerPower;
        }

        return workParts;
    }

    private static int CalculateBoostEffect(RoomObjectSnapshot creep, int energyToConsume)
    {
        var boostEffects = new List<double>();

        foreach (var part in creep.Body) {
            if (part.Type != BodyPartType.Work)
                continue;

            if (part.Hits <= 0)
                continue;

            if (string.IsNullOrWhiteSpace(part.Boost))
                continue;

            if (!ScreepsGameConstants.WorkBoostUpgradeMultipliers.TryGetValue(part.Boost, out var multiplier))
                continue;

            var extraPower = multiplier - 1.0;
            boostEffects.Add(extraPower);
        }

        if (boostEffects.Count == 0)
            return 0;

        boostEffects.Sort((a, b) => b.CompareTo(a));

        var boostedParts = boostEffects.Take(energyToConsume).Sum();
        var result = (int)Math.Floor(boostedParts);
        return result;
    }

    private static bool IsInRange(RoomObjectSnapshot source, RoomObjectSnapshot target, int maxRange)
    {
        var dx = Math.Abs(source.X - target.X);
        var dy = Math.Abs(source.Y - target.Y);
        var result = dx <= maxRange && dy <= maxRange;
        return result;
    }

    private static bool TryCalculateLevelUp(
        RoomProcessorContext context,
        RoomObjectSnapshot controller,
        int newProgress,
        out RoomObjectPatchPayload patch)
    {
        var currentLevel = (ControllerLevel)(controller.Level ?? 0);

        if (currentLevel == ControllerLevel.Level8) {
            patch = default!;
            return false;
        }

        if (!ScreepsGameConstants.ControllerLevelProgress.TryGetValue(currentLevel, out var threshold)) {
            patch = default!;
            return false;
        }

        if (newProgress < threshold) {
            patch = default!;
            return false;
        }

        var nextLevel = (ControllerLevel)((int)currentLevel + 1);
        var gameTime = context.State.GameTime;

        var currentDowngrade = controller.ControllerDowngradeTimer ?? 0;

        if (!ScreepsGameConstants.ControllerDowngradeTimers.TryGetValue(nextLevel, out var nextLevelDowngradeMax)) {
            patch = default!;
            return false;
        }

        var minRequired = gameTime + nextLevelDowngradeMax - ScreepsGameConstants.ControllerDowngradeRestore;

        if (currentDowngrade < minRequired) {
            patch = default!;
            return false;
        }

        var remainingProgress = newProgress - threshold;
        var newDowngradeTime = gameTime + (nextLevelDowngradeMax / 2);

        var currentSafeModeAvailable = controller.SafeModeAvailable ?? 0;
        var newSafeModeAvailable = currentSafeModeAvailable + 1;

        patch = new RoomObjectPatchPayload
        {
            Progress = nextLevel == ControllerLevel.Level8 ? 0 : remainingProgress,
            DowngradeTimer = newDowngradeTime,
            SafeModeAvailable = newSafeModeAvailable,
            ActionLog = new RoomObjectActionLogPatch()
        };

        var infoPatch = new RoomInfoPatchPayload
        {
            ControllerLevel = nextLevel
        };

        context.MutationWriter.SetRoomInfoPatch(infoPatch);

        return true;
    }
}
