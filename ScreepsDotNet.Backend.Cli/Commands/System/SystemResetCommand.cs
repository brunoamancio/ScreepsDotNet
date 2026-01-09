using System.ComponentModel;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.System;

internal sealed class SystemResetCommand(ISeedDataService seedDataService, IOptions<MongoRedisStorageOptions> storageOptions, ILogger<SystemResetCommand>? logger = null, IHostApplicationLifetime? lifetime = null)
    : CommandHandler<SystemResetCommand.Settings>(logger, lifetime)
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--force")]
        public bool Force { get; init; }

        [CommandOption("--confirm <TEXT>")]
        [Description("Type RESET to confirm the destructive action.")]
        public string? Confirm { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!string.Equals(settings.Confirm, "RESET", StringComparison.OrdinalIgnoreCase)) {
            Logger.LogError("Confirmation required. Re-run with --confirm RESET to proceed.");
            return 1;
        }

        var options = storageOptions.Value;
        if (string.IsNullOrWhiteSpace(options.MongoConnectionString) || string.IsNullOrWhiteSpace(options.MongoDatabase)) {
            Logger.LogError("Mongo connection information is missing.");
            return 1;
        }

        if (!string.Equals(options.MongoDatabase, SeedDataDefaults.Database.Name, StringComparison.OrdinalIgnoreCase) && !settings.Force) {
            Logger.LogWarning("Refusing to reset database '{Database}' without --force.", options.MongoDatabase);
            return 1;
        }

        AnsiConsole.MarkupLine("[red]Resetting world data in database '{0}'. This wipes all user/world state.[/]", options.MongoDatabase);
        await seedDataService.ReseedAsync(options.MongoConnectionString, options.MongoDatabase, cancellationToken).ConfigureAwait(false);
        AnsiConsole.MarkupLine("[green]Reset complete.[/]");
        return 0;
    }
}
