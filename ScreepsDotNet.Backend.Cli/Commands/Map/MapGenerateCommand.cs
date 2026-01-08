namespace ScreepsDotNet.Backend.Cli.Commands.Map;

using global::System.ComponentModel;
using global::System.Globalization;
using global::System.Text.Json;
using ScreepsDotNet.Backend.Core.Models.Map;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

internal sealed class MapGenerateCommand(IMapControlService mapControlService) : AsyncCommand<MapGenerateCommand.Settings>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public sealed class Settings : CommandSettings
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

        [CommandOption("--json")]
        [Description("Output JSON summary.")]
        public bool OutputJson { get; init; }

        public override ValidationResult Validate()
        {
            if (string.IsNullOrWhiteSpace(RoomName))
                return ValidationResult.Error("Room name is required.");

            if (SourceCount is < 1 or > 5)
                return ValidationResult.Error("Source count must be between 1 and 5.");

            return ValidationResult.Success();
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var options = new MapRoomGenerationOptions(settings.RoomName.Trim(),
                                                   settings.Terrain,
                                                   settings.SourceCount,
                                                   IncludeController: !settings.NoController,
                                                   settings.KeeperLairs,
                                                   settings.MineralType,
                                                   settings.Overwrite,
                                                   settings.Seed);

        var result = await mapControlService.GenerateRoomAsync(options, cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            AnsiConsole.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        var table = new Table().AddColumn("Property").AddColumn("Value");
        table.AddRow("Room", result.RoomName);
        table.AddRow("Objects", result.ObjectCount.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Sources", result.SourceCount.ToString(CultureInfo.InvariantCulture));
        table.AddRow("Controller", result.ControllerCreated ? "yes" : "no");
        table.AddRow("Keeper Lairs", result.KeeperLairsCreated ? "yes" : "no");
        table.AddRow("Mineral", result.MineralType ?? "random");
        AnsiConsole.Write(table);

        return 0;
    }
}
