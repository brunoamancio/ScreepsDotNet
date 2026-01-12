namespace ScreepsDotNet.Backend.Cli.Commands.Auth;

using global::System.Collections.Generic;
using global::System.ComponentModel;
using global::System.Globalization;
using global::System.Linq;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

internal sealed class AuthTokenListCommand(
    ITokenService tokenService,
    ILogger<AuthTokenListCommand>? logger = null,
    IHostApplicationLifetime? lifetime = null,
    ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<AuthTokenListCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--user-id <ID>")]
        [Description("Filter tokens to a specific user id.")]
        public string? UserId { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON payload.")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var tokens = await tokenService.ListTokensAsync(settings.UserId, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = tokens.Select(token => new
            {
                token.Token,
                token.UserId,
                ExpiresInSeconds = token.TimeToLive?.TotalSeconds
            });
            OutputFormatter.WriteJson(payload);
            return 0;
        }

        if (tokens.Count == 0) {
            OutputFormatter.WriteLine(settings.UserId is null
                                          ? "No active tokens found."
                                          : $"No active tokens found for user '{settings.UserId}'.");
            return 0;
        }

        var rows = tokens.Select(token => (IReadOnlyList<string>)[
            token.Token,
            token.UserId,
            token.TimeToLive?.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture) ?? "unknown"
        ]);
        OutputFormatter.WriteTabularData("Active tokens", ["Token", "User", "TTL (hh:mm:ss)"], rows);
        return 0;
    }
}
