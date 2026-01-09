namespace ScreepsDotNet.Backend.Cli.Commands.Auth;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class AuthResolveCommand(
    ITokenService tokenService,
    ICommandOutputFormatter outputFormatter,
    ILogger<AuthResolveCommand>? logger = null,
    IHostApplicationLifetime? lifetime = null)
    : CommandHandler<AuthResolveCommand.Settings>(logger, lifetime)
{
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
            outputFormatter.WriteKeyValueTable([("Token", settings.Token!), ("Status", "not found / expired")]);
            return 1;
        }

        if (settings.OutputJson) {
            outputFormatter.WriteJson(new { userId });
            return 0;
        }

        outputFormatter.WriteKeyValueTable([("Token", settings.Token!), ("User Id", userId)], "Token owner");
        return 0;
    }
}
