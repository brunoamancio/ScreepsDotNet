using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemMessageCommand(ISystemControlService controlService, ILogger<SystemMessageCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<SystemMessageCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandArgument(0, "<MESSAGE>")]
        public string Message { get; init; } = string.Empty;

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(Message))
                return ValidationResult.Error("Message text must be provided.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await controlService.PublishServerMessageAsync(settings.Message, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(new { message = settings.Message, dispatched = true });
            return 0;
        }

        OutputFormatter.WriteKeyValueTable([
                                               ("Message", settings.Message),
                                               ("Dispatched", "yes")
                                           ],
                                           "System message");
        return 0;
    }
}
