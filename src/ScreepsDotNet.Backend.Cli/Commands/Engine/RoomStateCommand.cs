using ScreepsDotNet.Engine.Data.Rooms;
using Spectre.Console.Cli;

namespace ScreepsDotNet.Backend.Cli.Commands.Engine;

internal sealed class RoomStateCommand(IRoomStateProvider stateProvider, ILogger<RoomStateCommand>? logger = null, IHostApplicationLifetime? lifetime = null, ICommandOutputFormatter? outputFormatter = null)
    : CommandHandler<RoomStateCommand.Settings>(logger, lifetime, outputFormatter)
{
    public sealed class Settings : FormattableCommandSettings
    {
        [CommandArgument(0, "<roomName>")]
        public string RoomName { get; init; } = string.Empty;

        [CommandOption("--json")]
        public bool OutputJson { get; init; }
    }

    protected override async Task<int> ExecuteCommandAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var state = await stateProvider.GetRoomStateAsync(settings.RoomName, gameTime: 0, token: cancellationToken).ConfigureAwait(false);

        if (settings.OutputJson) {
            OutputFormatter.WriteJson(state);
            return 0;
        }

        var objectsByType = state.Objects.Values
            .GroupBy(o => o.Type ?? "unknown")
            .ToDictionary(g => g.Key, g => g.Count());

        var intentCount = state.Intents?.Users.Values
            .Sum(u => u.ObjectIntents.Values.Sum(intents => intents.Count) +
                      u.SpawnIntents.Count +
                      u.CreepIntents.Count) ?? 0;

        OutputFormatter.WriteKeyValueTable([
            ("Room", state.RoomName),
            ("Game time", state.GameTime.ToString()),
            ("Object count", state.Objects.Count.ToString()),
            ("Intent count", intentCount.ToString()),
            ("Creeps", objectsByType.GetValueOrDefault("creep", 0).ToString()),
            ("Spawns", objectsByType.GetValueOrDefault("spawn", 0).ToString()),
            ("Extensions", objectsByType.GetValueOrDefault("extension", 0).ToString()),
            ("Towers", objectsByType.GetValueOrDefault("tower", 0).ToString()),
            ("Sources", objectsByType.GetValueOrDefault("source", 0).ToString())
        ], $"Room State: {settings.RoomName}");

        return 0;
    }
}
