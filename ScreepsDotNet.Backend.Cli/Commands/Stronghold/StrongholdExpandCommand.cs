namespace ScreepsDotNet.Backend.Cli.Commands.Stronghold;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StrongholdExpandCommand(IStrongholdControlService strongholdControlService) : AsyncCommand<StrongholdExpandCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room containing the stronghold core to expand.")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(RoomName)
                ? ValidationResult.Error("Room name is required.")
                : ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var expanded = await strongholdControlService.ExpandAsync(settings.RoomName, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                settings.RoomName,
                Expanded = expanded
            };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return expanded ? 0 : 1;
        }

        if (!expanded) {
            AnsiConsole.MarkupLine("[yellow]No expandable stronghold core was found in that room.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Expansion queued for stronghold in {settings.RoomName}.[/]");
        return 0;
    }
}
