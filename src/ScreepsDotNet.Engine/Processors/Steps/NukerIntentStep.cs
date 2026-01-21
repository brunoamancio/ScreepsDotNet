namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;
using ScreepsDotNet.Common.Utilities;
using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Processes nuker launchNuke intents.
/// Node.js parity: engine/src/processor/intents/nukers/launch-nuke.js
/// </summary>
internal sealed class NukerIntentStep : IRoomProcessorStep
{

    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        if (context.State.Intents is null)
            return Task.CompletedTask;

        foreach (var userEnvelope in context.State.Intents.Users.Values) {
            foreach (var (objectId, intentRecords) in userEnvelope.ObjectIntents) {
                if (!context.State.Objects.TryGetValue(objectId, out var obj))
                    continue;

                if (obj.Type != RoomObjectTypes.Nuker)
                    continue;

                foreach (var record in intentRecords) {
                    if (record.Name != IntentKeys.LaunchNuke)
                        continue;

                    ProcessLaunchNuke(context, obj, record);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void ProcessLaunchNuke(RoomProcessorContext context, RoomObjectSnapshot nuker, IntentRecord record)
    {
        // Get intent arguments
        var arg = record.Arguments[0];

        if (!arg.Fields.TryGetValue(NukerIntentFields.RoomName, out var roomNameField) || roomNameField.TextValue is null)
            return;

        if (!arg.Fields.TryGetValue(NukerIntentFields.X, out var xField) || xField.NumberValue is null)
            return;

        if (!arg.Fields.TryGetValue(NukerIntentFields.Y, out var yField) || yField.NumberValue is null)
            return;

        var targetRoomName = roomNameField.TextValue;
        var targetX = xField.NumberValue.Value;
        var targetY = yField.NumberValue.Value;

        var currentEnergy = nuker.Store.GetValueOrDefault(ResourceTypes.Energy, 0);
        var currentGhodium = nuker.Store.GetValueOrDefault(ResourceTypes.Ghodium, 0);

        if (currentEnergy < ScreepsGameConstants.NukerEnergyCapacity)
            return;

        if (currentGhodium < ScreepsGameConstants.NukerGhodiumCapacity)
            return;

        // Validate cooldown
        if (nuker.CooldownTime is not null && nuker.CooldownTime > context.State.GameTime)
            return;

        // Validate target coordinates
        if (targetX < 0 || targetX > 49 || targetY < 0 || targetY > 49)
            return;

        // Validate room name format and calculate range
        if (!RoomCoordinateHelper.TryParse(targetRoomName, out var targetRoomX, out var targetRoomY))
            return;

        if (!RoomCoordinateHelper.TryParse(nuker.RoomName, out var nukerRoomX, out var nukerRoomY))
            return;

        var rangeX = Math.Abs(targetRoomX - nukerRoomX);
        var rangeY = Math.Abs(targetRoomY - nukerRoomY);

        if (rangeX > ScreepsGameConstants.NukeRange || rangeY > ScreepsGameConstants.NukeRange)
            return;

        // Consume resources and set cooldown
        var patch = new RoomObjectPatchPayload
        {
            Store = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [ResourceTypes.Energy] = 0,
                [ResourceTypes.Ghodium] = 0
            },
            CooldownTime = context.State.GameTime + ScreepsGameConstants.NukerCooldown
        };

        context.MutationWriter.Patch(nuker.Id, patch);

        // Create nuke object in target room
        var landTime = context.State.GameTime + ScreepsGameConstants.NukeLandTime;
        var nuke = new RoomObjectSnapshot(
            Id: Guid.NewGuid().ToString(),
            Type: RoomObjectTypes.Nuke,
            RoomName: targetRoomName,
            Shard: nuker.Shard,
            UserId: nuker.UserId,
            X: targetX,
            Y: targetY,
            Hits: null,
            HitsMax: null,
            Fatigue: null,
            TicksToLive: null,
            Name: nuker.RoomName, // Store launch room name in Name field
            Level: null,
            Density: null,
            MineralType: null,
            DepositType: null,
            StructureType: null,
            Store: new Dictionary<string, int>(StringComparer.Ordinal),
            StoreCapacity: null,
            StoreCapacityResource: new Dictionary<string, int>(StringComparer.Ordinal),
            Reservation: null,
            Sign: null,
            Structure: null,
            Effects: new Dictionary<PowerTypes, PowerEffectSnapshot>(),
            Body: [],
            NextRegenerationTime: landTime);

        context.GlobalMutationWriter.UpsertRoomObject(nuke);
    }
}
