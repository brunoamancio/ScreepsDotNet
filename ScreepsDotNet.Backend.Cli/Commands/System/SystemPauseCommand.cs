using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemPauseCommand(ISystemControlService controlService) : AsyncCommand<CommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        await controlService.PauseSimulationAsync(cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[yellow]Simulation loop paused.[/]");
        return 0;
    }
}
