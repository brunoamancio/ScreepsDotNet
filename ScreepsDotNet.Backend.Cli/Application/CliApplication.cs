namespace ScreepsDotNet.Backend.Cli.Application;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Cli.Commands;
using ScreepsDotNet.Backend.Cli.Commands.Storage;
using ScreepsDotNet.Backend.Cli.Commands.User;
using ScreepsDotNet.Backend.Cli.Commands.Version;
using ScreepsDotNet.Backend.Cli.Commands.World;
using ScreepsDotNet.Backend.Cli.Infrastructure;
using Spectre.Console.Cli;

internal interface ICliApplication
{
    Task<int> RunAsync(string[] args, CancellationToken cancellationToken);
}

internal sealed class CliApplication(IServiceProvider serviceProvider, ILogger<CliApplication> logger) : ICliApplication
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        logger.LogDebug("Initializing Spectre command host.");

        var registrar = new CliTypeRegistrar(serviceProvider);
        var app = new CommandApp<RootCommand>(registrar);
        app.Configure(config =>
        {
            config.SetApplicationName("screeps-cli");
            config.PropagateExceptions();
            config.ValidateExamples();
            config.AddCommand<VersionCommand>("version").WithDescription("Display server version information.");

            config.AddBranch("storage", branch =>
            {
                branch.SetDescription("Storage utilities.");
                branch.AddCommand<StorageStatusCommand>("status").WithDescription("Check Mongo/Redis health.");
                branch.AddCommand<StorageReseedCommand>("reseed").WithDescription("Reseed Mongo/Redis with default data (coming soon).");
            });

            config.AddBranch("user", branch =>
            {
                branch.SetDescription("User operations.");
                branch.AddCommand<UserShowCommand>("show").WithDescription("Display a user's profile.");
            });

            config.AddBranch("world", branch =>
            {
                branch.SetDescription("World utilities.");
                branch.AddCommand<WorldDumpCommand>("dump").WithDescription("Dump room terrain data.");
            });
        });

        return app.RunAsync(args, cancellationToken);
    }
}
