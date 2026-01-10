using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemTickSetCommand(ISystemControlService controlService, ILogger<SystemTickSetCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<SystemTickSetCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--ms <MILLISECONDS>")]
        public int? DurationMilliseconds { get; init; }

        public override ValidationResult Validate()
        {
            if (DurationMilliseconds is null or <= 0)
                return ValidationResult.Error("Specify --ms with a positive integer value.");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        await controlService.SetTickDurationAsync(settings.DurationMilliseconds!.Value, cancellationToken).ConfigureAwait(false);
        OutputFormatter.WriteMarkupLine($"[green]Tick duration set to {settings.DurationMilliseconds} ms.[/]");
        return 0;
    }
}
