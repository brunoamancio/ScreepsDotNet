using System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemStatusCommand(ISystemControlService controlService, ILogger<SystemStatusCommand>? logger = null, IHostApplicationLifetime? lifetime = null) : CommandHandler<SystemStatusCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var paused = await controlService.IsSimulationPausedAsync(cancellationToken).ConfigureAwait(false);
        var tickDuration = await controlService.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                paused,
                tickDuration
            };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Simulation paused", paused ? "yes" : "no");
        table.AddRow("Tick duration (ms)", tickDuration?.ToString() ?? "not set");
        AnsiConsole.Write(table);
        return 0;
    }
}
