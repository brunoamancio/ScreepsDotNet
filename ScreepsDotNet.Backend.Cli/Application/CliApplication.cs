namespace ScreepsDotNet.Backend.Cli.Application;

using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Cli.Commands;
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
        });

        return app.RunAsync(args, cancellationToken);
    }
}
