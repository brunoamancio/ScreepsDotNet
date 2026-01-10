namespace ScreepsDotNet.Backend.Cli.Infrastructure;

using System;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ScreepsDotNet.Backend.Cli.Formatting;
using Spectre.Console.Cli;

internal abstract class CommandHandler<TSettings>(ILogger? logger, IHostApplicationLifetime? lifetime, ICommandOutputFormatter? outputFormatter = null)
    : AsyncCommand<TSettings>
    where TSettings : CommandSettings
{
    protected ILogger Logger { get; } = logger ?? NullLogger.Instance;
    protected ICommandOutputFormatter OutputFormatter { get; } = outputFormatter ?? new CommandOutputFormatter();
    private IHostApplicationLifetime Lifetime { get; } = lifetime ?? NullHostApplicationLifetime.Instance;

    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken)
    {
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, Lifetime.ApplicationStopping);
        var token = linkedCts.Token;
        var stopwatch = Stopwatch.StartNew();
        var commandName = context?.Name ?? GetType().Name;
        Logger.LogInformation("Command {Command} starting.", commandName);

        var previousFormat = OutputFormatter.PreferredFormat;
        if (settings is IFormattableCommandSettings { PreferredOutputFormat: { } format })
            OutputFormatter.SetPreferredFormat(format);

        if (settings is IConfirmationSettings confirmationSettings) {
            if (!string.Equals(confirmationSettings.ConfirmationValue,
                               confirmationSettings.RequiredConfirmationToken,
                               StringComparison.OrdinalIgnoreCase)) {
                stopwatch.Stop();
                Logger.LogError("Command {Command} requires confirmation token '{Token}'.",
                                commandName,
                                confirmationSettings.RequiredConfirmationToken);
                OutputFormatter.WriteLine(confirmationSettings.ConfirmationHelpText);
                if (OutputFormatter.PreferredFormat != previousFormat)
                    OutputFormatter.SetPreferredFormat(previousFormat);
                return 1;
            }
        }

        try {
            var exitCode = await ExecuteCommandAsync(context!, settings, token).ConfigureAwait(false);
            stopwatch.Stop();
            Logger.LogInformation("Command {Command} finished in {Elapsed}", commandName, stopwatch.Elapsed);
            return exitCode;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) {
            stopwatch.Stop();
            Logger.LogWarning("Command {Command} cancelled after {Elapsed}", commandName, stopwatch.Elapsed);
            return -2;
        }
        catch (Exception ex) {
            stopwatch.Stop();
            Logger.LogError(ex, "Command {Command} failed after {Elapsed}", commandName, stopwatch.Elapsed);
            return -1;
        }
        finally {
            if (OutputFormatter.PreferredFormat != previousFormat)
                OutputFormatter.SetPreferredFormat(previousFormat);
        }
    }

    protected abstract Task<int> ExecuteCommandAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken);
}
