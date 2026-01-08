using ScreepsDotNet.Backend.Cli.Commands.Bot;
using ScreepsDotNet.Backend.Cli.Commands.Map;
using ScreepsDotNet.Backend.Cli.Commands.Stronghold;
using ScreepsDotNet.Backend.Cli.Commands.System;

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
                branch.AddCommand<StorageReseedCommand>("reseed").WithDescription("Reseed Mongo/Redis with default data.");
            });

            config.AddBranch("user", branch =>
            {
                branch.SetDescription("User operations.");
                branch.AddCommand<UserShowCommand>("show").WithDescription("Display a user's profile.");
                branch.AddCommand<UserConsoleExecCommand>("console").WithDescription("Queue a console expression for a user.");
                branch.AddBranch("memory", memory =>
                {
                    memory.AddCommand<UserMemoryGetCommand>("get").WithDescription("Inspect user memory.");
                    memory.AddCommand<UserMemorySetCommand>("set").WithDescription("Update user memory or segments.");
                });
            });

            config.AddBranch("world", branch =>
            {
                branch.SetDescription("World utilities.");
                branch.AddCommand<WorldDumpCommand>("dump").WithDescription("Dump room terrain data.");
            });

            config.AddBranch("bots", branch =>
            {
                branch.SetDescription("NPC bot management.");
                branch.AddCommand<BotListCommand>("list").WithDescription("List available bot AI bundles.");
                branch.AddCommand<BotSpawnCommand>("spawn").WithDescription("Spawn a bot AI into a room.");
                branch.AddCommand<BotReloadCommand>("reload").WithDescription("Reload bot AI scripts for all users.");
                branch.AddCommand<BotRemoveCommand>("remove").WithDescription("Remove a bot-controlled user.");
            });

            config.AddBranch("system", branch =>
            {
                branch.SetDescription("Runtime/system controls.");
                branch.AddCommand<SystemStatusCommand>("status").WithDescription("Show pause/tick status.");
                branch.AddCommand<SystemPauseCommand>("pause").WithDescription("Pause the simulation loop.");
                branch.AddCommand<SystemResumeCommand>("resume").WithDescription("Resume the simulation loop.");
                branch.AddCommand<SystemMessageCommand>("message").WithDescription("Broadcast a server message.");
                branch.AddCommand<SystemResetCommand>("reset").WithDescription("Reset world data (reseeds Mongo).");
                branch.AddBranch("tick", tick =>
                {
                    tick.SetDescription("Tick duration utilities.");
                    tick.AddCommand<SystemTickGetCommand>("get").WithDescription("Show the current tick duration.");
                    tick.AddCommand<SystemTickSetCommand>("set").WithDescription("Update the minimal tick duration.");
                });
            });

            config.AddBranch("map", branch =>
            {
                branch.SetDescription("Map editing utilities.");
                branch.AddCommand<MapGenerateCommand>("generate").WithDescription("Procedurally generate a room.");
                branch.AddCommand<MapOpenCommand>("open").WithDescription("Open (enable) a room.");
                branch.AddCommand<MapCloseCommand>("close").WithDescription("Close (disable) a room.");
                branch.AddCommand<MapRemoveCommand>("remove").WithDescription("Remove a room entry.");
                branch.AddBranch("assets", assets =>
                {
                    assets.SetDescription("Map asset helpers.");
                    assets.AddCommand<MapAssetsUpdateCommand>("update").WithDescription("Regenerate map assets (stubbed until renderer lands).");
                });
                branch.AddBranch("terrain", terrain =>
                {
                    terrain.SetDescription("Terrain cache helpers.");
                    terrain.AddCommand<MapTerrainRefreshCommand>("refresh").WithDescription("Refresh cached terrain metadata.");
                });
            });

            config.AddBranch("strongholds", branch =>
            {
                branch.SetDescription("NPC stronghold controls.");
                branch.AddCommand<StrongholdTemplatesCommand>("templates").WithDescription("List stronghold templates.");
                branch.AddCommand<StrongholdSpawnCommand>("spawn").WithDescription("Create a new NPC stronghold.");
                branch.AddCommand<StrongholdExpandCommand>("expand").WithDescription("Force a stronghold expansion.");
            });
        });

        return app.RunAsync(args, cancellationToken);
    }
}
