namespace ScreepsDotNet.Backend.Cli.Tests.Commands;

using ScreepsDotNet.Backend.Cli.Commands.Map;
using ScreepsDotNet.Backend.Core.Models.Map;
using ScreepsDotNet.Backend.Core.Services;
using Spectre.Console.Cli;

public sealed class MapCommandTests
{
    [Fact]
    public async Task MapGenerateCommand_ForwardsAllOptions()
    {
        var service = new FakeMapControlService { GenerationResult = new MapGenerationResult("W3N3", 2500, 42, false, 3, true, "Z") };

        var command = new MapGenerateCommand(service);
        var settings = new MapGenerateCommand.Settings
        {
            RoomName = " W3N3 ",
            Terrain = MapTerrainPreset.Plain,
            SourceCount = 3,
            NoController = true,
            KeeperLairs = true,
            MineralType = "Z",
            Overwrite = true,
            Seed = 123,
            OutputJson = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.NotNull(service.GeneratedOptions);
        var options = service.GeneratedOptions!;
        Assert.Equal("W3N3", options.RoomName);
        Assert.Equal(MapTerrainPreset.Plain, options.TerrainPreset);
        Assert.Equal(3, options.SourceCount);
        Assert.False(options.IncludeController);
        Assert.True(options.IncludeKeeperLairs);
        Assert.Equal("Z", options.MineralType);
        Assert.True(options.OverwriteExisting);
        Assert.Equal(123, options.Seed);
        Assert.NotNull(service.LastGenerationResult);
        Assert.Equal("W3N3", service.LastGenerationResult!.RoomName);
    }

    [Fact]
    public async Task MapRemoveCommand_PropagatesRoomAndPurgeFlag()
    {
        var service = new FakeMapControlService();
        var command = new MapRemoveCommand(service);
        var settings = new MapRemoveCommand.Settings
        {
            RoomName = "W8S2",
            PurgeObjects = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("W8S2", service.RemovedRoom);
        Assert.True(service.PurgedObjects ?? false);
    }

    [Fact]
    public async Task MapAssetsUpdateCommand_HonorsFullFlag()
    {
        var service = new FakeMapControlService();
        var command = new MapAssetsUpdateCommand(service);
        var settings = new MapAssetsUpdateCommand.Settings
        {
            RoomName = "W9N9",
            Full = true
        };

        var exitCode = await command.ExecuteAsync(null!, settings, CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Equal("W9N9", service.AssetRoomName);
        Assert.True(service.AssetFull ?? false);
    }

    [Fact]
    public async Task MapTerrainRefreshCommand_InvokesService()
    {
        var service = new FakeMapControlService();
        var command = new MapTerrainRefreshCommand(service);
        var exitCode = await command.ExecuteAsync(null!, new EmptySettings(), CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(service.TerrainRefreshed);
    }

    [Fact]
    public void MapOpenCommand_RequiresRoomName()
    {
        var command = new MapOpenCommand(new FakeMapControlService());
        var settings = new MapOpenCommand.Settings();

        var validation = settings.Validate();

        Assert.False(validation.Successful);
    }

    private sealed class FakeMapControlService : IMapControlService
    {
        public MapRoomGenerationOptions? GeneratedOptions { get; private set; }
        public MapGenerationResult? GenerationResult { get; set; }
        public MapGenerationResult? LastGenerationResult { get; private set; }
        public string? RemovedRoom { get; private set; }
        public bool? PurgedObjects { get; private set; }
        public string? AssetRoomName { get; private set; }
        public bool? AssetFull { get; private set; }
        public bool TerrainRefreshed { get; private set; }

        public Task<MapGenerationResult> GenerateRoomAsync(MapRoomGenerationOptions options, CancellationToken cancellationToken = default)
        {
            GeneratedOptions = options;
            LastGenerationResult = GenerationResult ?? new MapGenerationResult(options.RoomName, 0, 0, options.IncludeController, options.SourceCount, options.IncludeKeeperLairs, options.MineralType);
            return Task.FromResult(LastGenerationResult!);
        }

        public Task OpenRoomAsync(string roomName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task CloseRoomAsync(string roomName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveRoomAsync(string roomName, bool purgeObjects, CancellationToken cancellationToken = default)
        {
            RemovedRoom = roomName;
            PurgedObjects = purgeObjects;
            return Task.CompletedTask;
        }

        public Task UpdateRoomAssetsAsync(string roomName, bool fullRegeneration, CancellationToken cancellationToken = default)
        {
            AssetRoomName = roomName;
            AssetFull = fullRegeneration;
            return Task.CompletedTask;
        }

        public Task RefreshTerrainCacheAsync(CancellationToken cancellationToken = default)
        {
            TerrainRefreshed = true;
            return Task.CompletedTask;
        }
    }
    private sealed class EmptySettings : CommandSettings;
}
