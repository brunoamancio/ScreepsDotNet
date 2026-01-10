namespace ScreepsDotNet.Backend.Cli.Commands.World;

using global::System;
using global::System.Collections.Generic;
using global::System.ComponentModel;
using global::System.Globalization;
using global::System.Linq;
using global::System.Text.RegularExpressions;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Cli.Formatting;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed partial class WorldStatsCommand(
    IWorldStatsRepository statsRepository,
    ILogger<WorldStatsCommand>? logger = null,
    IHostApplicationLifetime? lifetime = null,
    ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<WorldStatsCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room identifier (e.g., W1N1). Repeat for multiple rooms.")]
        public string[] Rooms { get; init; } = [];

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
            OutputFormatter.WriteJson(result);
            return 0;
        }

        RenderTable(result);
        return 0;
    }

    private void RenderTable(MapStatsResult result)
    {
        OutputFormatter.WriteKeyValueTable([("Game time", result.GameTime.ToString(CultureInfo.InvariantCulture))]);

        if (result.Stats.Count == 0) {
            OutputFormatter.WriteKeyValueTable([("Status", "No stats returned for requested rooms")]);
            return;
        }

        var rows = result.Stats.Values
                              .OrderBy(r => r.RoomName, StringComparer.OrdinalIgnoreCase)
                              .Select(room => (IReadOnlyList<string>)[
                                  room.RoomName,
                                  room.Status ?? "unknown",
                                  ResolveOwner(room.Ownership, result.Users),
                                  room.Ownership?.Level.ToString(CultureInfo.InvariantCulture) ?? "-",
                                  room.IsSafeMode ? "yes" : "no",
                                  room.PrimaryMineral?.Type ?? "-"
                              ]);

        OutputFormatter.WriteTabularData("Room stats",
                                         ["Room", "Status", "Owner", "Level", "Safe Mode", "Primary Mineral"],
                                         rows);
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
