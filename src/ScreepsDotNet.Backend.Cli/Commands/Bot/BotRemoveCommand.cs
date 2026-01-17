namespace ScreepsDotNet.Backend.Cli.Commands.Bot;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BotRemoveCommand(IBotControlService botControlService, ILogger<BotRemoveCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<BotRemoveCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : FormattableCommandSettings
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

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var removed = await botControlService.RemoveAsync(settings.Username, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                settings.Username,
                Removed = removed
            };
            OutputFormatter.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            var jsonExitCode = removed ? 0 : 1;
            return jsonExitCode;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Username", settings.Username),
                                               ("Removed", removed ? "yes" : "no")
                                           ],
                                           "Bot removal");
        var exitCode = removed ? 0 : 1;
        return exitCode;
    }
}
