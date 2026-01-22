using ScreepsDotNet.Engine.Validation;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.Engine;

internal sealed class ValidationStatsCommand(IValidationStatisticsSink statisticsSink, ILogger<ValidationStatsCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<ValidationStatsCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--reset")]
        public bool Reset { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var stats = statisticsSink.GetStatistics();

        if (settings.Reset) {
            statisticsSink.Reset();
            OutputFormatter.WriteLine("Validation statistics reset.");
        }

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(stats);
            return Task.FromResult(0);
        }

        var rejectionRate = stats.TotalIntentsValidated > 0
            ? (double)stats.RejectedIntentsCount / stats.TotalIntentsValidated
            : 0;

        OutputFormatter.WriteKeyValueTable([
            ("Total validated", stats.TotalIntentsValidated.ToString()),
            ("Valid intents", stats.ValidIntentsCount.ToString()),
            ("Rejected intents", stats.RejectedIntentsCount.ToString()),
            ("Rejection rate", $"{rejectionRate:P2}")
        ], "Validation Statistics");

        // Top error codes table
        var topErrors = stats.RejectionsByErrorCode
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .ToList();

        if (topErrors.Count > 0) {
            OutputFormatter.WriteTabularData(
                "Top Rejection Errors",
                ["Error Code", "Count"],
                topErrors.Select(e => (IReadOnlyList<string>)[e.Key.ToString(), e.Value.ToString()]));
        }

        // Top intent types table
        var topIntents = stats.RejectionsByIntentType
            .OrderByDescending(kvp => kvp.Value)
            .Take(10)
            .ToList();

        if (topIntents.Count > 0) {
            OutputFormatter.WriteTabularData(
                "Top Rejected Intent Types",
                ["Intent Type", "Count"],
                topIntents.Select(i => (IReadOnlyList<string>)[i.Key, i.Value.ToString()]));
        }

        return Task.FromResult(0);
    }
}
