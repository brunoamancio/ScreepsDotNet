namespace ScreepsDotNet.Backend.Cli.Commands.Storage;

using global::System.Text.Json;
using Microsoft.Extensions.Logging;
using ScreepsDotNet.Backend.Core.Storage;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StorageStatusCommand(IStorageAdapter storageAdapter, ILogger<StorageStatusCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<StorageStatusCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var status = await storageAdapter.GetStatusAsync(cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                status.IsConnected,
                status.LastSynchronizationUtc,
                status.Details
            };
            OutputFormatter.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return status.IsConnected ? 0 : 1;
        }

        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Connected", status.IsConnected.ToString());
        table.AddRow("Last Sync (UTC)", status.LastSynchronizationUtc?.ToString("u") ?? "unknown");
        table.AddRow("Details", status.Details ?? "none");
        OutputFormatter.WriteTable(table);

        if (!status.IsConnected)
            Logger.LogWarning("Storage status reported disconnected: {Details}", status.Details);

        return status.IsConnected ? 0 : 1;
    }
}
