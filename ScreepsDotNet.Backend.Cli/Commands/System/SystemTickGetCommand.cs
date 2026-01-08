using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemTickGetCommand(ISystemControlService controlService) : AsyncCommand<SystemTickGetCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var duration = await controlService.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new { tickDuration = duration };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (duration is null) {
            AnsiConsole.MarkupLine("[yellow]Tick duration is not configured.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"Current minimal tick duration: [green]{duration} ms[/]");
        return 0;
    }
}
