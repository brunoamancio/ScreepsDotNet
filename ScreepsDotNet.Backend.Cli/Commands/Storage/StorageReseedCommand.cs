namespace ScreepsDotNet.Backend.Cli.Commands.Storage;

using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StorageReseedCommand(ILogger<StorageReseedCommand> logger) : Command<StorageReseedCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--force")]
        public bool Force { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        logger.LogWarning("Storage reseed is not yet implemented in the CLI. Use 'docker compose down -v && docker compose up -d' to reseed locally.");
        AnsiConsole.MarkupLine("[yellow]Storage reseed is not yet implemented. Run 'docker compose down -v && docker compose up -d' to reseed containers.[/]");
        return 1;
    }
}
