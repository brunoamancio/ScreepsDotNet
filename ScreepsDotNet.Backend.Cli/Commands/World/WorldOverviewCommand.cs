namespace ScreepsDotNet.Backend.Cli.Commands.World;

using global::System.ComponentModel;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class WorldOverviewCommand(IRoomOverviewRepository overviewRepository, ILogger<WorldOverviewCommand>? logger = null, IHostApplicationLifetime? lifetime = null)
    : CommandHandler<WorldOverviewCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room identifier (e.g., W1N1).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard override (e.g., shard2).")]
        public string? Shard { get; init; }

        [CommandOption("--json")]
        [Description("Emit JSON payload.")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Specify --room.");

            if (!RoomReferenceParser.TryParse(RoomName, Shard, out _))
                return ValidationResult.Error("Room must match W##N## (optionally shard/W##N##).");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null)
            throw new InvalidOperationException("Room validation failed.");

        var overview = await overviewRepository.GetRoomOverviewAsync(reference, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            var payload = new
            {
                room = new { reference.RoomName, reference.ShardName },
                overview?.Owner
            };
            AnsiConsole.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
            return 0;
        }

        if (overview?.Owner is null) {
            AnsiConsole.MarkupLine($"[yellow]{reference.RoomName}[/] has no owner.");
            return 0;
        }

        var table = new Table().AddColumns("Room", "Owner", "User Id");
        table.AddRow(FormatRoom(reference), overview.Owner.Username, overview.Owner.Id);
        AnsiConsole.Write(table);
        return 0;
    }

    private static string FormatRoom(RoomReference reference)
        => string.IsNullOrWhiteSpace(reference.ShardName) ? reference.RoomName : $"{reference.ShardName}/{reference.RoomName}";
}
