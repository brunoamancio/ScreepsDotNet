using System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemTickGetCommand(ISystemControlService controlService, ILogger<SystemTickGetCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<SystemTickGetCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var duration = await controlService.GetTickDurationAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new { tickDuration = duration };
            OutputFormatter.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (duration is null) {
            OutputFormatter.WriteMarkupLine("[yellow]Tick duration is not configured.[/]");
            return 0;
        }

        OutputFormatter.WriteMarkupLine($"Current minimal tick duration: [green]{duration} ms[/]");
        return 0;
    }
}
