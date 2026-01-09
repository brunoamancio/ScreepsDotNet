namespace ScreepsDotNet.Backend.Cli.Commands.World;

using global::System;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Models;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class WorldDumpCommand(IRoomTerrainRepository terrainRepository, ILogger<WorldDumpCommand>? logger = null, IHostApplicationLifetime? lifetime = null) : CommandHandler<WorldDumpCommand.Settings>(logger, lifetime)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public sealed class Settings : CommandSettings
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
                    e.Type,
                    Tiles = DecodeTerrain(e.Terrain)
                });
                AnsiConsole.WriteLine(JsonSerializer.Serialize(decoded, JsonOptions));
                return 0;
            }

            AnsiConsole.WriteLine(JsonSerializer.Serialize(entries, JsonOptions));
            return 0;
        }

        foreach (var entry in entries) {
            AnsiConsole.MarkupLine($"[bold]{entry.RoomName}[/] ({entry.Type ?? "terrain"})");
            if (settings.DecodeTiles) {
                var tiles = DecodeTerrain(entry.Terrain);
                AnsiConsole.MarkupLine($"Tiles: {tiles.Count}");
            }
            else
                AnsiConsole.MarkupLine(entry.Terrain ?? "(empty)");

            AnsiConsole.WriteLine();
        }

        return 0;
    }

    private static IReadOnlyList<object> DecodeTerrain(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
            return Array.Empty<object>();

        var tiles = new List<object>(encoded.Length);
        for (var index = 0; index < encoded.Length && index < 2500; index++) {
            var value = DecodeChar(encoded[index]);
            var x = index % 50;
            var y = index / 50;
            var terrain = ResolveTerrain(value);
            tiles.Add(new { x, y, terrain });
        }

        return tiles;
    }

    private static int DecodeChar(char value)
    {
        if (value is >= '0' and <= '9')
            return value - '0';
        if (value is >= 'a' and <= 'z')
            return 10 + (value - 'a');
        if (value is >= 'A' and <= 'Z')
            return 10 + (value - 'A');
        return 0;
    }

    private static string ResolveTerrain(int value)
    {
        if ((value & 1) != 0)
            return "wall";
        if ((value & 2) != 0)
            return "swamp";
        return "plain";
    }
}
