namespace ScreepsDotNet.Backend.Cli.Commands.Auth;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class AuthIssueCommand(ITokenService tokenService, ILogger<AuthIssueCommand>? logger = null, IHostApplicationLifetime? lifetime = null)
    : CommandHandler<AuthIssueCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
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
            AnsiConsole.WriteLine(JsonSerializer.Serialize(new { token }, JsonOptions));
            return 0;
        }

        AnsiConsole.MarkupLine("[green]Issued token:[/] {0}", token);
        return 0;
    }
}
