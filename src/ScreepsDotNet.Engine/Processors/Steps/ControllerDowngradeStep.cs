using ScreepsDotNet.Common.Constants;

namespace ScreepsDotNet.Engine.Processors.Steps;

using ScreepsDotNet.Driver.Contracts;
using ScreepsDotNet.Engine.Processors;

/// <summary>
/// Applies a lightweight controller downgrade timer so rooms without active users eventually lose ownership.
/// </summary>
internal sealed class ControllerDowngradeStep : IRoomProcessorStep
{
    public Task ExecuteAsync(RoomProcessorContext context, CancellationToken token = default)
    {
        foreach (var controller in context.State.Objects.Values) {
            if (controller.Type != RoomObjectTypes.Controller)
                continue;

            var timer = controller.ControllerDowngradeTimer;
            if (!timer.HasValue)
                continue;

            var next = Math.Max(timer.Value - 1, 0);
            var patch = new RoomObjectPatchPayload
            {
                DowngradeTimer = next,
                UpgradeBlocked = next == 0 ? true : null
            };

            context.MutationWriter.Patch(controller.Id, patch);
        }

        return Task.CompletedTask;
    }
}
