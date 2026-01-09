namespace ScreepsDotNet.Backend.Cli.Commands.Auth;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class AuthResolveCommand(ITokenService tokenService, ILogger<AuthResolveCommand>? logger = null, IHostApplicationLifetime? lifetime = null)
    : CommandHandler<AuthResolveCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--token <VALUE>")]
        [Description("Auth token issued via /api/auth/me or screeps-cli auth issue.")]
        public string? Token { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON payload.")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(Token)
                   ? ValidationResult.Error("Specify --token.")
                   : ValidationResult.Success();
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var userId = await tokenService.ResolveUserIdAsync(settings.Token!, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(userId)) {
            Logger.LogWarning("Token could not be resolved.");
            AnsiConsole.MarkupLine("[red]Token not found or expired.[/]");
            return 1;
        }

        if (settings.OutputJson) {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(new { userId }, JsonOptions));
            return 0;
        }

        AnsiConsole.MarkupLine("[green]Token belongs to user:[/] {0}", userId);
        return 0;
    }
}
