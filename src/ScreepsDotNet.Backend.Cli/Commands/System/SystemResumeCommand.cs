using System.Globalization;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemResumeCommand(ISystemControlService controlService, ILogger<SystemResumeCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<SystemResumeCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await controlService.ResumeSimulationAsync(cancellationToken).ConfigureAwait(false);
        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { resumed = true });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Simulation", "resumed"),
                                               ("Timestamp", DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture))
                                           ],
                                           "System resume");
        return 0;
    }
}
