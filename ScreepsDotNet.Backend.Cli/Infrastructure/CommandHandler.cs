namespace ScreepsDotNet.Backend.Cli.Infrastructure;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console.Cli;

internal abstract class CommandHandler<TSettings>(ILogger? logger, IHostApplicationLifetime? lifetime)
    : AsyncCommand<TSettings>
    where TSettings : CommandSettings
{
    protected ILogger Logger { get; } = logger ?? NullLogger.Instance;
    private IHostApplicationLifetime Lifetime { get; } = lifetime ?? NullHostApplicationLifetime.Instance;

    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Lifetime.ApplicationStopping);
        var token = linkedCts.Token;
        var stopwatch = Stopwatch.StartNew();
        Logger.LogInformation("Command {Command} starting.", GetType().Name);

        try {
            var exitCode = await ExecuteCommandAsync(context, settings, token).ConfigureAwait(false);
            stopwatch.Stop();
            Logger.LogInformation("Command {Command} finished in {Elapsed}", GetType().Name, stopwatch.Elapsed);
            return exitCode;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            stopwatch.Stop();
            Logger.LogWarning("Command {Command} cancelled after {Elapsed}", GetType().Name, stopwatch.Elapsed);
            return -2;
        }
        catch (Exception ex) {
            stopwatch.Stop();
            Logger.LogError(ex, "Command {Command} failed after {Elapsed}", GetType().Name, stopwatch.Elapsed);
            return -1;
        }
    }

    protected abstract Task<int> ExecuteCommandAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken);
}
