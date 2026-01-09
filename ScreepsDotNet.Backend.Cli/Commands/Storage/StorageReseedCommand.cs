namespace ScreepsDotNet.Backend.Cli.Commands.Storage;

using global::System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StorageReseedCommand(ISeedDataService seedDataService, IOptions<MongoRedisStorageOptions> storageOptions, ILogger<StorageReseedCommand> logger)
    : AsyncCommand<StorageReseedCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--force")]
        public bool Force { get; init; }

        [CommandOption("--confirm <TEXT>")]
        [Description("Type RESET to confirm the destructive action.")]
        public string? Confirm { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!string.Equals(settings.Confirm, "RESET", StringComparison.OrdinalIgnoreCase)) {
            logger.LogError("Confirmation required. Re-run with --confirm RESET to proceed.");
            return 1;
        }

        var options = storageOptions.Value;
        if (string.IsNullOrWhiteSpace(options.MongoConnectionString) || string.IsNullOrWhiteSpace(options.MongoDatabase)) {
            logger.LogError("Mongo connection information is missing.");
            return 1;
        }

        if (!string.Equals(options.MongoDatabase, SeedDataDefaults.Database.Name, StringComparison.OrdinalIgnoreCase) && !settings.Force) {
            logger.LogWarning("Refusing to reseed database '{Database}' without --force.", options.MongoDatabase);
            return 1;
        }

        AnsiConsole.MarkupLine("[yellow]Reseeding Mongo database '{0}'...[/]", options.MongoDatabase);
        await seedDataService.ReseedAsync(options.MongoConnectionString, options.MongoDatabase, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]Reseed complete.[/]");
        return 0;
    }
}
