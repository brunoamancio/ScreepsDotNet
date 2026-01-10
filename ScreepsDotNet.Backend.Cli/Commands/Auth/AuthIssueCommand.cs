namespace ScreepsDotNet.Backend.Cli.Commands.Auth;

using global::System.ComponentModel;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class AuthIssueCommand(
    ITokenService tokenService,
    ILogger<AuthIssueCommand>? logger = null,
    IHostApplicationLifetime? lifetime = null,
    ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<AuthIssueCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--user-id <ID>")]
        [Description("User id to mint a token for.")]
        public string? UserId { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON payload.")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
            => string.IsNullOrWhiteSpace(UserId)
                   ? ValidationResult.Error("Specify --user-id.")
                   : ValidationResult.Success();
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var token = await tokenService.IssueTokenAsync(settings.UserId!, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { token });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([("Token", token)], "Issued token");
        return 0;
    }
}
