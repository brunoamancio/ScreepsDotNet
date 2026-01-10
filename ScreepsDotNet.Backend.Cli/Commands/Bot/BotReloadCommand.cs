namespace ScreepsDotNet.Backend.Cli.Commands.Bot;

using global::System.ComponentModel;
using global::System.Globalization;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class BotReloadCommand(IBotControlService botControlService, ILogger<BotReloadCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<BotReloadCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : FormattableCommandSettings
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

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var count = await botControlService.ReloadAsync(settings.BotName, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                settings.BotName,
                UsersReloaded = count
            };
            OutputFormatter.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Bot", settings.BotName),
                                               ("Users Reloaded", count.ToString(CultureInfo.InvariantCulture))
                                           ],
                                           "Bot reload");
        return 0;
    }
}
