namespace ScreepsDotNet.Backend.Cli.Commands.Stronghold;

using global::System;
using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class StrongholdExpandCommand(IStrongholdControlService strongholdControlService, ILogger<StrongholdExpandCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<StrongholdExpandCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room containing the stronghold core to expand.")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard (e.g., shard2).")]
        public string? Shard { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            return RoomReferenceParser.TryParse(RoomName, Shard, out _)
                ? ValidationResult.Success()
                : ValidationResult.Error("Room must match W##N## (optionally shard/W##N##).");
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null)
            throw new InvalidOperationException("Room validation failed.");

        var expanded = await strongholdControlService.ExpandAsync(reference.RoomName, reference.ShardName, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                Room = reference.RoomName,
                Shard = reference.ShardName,
                Expanded = expanded
            };
            OutputFormatter.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return expanded ? 0 : 1;
        }

        var displayRoom = string.IsNullOrWhiteSpace(reference.ShardName) ? reference.RoomName : $"{reference.ShardName}/{reference.RoomName}";
        OutputFormatter.WriteKeyValueTable([
                                               ("Room", displayRoom),
                                               ("Expanded", expanded ? "yes" : "no")
                                           ],
                                           "Stronghold expand");
        return expanded ? 0 : 1;
    }
}
