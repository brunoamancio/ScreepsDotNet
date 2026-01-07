namespace ScreepsDotNet.Backend.Cli.Commands.User;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class UserConsoleExecCommand(IUserConsoleRepository consoleRepository, ILogger<UserConsoleExecCommand> logger)
    : AsyncCommand<UserConsoleExecCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--user-id <ID>")]
        public string? UserId { get; init; }

        [CommandOption("--expression <JS>")]
        public string? Expression { get; init; }

        [CommandOption("--hidden")]
        public bool Hidden { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(UserId))
                return ValidationResult.Error("Specify --user-id.");
            if (string.IsNullOrWhiteSpace(Expression))
                return ValidationResult.Error("Specify --expression.");
            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await consoleRepository.EnqueueExpressionAsync(settings.UserId!, settings.Expression!, settings.Hidden, cancellationToken)
                                .ConfigureAwait(false);
        if (settings.UserId == null) return 0;

        AnsiConsole.MarkupLine("[green]Queued expression for user '{0}'.[/]", settings.UserId);
        logger.LogInformation("Queued console expression for {UserId}.", settings.UserId);
        return 0;
    }
}
