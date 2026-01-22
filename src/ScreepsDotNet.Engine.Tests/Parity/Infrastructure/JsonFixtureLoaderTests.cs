namespace ScreepsDotNet.Engine.Tests.Parity.Infrastructure;

using ScreepsDotNet.Common.Constants;
using ScreepsDotNet.Common.Types;

public sealed class JsonFixtureLoaderTests
{
    [Fact]
    public async Task LoadFromFileAsync_HarvestBasic_LoadsCorrectly()
    {
        // Arrange
        var fixturePath = Path.Combine("Parity", "Fixtures", "harvest_basic.json");

        // Act
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(100, state.GameTime);
        Assert.Equal("W1N1", state.RoomName);
        Assert.Equal(2, state.Objects.Count);

        var creep = state.Objects["creep1"];
        Assert.Equal("creep1", creep.Id);
        Assert.Equal(RoomObjectTypes.Creep, creep.Type);
        Assert.Equal(25, creep.X);
        Assert.Equal(25, creep.Y);
        Assert.Equal("user1", creep.UserId);
        Assert.Equal(2, creep.Body.Count);
        Assert.Equal(BodyPartType.Work, creep.Body[0].Type);
        Assert.Equal(BodyPartType.Move, creep.Body[1].Type);
        Assert.Equal(0, creep.Store[ResourceTypes.Energy]);
        Assert.Equal(50, creep.StoreCapacity);

        var source = state.Objects["source1"];
        Assert.Equal("source1", source.Id);
        Assert.Equal(RoomObjectTypes.Source, source.Type);
        Assert.Equal(26, source.X);
        Assert.Equal(25, source.Y);
        Assert.Equal(3000, source.Energy);

        var user = state.Users["user1"];
        Assert.Equal("user1", user.Id);
        Assert.Equal(100.0, user.Cpu);
        Assert.Equal(0.0, user.Power);

        Assert.NotNull(state.Intents);
        var userIntents = state.Intents.Users["user1"];
        Assert.NotNull(userIntents);
        var creepIntents = userIntents.ObjectIntents["creep1"];
        Assert.Single(creepIntents);
        var harvestIntent = creepIntents[0];
        Assert.Equal(IntentKeys.Harvest, harvestIntent.Name);
        Assert.Equal("source1", harvestIntent.Arguments[0].Fields[IntentKeys.TargetId].TextValue);

        var terrain = state.Terrain["W1N1"];
        Assert.Equal("W1N1", terrain.RoomName);
        Assert.NotNull(terrain.Terrain);
        Assert.Equal(2500, terrain.Terrain.Length);
    }

    [Fact]
    public async Task LoadFromFileAsync_TransferBasic_LoadsCorrectly()
    {
        // Arrange
        var fixturePath = Path.Combine("Parity", "Fixtures", "transfer_basic.json");

        // Act
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(200, state.GameTime);
        Assert.Equal("W1N1", state.RoomName);
        Assert.Equal(2, state.Objects.Count);

        var creep = state.Objects["creep1"];
        Assert.Equal(50, creep.Store[ResourceTypes.Energy]);

        var spawn = state.Objects["spawn1"];
        Assert.Equal(RoomObjectTypes.Spawn, spawn.Type);
        Assert.Equal(200, spawn.Store[ResourceTypes.Energy]);

        Assert.NotNull(state.Intents);
        var userIntents = state.Intents.Users["user1"];
        var creepIntents = userIntents.ObjectIntents["creep1"];
        Assert.Single(creepIntents);
        var transferIntent = creepIntents[0];
        Assert.Equal(IntentKeys.Transfer, transferIntent.Name);
        Assert.Equal("spawn1", transferIntent.Arguments[0].Fields[IntentKeys.TargetId].TextValue);
        Assert.Equal(ResourceTypes.Energy, transferIntent.Arguments[0].Fields[IntentKeys.ResourceType].TextValue);
        Assert.Equal(50, transferIntent.Arguments[0].Fields[IntentKeys.Amount].NumberValue);
    }

    [Fact]
    public async Task LoadFromFileAsync_ControllerUpgrade_LoadsCorrectly()
    {
        // Arrange
        var fixturePath = Path.Combine("Parity", "Fixtures", "controller_upgrade.json");

        // Act
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(300, state.GameTime);
        Assert.Equal(2, state.Objects.Count);

        var creep = state.Objects["creep1"];
        Assert.Equal(3, creep.Body.Count);

        var controller = state.Objects["controller1"];
        Assert.Equal(RoomObjectTypes.Controller, controller.Type);
        Assert.Equal(1, controller.Level);
        Assert.Equal(0, controller.Progress);
        Assert.Equal(200, controller.ProgressTotal);

        Assert.NotNull(state.Intents);
        var userIntents = state.Intents.Users["user1"];
        var creepIntents = userIntents.ObjectIntents["creep1"];
        Assert.Single(creepIntents);
        var upgradeIntent = creepIntents[0];
        Assert.Equal(IntentKeys.UpgradeController, upgradeIntent.Name);
        Assert.Equal("controller1", upgradeIntent.Arguments[0].Fields[IntentKeys.TargetId].TextValue);
    }

    [Fact]
    public async Task LoadFromFileAsync_LinkTransfer_LoadsCorrectly()
    {
        // Arrange
        var fixturePath = Path.Combine("Parity", "Fixtures", "link_transfer.json");

        // Act
        var state = await JsonFixtureLoader.LoadFromFileAsync(fixturePath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(400, state.GameTime);
        Assert.Equal(2, state.Objects.Count);

        var link1 = state.Objects["link1"];
        Assert.Equal(RoomObjectTypes.Link, link1.Type);
        Assert.Equal(400, link1.Store[ResourceTypes.Energy]);
        Assert.Equal(0, link1.CooldownTime);

        var link2 = state.Objects["link2"];
        Assert.Equal(0, link2.Store[ResourceTypes.Energy]);

        Assert.NotNull(state.Intents);
        var userIntents = state.Intents.Users["user1"];
        var link1Intents = userIntents.ObjectIntents["link1"];
        Assert.Single(link1Intents);
        var transferEnergyIntent = link1Intents[0];
        Assert.Equal(IntentKeys.TransferEnergy, transferEnergyIntent.Name);
        Assert.Equal("link2", transferEnergyIntent.Arguments[0].Fields[IntentKeys.TargetId].TextValue);
        Assert.Equal(100, transferEnergyIntent.Arguments[0].Fields[IntentKeys.Amount].NumberValue);
    }

    [Fact]
    public void LoadFromJson_InvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        Assert.Throws<System.Text.Json.JsonException>(() =>
            JsonFixtureLoader.LoadFromJson(invalidJson));
    }

    [Fact]
    public void LoadFromJson_TerrainTooShort_ThrowsException()
    {
        // Arrange
        var json = """
        {
          "gameTime": 100,
          "room": "W1N1",
          "shard": "shard0",
          "terrain": "000",
          "objects": [],
          "intents": {},
          "users": {}
        }
        """;

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            JsonFixtureLoader.LoadFromJson(json));
        Assert.Contains("2500", exception.Message);
    }

    [Fact]
    public void LoadFromJson_AllOptionalFieldsMissing_LoadsWithDefaults()
    {
        // Arrange - Minimal fixture with only required fields
        var terrain = new string('0', 2500);
        var json = $$"""
        {
          "gameTime": 500,
          "room": "W2N2",
          "shard": "shard1",
          "terrain": "{{terrain}}",
          "objects": [
            {
              "_id": "obj1",
              "type": "creep",
              "x": 10,
              "y": 10
            }
          ],
          "intents": {},
          "users": {}
        }
        """;

        // Act
        var state = JsonFixtureLoader.LoadFromJson(json);

        // Assert
        Assert.Equal(500, state.GameTime);
        Assert.Equal("W2N2", state.RoomName);
        Assert.Single(state.Objects);
        Assert.Empty(state.Users);

        var obj = state.Objects["obj1"];
        Assert.Equal("obj1", obj.Id);
        Assert.Equal(10, obj.X);
        Assert.Equal(10, obj.Y);
        Assert.Null(obj.UserId);
        Assert.Empty(obj.Body);
        Assert.Empty(obj.Store);
    }
}
