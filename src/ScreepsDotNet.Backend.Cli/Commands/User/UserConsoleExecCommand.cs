namespace ScreepsDotNet.Backend.Cli.Commands.User;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class UserConsoleExecCommand(IUserConsoleRepository consoleRepository, ILogger<UserConsoleExecCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<UserConsoleExecCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--user-id <ID>")]
        public string? UserId { get; init; }

        [CommandOption("--expression <JS>")]
        public string? Expression { get; init; }

        [CommandOption("--hidden")]
        public bool Hidden { get; init; }


        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (string.IsNullOrWhiteSpace(UserId))
                return ValidationResult.Error("Specify --user-id.");
            if (string.IsNullOrWhiteSpace(Expression))
                return ValidationResult.Error("Specify --expression.");
            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await consoleRepository.EnqueueExpressionAsync(settings.UserId!, settings.Expression!, settings.Hidden, cancellationToken)
                                .ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new
            {
                success = true,
                settings.UserId,
                settings.Hidden
            });
        }
        else {
            OutputFormatter.WriteKeyValueTable([
                                                   ("User", settings.UserId!),
                                                   ("Hidden", settings.Hidden ? "yes" : "no")
                                               ],
                                               "Console expression queued");
        }

        Logger.LogInformation("Queued console expression for {UserId}.", settings.UserId);
        return 0;
    }
}
