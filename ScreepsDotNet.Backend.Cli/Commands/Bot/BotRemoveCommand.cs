namespace ScreepsDotNet.Backend.Cli.Commands.Bot;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BotRemoveCommand(IBotControlService botControlService) : AsyncCommand<BotRemoveCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--username <NAME>")]
        [Description("Bot username to remove.")]
        public string Username { get; init; } = string.Empty;

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(Username)
                ? ValidationResult.Error("Username is required.")
                : ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var removed = await botControlService.RemoveAsync(settings.Username, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                settings.Username,
                Removed = removed
            };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return removed ? 0 : 1;
        }

        if (!removed) {
            AnsiConsole.MarkupLine("[yellow]User not found or not a bot account.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Bot user {settings.Username} removed.[/]");
        return 0;
    }
}
