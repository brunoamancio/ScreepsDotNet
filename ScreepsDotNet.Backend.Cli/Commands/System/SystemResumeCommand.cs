using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemResumeCommand(ISystemControlService controlService) : AsyncCommand<CommandSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        await controlService.ResumeSimulationAsync(cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]Simulation loop resumed.[/]");
        return 0;
    }
}
