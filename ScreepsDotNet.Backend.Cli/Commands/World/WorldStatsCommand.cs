namespace ScreepsDotNet.Backend.Cli.Commands.World;

using global::System;
using global::System.Collections.Generic;
using global::System.ComponentModel;
using global::System.Globalization;
using global::System.Linq;
using global::System.Text.Json;
using global::System.Text.RegularExpressions;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed partial class WorldStatsCommand(IWorldStatsRepository statsRepository, ILogger<WorldStatsCommand>? logger = null, IHostApplicationLifetime? lifetime = null)
    : CommandHandler<WorldStatsCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room identifier (e.g., W1N1). Repeat for multiple rooms.")]
        public string[] Rooms { get; init; } = Array.Empty<string>();

        [CommandOption("--stat <NAME>")]
        [Description("Legacy stat bucket (e.g., owners1, power5).")]
        public string StatName { get; init; } = "owners1";

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard override (e.g., shard1).")]
        public string? Shard { get; init; }

        [CommandOption("--json")]
        [Description("Emit raw JSON instead of a table.")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (Rooms.Length == 0)
                return ValidationResult.Error("Specify at least one --room.");

            foreach (var room in Rooms) {
                if (!RoomReferenceParser.TryParse(room, Shard, out _))
                    return ValidationResult.Error($"Invalid room: {room}. Use W##N## or shard/W##N##.");
            }

            if (string.IsNullOrWhiteSpace(StatName))
                return ValidationResult.Error("Specify --stat.");

            if (!StatNameRegex().IsMatch(StatName.Trim()))
                return ValidationResult.Error("Stat name must end with digits (e.g., owners1).");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var references = new List<RoomReference>(settings.Rooms.Length);
        foreach (var room in settings.Rooms) {
            if (!RoomReferenceParser.TryParse(room, settings.Shard, out var reference) || reference is null)
                throw new InvalidOperationException("Room validation failed.");
            references.Add(reference);
        }

        var request = new MapStatsRequest(references, settings.StatName.Trim());
        var result = await statsRepository.GetMapStatsAsync(request, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        RenderTable(result);
        return 0;
    }

    private static void RenderTable(MapStatsResult result)
    {
        AnsiConsole.MarkupLine($"[grey]Game time:[/] [bold]{result.GameTime}[/]");

        if (result.Stats.Count == 0) {
            AnsiConsole.MarkupLine("[yellow]No stats returned for the requested rooms.[/]");
            return;
        }

        var table = new Table().AddColumns("Room", "Status", "Owner", "Level", "Safe Mode", "Primary Mineral");

        foreach (var room in result.Stats.Values.OrderBy(r => r.RoomName, StringComparer.OrdinalIgnoreCase)) {
            var ownerLabel = ResolveOwner(room.Ownership, result.Users);
            var level = room.Ownership?.Level.ToString(CultureInfo.InvariantCulture) ?? "-";
            var safeMode = room.IsSafeMode ? "yes" : "no";
            var mineral = room.PrimaryMineral?.Type ?? "-";
            table.AddRow(room.RoomName, room.Status ?? "unknown", ownerLabel, level, safeMode, mineral);
        }

        AnsiConsole.Write(table);
    }

    private static string ResolveOwner(RoomOwnershipInfo? ownership, IReadOnlyDictionary<string, MapStatsUser> users)
    {
        if (ownership is null || string.IsNullOrWhiteSpace(ownership.UserId))
            return "(unowned)";

        if (users.TryGetValue(ownership.UserId, out var user) && !string.IsNullOrWhiteSpace(user.Username))
            return $"{user.Username} ({user.Id})";

        return ownership.UserId;
    }

    [GeneratedRegex(@"^(.*?)(\d+)$", RegexOptions.Compiled)]
    private static partial Regex StatNameRegex();
}
