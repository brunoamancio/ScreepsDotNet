namespace ScreepsDotNet.Backend.Cli.Commands.Bot;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BotReloadCommand(IBotControlService botControlService) : AsyncCommand<BotReloadCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--bot <NAME>")]
        [Description("Bot AI definition name.")]
        public string BotName { get; init; } = string.Empty;

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(BotName)
                ? ValidationResult.Error("Bot AI name is required.")
                : ValidationResult.Success();
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var count = await botControlService.ReloadAsync(settings.BotName, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                settings.BotName,
                UsersReloaded = count
            };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (count == 0) {
            AnsiConsole.MarkupLine("[yellow]No users were using this bot AI.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine($"[green]{count}[/] user(s) reloaded for bot [cyan]{settings.BotName}[/].");
        return 0;
    }
}
