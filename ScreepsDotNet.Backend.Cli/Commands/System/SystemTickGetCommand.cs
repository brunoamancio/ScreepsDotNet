using System.Globalization;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemTickGetCommand(ISystemControlService controlService, ILogger<SystemTickGetCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<SystemTickGetCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var duration = await controlService.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { tickDuration = duration });
            return 0;
        }

        if (duration is null) {
            OutputFormatter.WriteKeyValueTable([("Tick Duration (ms)", "not set")], "Tick duration");
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Tick Duration (ms)", duration.Value.ToString(CultureInfo.InvariantCulture))
                                           ],
                                           "Tick duration");
        return 0;
    }
}
