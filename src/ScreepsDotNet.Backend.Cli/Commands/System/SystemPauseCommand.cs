using System.Globalization;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemPauseCommand(ISystemControlService controlService, ILogger<SystemPauseCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<SystemPauseCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await controlService.PauseSimulationAsync(cancellationToken).ConfigureAwait(false);
        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { paused = true });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Simulation", "paused"),
                                               ("Timestamp", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture))
                                           ],
                                           "System pause");
        return 0;
    }
}
