namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System;
using global::System.ComponentModel;
using global::System.Globalization;
using ScreepsDotNet.Backend.Cli.Formatting;
using ScreepsDotNet.Backend.Core.Models.Map;
using ScreepsDotNet.Backend.Core.Parsing;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapGenerateCommand(IMapControlService mapControlService, ILogger<MapGenerateCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null) : CommandHandler<MapGenerateCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandOption("--room <NAME>")]
        [Description("Room name (e.g., W1N1).")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--sources <COUNT>")]
        [Description("Number of energy sources (1-3, default 2).")]
        [DefaultValue(2)]
        public int SourceCount { get; init; } = 2;

        [CommandOption("--terrain <PRESET>")]
        [Description("Terrain preset: plain, swampLow, swampHeavy, checker, mixed.")]
        [DefaultValue(MapTerrainPreset.Mixed)]
        public MapTerrainPreset Terrain { get; init; } = MapTerrainPreset.Mixed;

        [CommandOption("--no-controller")]
        [Description("Skip controller creation.")]
        public bool NoController { get; init; }

        [CommandOption("--keeper-lairs")]
        [Description("Place keeper lairs in the room.")]
        public bool KeeperLairs { get; init; }

        [CommandOption("--mineral <TYPE>")]
        [Description("Mineral type to place (default random).")]
        public string? MineralType { get; init; }

        [CommandOption("--overwrite")]
        [Description("Overwrite existing room data if present.")]
        public bool Overwrite { get; init; }

        [CommandOption("--seed <INT>")]
        [Description("Optional deterministic seed for generation.")]
        public int? Seed { get; init; }

        [CommandOption("--shard <NAME>")]
        [Description("Optional shard override (e.g., shard3).")]
        public string? Shard { get; init; }

        [CommandOption("--json")]
        [Description("Output JSON summary.")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            var baseResult = base.Validate();
            if (!baseResult.Successful)
                return baseResult;

            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (SourceCount is < 1 or > 5)
                return ValidationResult.Error("Source count must be between 1 and 5.");

            if (!RoomReferenceParser.TryParse(RoomName, Shard, out _))
                return ValidationResult.Error("Room name must match W##N## (optionally shard/W##N##).");

            return ValidationResult.Success();
        }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        if (!RoomReferenceParser.TryParse(settings.RoomName, settings.Shard, out var reference) || reference is null)
            throw new InvalidOperationException("Room name validation failed.");

        var options = new MapRoomGenerationOptions(reference.RoomName,
                                                   reference.ShardName,
                                                   settings.Terrain,
                                                   settings.SourceCount,
                                                   IncludeController: !settings.NoController,
                                                   settings.KeeperLairs,
                                                   settings.MineralType,
                                                   settings.Overwrite,
                                                   settings.Seed);

        var result = await mapControlService.GenerateRoomAsync(options, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(result);
            return 0;
        }

        var displayName = string.IsNullOrWhiteSpace(reference.ShardName) ? result.RoomName : $"{reference.ShardName}/{result.RoomName}";
        OutputFormatter.WriteKeyValueTable([
                                               ("Room", displayName),
                                               ("Objects", result.ObjectCount.ToString(CultureInfo.InvariantCulture)),
                                               ("Sources", result.SourceCount.ToString(CultureInfo.InvariantCulture)),
                                               ("Controller", result.ControllerCreated ? "yes" : "no"),
                                               ("Keeper Lairs", result.KeeperLairsCreated ? "yes" : "no"),
                                               ("Mineral", result.MineralType ?? "random")
                                           ],
                                           "Generated room");

        return 0;
    }
}
