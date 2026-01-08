using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemMessageCommand(ISystemControlService controlService) : AsyncCommand<SystemMessageCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<MESSAGE>")]
        public string Message { get; init; } = string.Empty;

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Message))
                return ValidationResult.Error("Message text must be provided.");

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await controlService.PublishServerMessageAsync(settings.Message, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]Server message dispatched.[/]");
        return 0;
    }
}
