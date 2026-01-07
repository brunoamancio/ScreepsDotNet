namespace ScreepsDotNet.Backend.Cli.Application;

using Microsoft.Extensions.Logging;

internal interface ICliApplication
{
    Task<int> RunAsync(string[] args, CancellationToken cancellationToken);
}

internal sealed class CliApplication(ILogger<CliApplication> logger) : ICliApplication
{
    public Task<int> RunAsync(string[] args, CancellationToken cancellationToken)
    {
        if (args.Any(static arg => string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase))) {
            var version = typeof(CliApplication).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            logger.LogInformation("Screeps CLI version {Version}", version);
            return Task.FromResult(0);
        }

        logger.LogInformation("Screeps CLI host initialized. Command handling will be added in the next phase.");
        logger.LogInformation("Hint: run with '--version' to display the assembly version.");
        return Task.FromResult(0);
    }
}
