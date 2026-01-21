using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Driver.Contracts;

namespace ScreepsDotNet.Engine.Processors.Steps;

/// <summary>
/// Applies passive source energy regeneration, power effect handling, and capacity adjustments based on room ownership.
/// </summary>
internal sealed class SourceRegenerationStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        var gameTime = context.State.GameTime;
        var roomController = FindRoomController(context.State.Objects);

        foreach (var source in context.State.Objects.Values) {
            if (source.Type != RoomObjectTypes.Source)
                continue;

            ProcessSource(context, source, gameTime, roomController);
        }

        return Task.CompletedTask;
    }

    private static void ProcessSource(RoomProcessorContext context, RoomObjectSnapshot source, int gameTime, RoomObjectSnapshot? controller)
    {
        var energy = source.Energy ?? 0;
        var currentCapacity = source.StoreCapacityResource.GetValueOrDefault(ResourceTypes.Energy, 0);

        if (currentCapacity == 0)
            return;

        // Determine target capacity based on room status
        var roomType = context.State.Info?.Type ?? RoomType.Unknown;
        var targetCapacity = GetTargetCapacity(controller, roomType);

        // Build patch with all needed changes
        int? newNextRegenerationTime = null;
        int? newEnergy = null;
        Dictionary<string, int>? newStoreCapacityResource = null;
        var hasCapacityChange = targetCapacity.HasValue && currentCapacity != targetCapacity.Value;
        var hasRegenerationChange = false;
        var hasPowerEffectChange = false;

        // Skip if source is full AND no capacity adjustment needed (no regeneration or power effects needed)
        if (energy >= currentCapacity && !hasCapacityChange)
            return;

        // 1. Handle capacity adjustment (only when room ownership is known)
        if (hasCapacityChange) {
            newStoreCapacityResource = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = targetCapacity!.Value
            };

            // Clamp energy if capacity reduced
            if (energy > targetCapacity.Value) {
                newEnergy = targetCapacity.Value;
                energy = targetCapacity.Value;  // Update local copy for further processing
            }
        }

        // 2. Handle passive regeneration (always use currentCapacity before adjustment)
        var regenCapacity = currentCapacity;
        if (energy < regenCapacity) {
            if (!source.NextRegenerationTime.HasValue) {
                // Set initial regeneration time
                newNextRegenerationTime = gameTime + ScreepsGameConstants.EnergyRegenTime;
                hasRegenerationChange = true;
            }
            else {
                // Start with current timer value
                var effectiveRegenerationTime = source.NextRegenerationTime.Value;

                // Check if DISRUPT_SOURCE effect is active
                var hasDisruptEffect = source.Effects.TryGetValue(PowerTypes.DisruptSource, out var disruptEffect) &&
                                      disruptEffect.EndTime > gameTime;

                if (hasDisruptEffect) {
                    // Delay regeneration by 1 tick (update effective time)
                    effectiveRegenerationTime += 1;
                    newNextRegenerationTime = effectiveRegenerationTime;
                    hasRegenerationChange = true;
                }

                // Check if regeneration time has been reached (use effective time after DISRUPT delay)
                if (gameTime >= effectiveRegenerationTime - 1) {
                    newNextRegenerationTime = null;
                    newEnergy = regenCapacity;
                    hasRegenerationChange = true;
                }
            }
        }

        // 3. Handle PWR_REGEN_SOURCE effect
        if (energy < regenCapacity && source.Effects.TryGetValue(PowerTypes.RegenSource, out var regenEffect) && regenEffect.EndTime > gameTime) {
            var powerInfo = PowerInfo.Abilities[PowerTypes.RegenSource];
            if (powerInfo.Period.HasValue) {
                var period = powerInfo.Period.Value;
                var ticksUntilEnd = regenEffect.EndTime - gameTime;

                if (ticksUntilEnd % period == 0) {
                    var energyToAdd = powerInfo.Effect![regenEffect.Level - 1];
                    var currentEnergy = newEnergy ?? energy;
                    newEnergy = Math.Min(regenCapacity, currentEnergy + energyToAdd);
                    hasPowerEffectChange = true;
                }
            }
        }

        // Emit single patch if any changes
        if (hasCapacityChange || hasRegenerationChange || hasPowerEffectChange) {
            var patch = new RoomObjectPatchPayload
            {
                StoreCapacityResource = newStoreCapacityResource,
                Energy = newEnergy,
                NextRegenerationTime = newNextRegenerationTime
            };
            context.MutationWriter.Patch(source.Id, patch);
        }
    }

    private static int? GetTargetCapacity(RoomObjectSnapshot? controller, RoomType roomType)
    {
        // Keeper rooms always use keeper capacity (4000)
        if (roomType == RoomType.Keeper)
            return ScreepsGameConstants.SourceEnergyKeeperCapacity;

        // Highway rooms have no sources, unknown rooms have ambiguous state
        if (roomType is RoomType.Highway or RoomType.Unknown)
            return null;

        // Normal rooms with no controller - uncertain state
        if (controller is null)
            return null;

        // Owned or reserved rooms use standard capacity (3000)
        if (!string.IsNullOrWhiteSpace(controller.UserId) ||
            !string.IsNullOrWhiteSpace(controller.Reservation?.UserId))
            return ScreepsGameConstants.SourceEnergyCapacity;

        // Neutral rooms use reduced capacity (1500)
        var result = ScreepsGameConstants.SourceEnergyNeutralCapacity;
        return result;
    }

    private static RoomObjectSnapshot? FindRoomController(IReadOnlyDictionary<string, RoomObjectSnapshot> objects)
    {
        foreach (var obj in objects.Values) {
            if (string.Equals(obj.Type, RoomObjectTypes.Controller, StringComparison.Ordinal))
                return obj;
        }
        return null;
    }
}
