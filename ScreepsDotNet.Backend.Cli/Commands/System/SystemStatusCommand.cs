using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemStatusCommand(ISystemControlService controlService, ILogger<SystemStatusCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<SystemStatusCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var paused = await controlService.IsSimulationPausedAsync(cancellationToken).ConfigureAwait(false);
        var tickDuration = await controlService.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { paused, tickDuration });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Simulation paused", paused ? "yes" : "no"),
                                               ("Tick duration (ms)", tickDuration?.ToString() ?? "not set")
                                           ],
                                           "System status");
        return 0;
    }
}
