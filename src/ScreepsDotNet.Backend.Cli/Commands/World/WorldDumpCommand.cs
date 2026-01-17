namespace ScreepsDotNet.Backend.Cli.Commands.World;

using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Common.Utilities;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class WorldDumpCommand(IRoomTerrainRepository terrainRepository, ILogger<WorldDumpCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<WorldDumpCommand.Settings>(logger, lifetime, outputFormatter)
{
    private static readonly string[] EncodedHeaders = ["Room", "Type", "Terrain"];
    private static readonly string[] DecodedHeaders = ["Room", "Type", "Tiles"];

    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--room <NAME>")]
        public string[] Rooms { get; init; } = [];

        [CommandOption("--decoded")]
        public bool DecodeTiles { get; init; }

        [CommandOption("--shard <NAME>")]
        public string? Shard { get; init; }

        [CommandOption("--json")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (Rooms.Length == 0)
                return ValidationResult.Error("Specify at least one --room.");

            foreach (var room in Rooms) {
                if (!RoomReferenceParser.TryParse(room, Shard, out _))
                    return ValidationResult.Error($"Invalid room: {room}. Use W##N## or shard/W##N##.");
            }

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

        var entries = await terrainRepository.GetTerrainEntriesAsync(references, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            if (settings.DecodeTiles) {
                var decoded = entries.Select(e => new
                {
                    e.RoomName,
                    e.ShardName,
                    e.Type,
                    Tiles = DecodeTerrain(e.Terrain)
                });
                OutputFormatter.WriteJson(decoded);
                return 0;
            }

            OutputFormatter.WriteJson(entries);
            return 0;
        }

        var rows = BuildRows(entries, settings.DecodeTiles);
        var headers = settings.DecodeTiles ? DecodedHeaders : EncodedHeaders;
        OutputFormatter.WriteTabularData("Room terrain", headers, rows);
        return 0;
    }

    private static IReadOnlyList<IReadOnlyList<string>> BuildRows(IReadOnlyList<RoomTerrainData> entries, bool decoded)
    {
        var rows = new List<IReadOnlyList<string>>(entries.Count);
        foreach (var entry in entries) {
            var displayRoom = FormatRoom(entry.RoomName, entry.ShardName);
            var type = entry.Type ?? "terrain";

            if (decoded) {
                var tiles = DecodeTerrain(entry.Terrain);
                rows.Add([displayRoom, type, $"{tiles.Count} tiles"]);
                continue;
            }

            rows.Add([displayRoom, type, entry.Terrain ?? "(empty)"]);
        }

        return rows;
    }

    private static IReadOnlyList<TerrainTile> DecodeTerrain(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
            return [];

        var tiles = new List<TerrainTile>(encoded.Length);
        for (var index = 0; index < encoded.Length && index < 2500; index++) {
            var value = TerrainEncoding.Decode(encoded[index]);
            var x = index % 50;
            var y = index / 50;
            var terrain = ResolveTerrain(value);
            tiles.Add(new TerrainTile(x, y, terrain));
        }

        return tiles;
    }

    private static string ResolveTerrain(int value)
    {
        if ((value & 1) != 0)
            return "wall";
        if ((value & 2) != 0)
            return "swamp";
        return "plain";
    }

    private static string FormatRoom(string roomName, string? shardName)
        => string.IsNullOrWhiteSpace(shardName) ? roomName : $"{shardName}/{roomName}";

    private sealed record TerrainTile(int X, int Y, string Terrain);
}
