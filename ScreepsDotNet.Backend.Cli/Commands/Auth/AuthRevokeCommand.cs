namespace ScreepsDotNet.Backend.Cli.Commands.Auth;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class AuthRevokeCommand(
    ITokenService tokenService,
    ILogger<AuthRevokeCommand>? logger = null,
    IHostApplicationLifetime? lifetime = null,
    ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<AuthRevokeCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--token <VALUE>")]
        [Description("Auth token value to revoke.")]
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
        var success = await tokenService.RevokeTokenAsync(settings.Token!, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson)
            OutputFormatter.WriteJson(new { revoked = success });
        else {
            if (success)
                OutputFormatter.WriteMarkupLine("[green]Revoked token {0}.[/]", settings.Token!);
            else
                OutputFormatter.WriteMarkupLine("[yellow]Token {0} was not found.[/]", settings.Token!);
        }

        return success ? 0 : 1;
    }
}
