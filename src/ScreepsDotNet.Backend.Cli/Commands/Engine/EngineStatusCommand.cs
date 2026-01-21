using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.Engine;

internal sealed class EngineStatusCommand(IEngineDiagnosticsService diagnosticsService, ILogger<EngineStatusCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<EngineStatusCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var stats = await diagnosticsService.GetEngineStatisticsAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson)
        {
            OutputFormatter.WriteJson(stats);
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
            ("Rooms processed", stats.TotalRoomsProcessed.ToString()),
            ("Avg processing time (ms)", stats.AverageProcessingTimeMs.ToString("F2")),
            ("Total intents validated", stats.TotalIntentsValidated.ToString()),
            ("Rejection rate", $"{stats.RejectionRate:P2}"),
            ("Top error code", stats.TopErrorCode ?? "None"),
            ("Top intent type", stats.TopIntentType ?? "None")
        ], "Engine Status");

        return 0;
    }
}
