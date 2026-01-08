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
            config.AddCommand<VersionCommand>("version")
                  .WithDescription("Display server version information.")
                  .WithExample("version", "--json");

            config.AddBranch("storage", branch =>
            {
                branch.SetDescription("Storage utilities.");
                branch.AddCommand<StorageStatusCommand>("status")
                      .WithDescription("Check Mongo/Redis health.")
                      .WithExample("storage", "status", "--json");
                branch.AddCommand<StorageReseedCommand>("reseed")
                      .WithDescription("Reseed Mongo/Redis with default data.")
                      .WithExample("storage", "reseed", "--force");
            });

            config.AddBranch("user", branch =>
            {
                branch.SetDescription("User operations.");
                branch.AddCommand<UserShowCommand>("show")
                      .WithDescription("Display a user's profile.")
                      .WithExample("user", "show", "--username", "test-user", "--json");
                branch.AddCommand<UserConsoleExecCommand>("console")
                      .WithDescription("Queue a console expression for a user.")
                      .WithExample("user", "console", "--user-id", "integration-user", "--expression", "console.log('hi');");
                branch.AddBranch("memory", memory =>
                {
                    memory.AddCommand<UserMemoryGetCommand>("get")
                          .WithDescription("Inspect user memory.")
                          .WithExample("user", "memory", "get", "--user-id", "integration-user", "--path", "stats");
                    memory.AddCommand<UserMemorySetCommand>("set")
                          .WithDescription("Update user memory or segments.")
                          .WithExample("user", "memory", "set", "--user-id", "integration-user", "--value", "{\"logLevel\":\"info\"}");
                });
            });

            config.AddBranch("world", branch =>
            {
                branch.SetDescription("World utilities.");
                branch.AddCommand<WorldDumpCommand>("dump")
                      .WithDescription("Dump room terrain data.")
                      .WithExample("world", "dump", "--room", "W1N1", "--decoded", "--json");
            });

            config.AddBranch("bots", branch =>
            {
                branch.SetDescription("NPC bot management.");
                branch.AddCommand<BotListCommand>("list")
                      .WithDescription("List available bot AI bundles.")
                      .WithExample("bots", "list", "--json");
                branch.AddCommand<BotSpawnCommand>("spawn")
                      .WithDescription("Spawn a bot AI into a room.")
                      .WithExample("bots", "spawn", "--bot", "alpha", "--room", "W1N1", "--cpu", "150");
                branch.AddCommand<BotReloadCommand>("reload")
                      .WithDescription("Reload bot AI scripts for all users.")
                      .WithExample("bots", "reload", "--bot", "alpha");
                branch.AddCommand<BotRemoveCommand>("remove")
                      .WithDescription("Remove a bot-controlled user.")
                      .WithExample("bots", "remove", "--username", "AlphaBot");
            });

            config.AddBranch("system", branch =>
            {
                branch.SetDescription("Runtime/system controls.");
                branch.AddCommand<SystemStatusCommand>("status")
                      .WithDescription("Show pause/tick status.")
                      .WithExample("system", "status", "--json");
                branch.AddCommand<SystemPauseCommand>("pause")
                      .WithDescription("Pause the simulation loop.")
                      .WithExample("system", "pause");
                branch.AddCommand<SystemResumeCommand>("resume")
                      .WithDescription("Resume the simulation loop.")
                      .WithExample("system", "resume");
                branch.AddCommand<SystemMessageCommand>("message")
                      .WithDescription("Broadcast a server message.")
                      .WithExample("system", "message", "Server restart in 5 minutes.");
                branch.AddCommand<SystemResetCommand>("reset")
                      .WithDescription("Reset world data (reseeds Mongo).")
                      .WithExample("system", "reset", "--force");
                branch.AddBranch("tick", tick =>
                {
                    tick.SetDescription("Tick duration utilities.");
                    tick.AddCommand<SystemTickGetCommand>("get")
                        .WithDescription("Show the current tick duration.")
                        .WithExample("system", "tick", "get", "--json");
                    tick.AddCommand<SystemTickSetCommand>("set")
                        .WithDescription("Update the minimal tick duration.")
                        .WithExample("system", "tick", "set", "--ms", "700");
                });
            });

            config.AddBranch("map", branch =>
            {
                branch.SetDescription("Map editing utilities.");
                branch.AddCommand<MapGenerateCommand>("generate")
                      .WithDescription("Procedurally generate a room.")
                      .WithExample("map", "generate", "--room", "W10N5", "--sources", "3", "--overwrite", "--json");
                branch.AddCommand<MapOpenCommand>("open")
                      .WithDescription("Open (enable) a room.")
                      .WithExample("map", "open", "--room", "W10N5");
                branch.AddCommand<MapCloseCommand>("close")
                      .WithDescription("Close (disable) a room.")
                      .WithExample("map", "close", "--room", "W10N5");
                branch.AddCommand<MapRemoveCommand>("remove")
                      .WithDescription("Remove a room entry.")
                      .WithExample("map", "remove", "--room", "W10N5", "--purge-objects");
                branch.AddBranch("assets", assets =>
                {
                    assets.SetDescription("Map asset helpers.");
                    assets.AddCommand<MapAssetsUpdateCommand>("update")
                          .WithDescription("Regenerate map assets (stubbed until renderer lands).")
                          .WithExample("map", "assets", "update", "--room", "W10N5", "--full");
                });
                branch.AddBranch("terrain", terrain =>
                {
                    terrain.SetDescription("Terrain cache helpers.");
                    terrain.AddCommand<MapTerrainRefreshCommand>("refresh")
                           .WithDescription("Refresh cached terrain metadata.")
                           .WithExample("map", "terrain", "refresh");
                });
            });

            config.AddBranch("strongholds", branch =>
            {
                branch.SetDescription("NPC stronghold controls.");
                branch.AddCommand<StrongholdTemplatesCommand>("templates")
                      .WithDescription("List stronghold templates.")
                      .WithExample("strongholds", "templates", "--json");
                branch.AddCommand<StrongholdSpawnCommand>("spawn")
                      .WithDescription("Create a new NPC stronghold.")
                      .WithExample("strongholds", "spawn", "--room", "W5N3", "--template", "bunker2");
                branch.AddCommand<StrongholdExpandCommand>("expand")
                      .WithDescription("Force a stronghold expansion.")
                      .WithExample("strongholds", "expand", "--room", "W5N3");
            });
        });

        return app.RunAsync(args, cancellationToken);
    }
}
