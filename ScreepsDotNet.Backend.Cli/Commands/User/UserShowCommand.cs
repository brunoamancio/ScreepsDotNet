namespace ScreepsDotNet.Backend.Cli.Commands.User;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class UserShowCommand(IUserRepository userRepository, ILogger<UserShowCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<UserShowCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly global::System.Text.Json.JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--user-id <ID>")]
        public string? UserId { get; init; }

        [CommandOption("--username <NAME>")]
        public string? Username { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(UserId) && string.IsNullOrWhiteSpace(Username))
                return ValidationResult.Error("Specify --user-id or --username.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var userId = settings.UserId;
        if (string.IsNullOrWhiteSpace(userId) && !string.IsNullOrWhiteSpace(settings.Username)) {
            var profile = await userRepository.FindPublicProfileAsync(settings.Username, null, cancellationToken).ConfigureAwait(false);
            userId = profile?.Id;
        }

        if (string.IsNullOrWhiteSpace(userId)) {
            Logger.LogError("Unable to resolve user identifier.");
            return 1;
        }

        var result = await userRepository.GetProfileAsync(userId, cancellationToken).ConfigureAwait(false);
        if (result is null) {
            Logger.LogWarning("User '{UserId}' not found.", userId);
            return 1;
        }

        if (settings.OutputJson) {
            var payload = global::System.Text.Json.JsonSerializer.Serialize(result, JsonOptions);
            OutputFormatter.WriteLine(payload);
            return 0;
        }

        var table = new Table().AddColumn("Field").AddColumn("Value");
        table.AddRow("User Id", result.Id);
        table.AddRow("Username", result.Username ?? "(unknown)");
        table.AddRow("Email", string.IsNullOrWhiteSpace(result.Email) ? "(hidden)" : result.Email);
        table.AddRow("CPU", result.Cpu.ToString("F0"));
        table.AddRow("Power", result.Power.ToString("F0"));
        table.AddRow("Money", result.Money.ToString("F0"));
        table.AddRow("Last Respawn", result.LastRespawnDate?.ToString("u") ?? "n/a");
        var steamVisibility = result.Steam?.SteamProfileLinkHidden is true ? "Hidden" : "Visible";
        table.AddRow("Steam Visible", steamVisibility);
        OutputFormatter.WriteTable(table);

        return 0;
    }
}
