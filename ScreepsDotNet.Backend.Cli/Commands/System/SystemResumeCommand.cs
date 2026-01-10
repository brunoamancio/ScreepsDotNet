using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemResumeCommand(ISystemControlService controlService, ILogger<SystemResumeCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<CommandSettings>(logger, lifetime, outputFormatter)
{
    protected override async Task<int> ExecuteCommandAsync(CommandContext context, CommandSettings settings, CancellationToken cancellationToken)
    {
        await controlService.ResumeSimulationAsync(cancellationToken).ConfigureAwait(false);
        OutputFormatter.WriteMarkupLine("[green]Simulation loop resumed.[/]");
        return 0;
    }
}
