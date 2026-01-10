using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemPauseCommand(ISystemControlService controlService, ILogger<SystemPauseCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<CommandSettings>(logger, lifetime, outputFormatter)
{
    protected override async Task<int> ExecuteCommandAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        await controlService.PauseSimulationAsync(cancellationToken).ConfigureAwait(false);
        OutputFormatter.WriteMarkupLine("[yellow]Simulation loop paused.[/]");
        return 0;
    }
}
