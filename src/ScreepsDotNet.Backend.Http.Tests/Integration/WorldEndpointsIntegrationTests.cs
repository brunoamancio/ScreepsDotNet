namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Constants;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class WorldEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private static readonly SearchValues<char> WestEastSearch = SearchValues.Create("WE");
    private static readonly SearchValues<char> NorthSouthSearch = SearchValues.Create("NS");

    private static readonly string[] RequestedRooms =
    [
        SeedDataDefaults.World.StartRoom,
        SeedDataDefaults.World.SecondaryRoom
    ];

    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task MapStats_ReturnsOwnershipAndMinerals()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.World.MapStats);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            rooms = RequestedRooms,
            statName = "owners1"
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var root = payload.RootElement;
        Assert.Equal(SeedDataDefaults.World.GameTime, root.GetProperty("gameTime").GetInt32());

        var stats = root.GetProperty("stats");
        var startRoom = stats.GetProperty(SeedDataDefaults.World.StartRoom);
        Assert.Equal(SeedDataDefaults.User.Id, startRoom.GetProperty("own").GetProperty("user").GetString());
        Assert.True(startRoom.GetProperty("safeMode").GetBoolean());
        Assert.Equal(SeedDataDefaults.World.ControllerSign, startRoom.GetProperty("sign").GetProperty("text").GetString());
        Assert.Equal(SeedDataDefaults.World.MineralType, startRoom.GetProperty("minerals0").GetProperty("type").GetString());

        var invaderRoom = stats.GetProperty(SeedDataDefaults.World.SecondaryRoom);
        Assert.Equal(SeedDataDefaults.World.InvaderUser, invaderRoom.GetProperty("own").GetProperty("user").GetString());
    }

    [Fact]
    public async Task MapStats_WithShard_LooksUpShardRoom()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.World.MapStats);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            rooms = new[] { SeedDataDefaults.World.SecondaryShardRoom },
            statName = "owners1",
            shard = SeedDataDefaults.World.SecondaryShardName
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var stats = payload.RootElement.GetProperty("stats");
        Assert.True(stats.TryGetProperty(SeedDataDefaults.World.SecondaryShardRoom, out _));
    }

    [Fact]
    public async Task MapStats_WithShardPrefixedRoom_ResolvesShardAutomatically()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Game.World.MapStats);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            rooms = new[]
            {
                $"{SeedDataDefaults.World.SecondaryShardName}/{SeedDataDefaults.World.SecondaryShardRoom}"
            },
            statName = "owners1"
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var stats = payload.RootElement.GetProperty("stats");
        Assert.True(stats.TryGetProperty(SeedDataDefaults.World.SecondaryShardRoom, out _));
    }

    [Fact]
    public async Task RoomStatus_ReturnsRoomFields()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiRoutes.Game.World.RoomStatus}?room={SeedDataDefaults.World.StartRoom}");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal("normal", payload.RootElement.GetProperty("room").GetProperty("status").GetString());
    }

    [Fact]
    public async Task RoomStatus_WithShard_ReturnsRoomFields()
    {
        var token = await AuthenticateAsync();
        var url = $"{ApiRoutes.Game.World.RoomStatus}?room={SeedDataDefaults.World.SecondaryShardRoom}&shard={SeedDataDefaults.World.SecondaryShardName}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal("normal", payload.RootElement.GetProperty("room").GetProperty("status").GetString());
    }

    [Fact]
    public async Task RoomStatus_WithShardPrefixedRoom_ReturnsRoomFields()
    {
        var token = await AuthenticateAsync();
        var url = $"{ApiRoutes.Game.World.RoomStatus}?room={SeedDataDefaults.World.SecondaryShardName}/{SeedDataDefaults.World.SecondaryShardRoom}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal("normal", payload.RootElement.GetProperty("room").GetProperty("status").GetString());
    }

    [Fact]
    public async Task RoomTerrain_Encoded_ReturnsString()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Game.World.RoomTerrain}?room={SeedDataDefaults.World.StartRoom}&encoded=1");

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var terrain = payload.RootElement.GetProperty("terrain").EnumerateArray().First();
        Assert.Equal(2500, terrain.GetProperty("terrain").GetString()!.Length);
    }

    [Fact]
    public async Task RoomTerrain_Decoded_ReturnsTiles()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Game.World.RoomTerrain}?room={SeedDataDefaults.World.StartRoom}");

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var terrain = payload.RootElement.GetProperty("terrain").EnumerateArray().First();
        var tiles = terrain.GetProperty("terrain").EnumerateArray().ToList();
        Assert.Equal(2500, tiles.Count);
        Assert.Equal("plain", tiles.First().GetProperty("terrain").GetString());
    }

    [Fact]
    public async Task RoomTerrain_WithShard_ReturnsShardEntry()
    {
        var url = $"{ApiRoutes.Game.World.RoomTerrain}?room={SeedDataDefaults.World.SecondaryShardRoom}&shard={SeedDataDefaults.World.SecondaryShardName}&encoded=1";
        var response = await _client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var terrain = payload.RootElement.GetProperty("terrain").EnumerateArray().First();
        Assert.Equal(SeedDataDefaults.World.SecondaryShardRoom, terrain.GetProperty("room").GetString());
    }

    [Fact]
    public async Task RoomTerrain_WithShardPrefixedRoom_ReturnsEntry()
    {
        var url = $"{ApiRoutes.Game.World.RoomTerrain}?room={SeedDataDefaults.World.SecondaryShardName}/{SeedDataDefaults.World.SecondaryShardRoom}&encoded=1";
        var response = await _client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var terrain = payload.RootElement.GetProperty("terrain").EnumerateArray().First();
        Assert.Equal(SeedDataDefaults.World.SecondaryShardRoom, terrain.GetProperty("room").GetString());
    }

    [Fact]
    public async Task Rooms_ReturnsRequestedEntries()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.Rooms, new
        {
            rooms = RequestedRooms
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var rooms = payload.RootElement.GetProperty("rooms").EnumerateArray().ToList();
        Assert.Equal(2, rooms.Count);
    }

    [Fact]
    public async Task Rooms_WithShard_ReturnsShardEntries()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.Rooms, new
        {
            rooms = new[] { SeedDataDefaults.World.SecondaryShardRoom },
            shard = SeedDataDefaults.World.SecondaryShardName
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var rooms = payload.RootElement.GetProperty("rooms").EnumerateArray().ToList();
        Assert.Single(rooms);
        Assert.Equal(SeedDataDefaults.World.SecondaryShardRoom, rooms[0].GetProperty("room").GetString());
    }

    [Fact]
    public async Task Rooms_WithShardPrefixedInput_ReturnsShardEntries()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.Game.World.Rooms, new
        {
            rooms = new[]
            {
                $"{SeedDataDefaults.World.SecondaryShardName}/{SeedDataDefaults.World.SecondaryShardRoom}"
            }
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var rooms = payload.RootElement.GetProperty("rooms").EnumerateArray().ToList();
        Assert.Single(rooms);
        Assert.Equal(SeedDataDefaults.World.SecondaryShardRoom, rooms[0].GetProperty("room").GetString());
    }

    [Fact]
    public async Task WorldSize_ReturnsComputedDimensions()
    {
        var response = await _client.GetAsync(ApiRoutes.Game.World.WorldSize);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        var expectedWidth = CalculateExpectedWidth();
        var expectedHeight = CalculateExpectedHeight();
        Assert.Equal(expectedWidth, payload.RootElement.GetProperty("width").GetInt32());
        Assert.Equal(expectedHeight, payload.RootElement.GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task Time_ReturnsSeededGameTime()
    {
        var response = await _client.GetAsync(ApiRoutes.Game.World.Time);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(SeedDataDefaults.World.GameTime, payload.RootElement.GetProperty("time").GetInt32());
    }

    [Fact]
    public async Task Tick_ReturnsSeededTickDuration()
    {
        var response = await _client.GetAsync(ApiRoutes.Game.World.Tick);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(SeedDataDefaults.World.TickDuration, payload.RootElement.GetProperty("tick").GetInt32());
    }

    [Fact]
    public async Task ShardSeedData_IsAvailableInMongo()
    {
        var roomsCollection = harness.Database.GetCollection<BsonDocument>("rooms");
        var shardRoomFilter = Builders<BsonDocument>.Filter.Eq("_id", SeedDataDefaults.World.SecondaryShardRoom);
        var shardRoom = await roomsCollection.Find(shardRoomFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(shardRoom);
        Assert.True(shardRoom.TryGetValue("shard", out var roomShard));
        Assert.Equal(SeedDataDefaults.World.SecondaryShardName, roomShard.AsString);

        var terrainCollection = harness.Database.GetCollection<BsonDocument>("rooms.terrain");
        var terrain = await terrainCollection.Find(Builders<BsonDocument>.Filter.Eq("room", SeedDataDefaults.World.SecondaryShardRoom))
                                             .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(terrain);
        Assert.True(terrain.TryGetValue("shard", out var terrainShard));
        Assert.Equal(SeedDataDefaults.World.SecondaryShardName, terrainShard.AsString);

        var objects = harness.Database.GetCollection<BsonDocument>("rooms.objects");
        var controllerFilter = Builders<BsonDocument>.Filter.And(
            Builders<BsonDocument>.Filter.Eq("room", SeedDataDefaults.World.SecondaryShardRoom),
            Builders<BsonDocument>.Filter.Eq("type", RoomObjectType.Controller.ToDocumentValue()));
        var controller = await objects.Find(controllerFilter).FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(controller);
        Assert.True(controller.TryGetValue("shard", out var controllerShard));
        Assert.Equal(SeedDataDefaults.World.SecondaryShardName, controllerShard.AsString);
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = SeedDataDefaults.Auth.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }

    private static int CalculateExpectedWidth()
    {
        var rooms = GetSeedRooms();
        var westValues = rooms.Select(ParseWestCoordinate).ToList();
        return westValues.Max() - westValues.Min() + 1;
    }

    private static int CalculateExpectedHeight()
    {
        var rooms = GetSeedRooms();
        var northValues = rooms.Select(ParseNorthCoordinate).ToList();
        return northValues.Max() - northValues.Min() + 1;
    }

    private static IReadOnlyCollection<string> GetSeedRooms()
        =>
        [
            SeedDataDefaults.World.StartRoom,
            SeedDataDefaults.World.SecondaryRoom,
            SeedDataDefaults.World.SecondaryShardRoom,
            SeedDataDefaults.Bots.SecondaryShardRoom,
            SeedDataDefaults.Strongholds.SecondaryShardRoom,
            SeedDataDefaults.Intents.SecondaryShardRoom
        ];

    private static int ParseWestCoordinate(string room)
    {
        var span = room.AsSpan();
        var start = span.IndexOfAny(WestEastSearch);
        if (start < 0 || start + 1 >= span.Length)
            return 0;

        var remaining = span[(start + 1)..];
        var end = remaining.IndexOfAny(NorthSouthSearch);
        var numberSpan = end >= 0 ? remaining[..end] : remaining;
        return int.TryParse(numberSpan, out var value) ? value : 0;
    }

    private static int ParseNorthCoordinate(string room)
    {
        var span = room.AsSpan();
        var start = span.IndexOfAny(NorthSouthSearch);
        if (start < 0 || start + 1 >= span.Length)
            return 0;

        var numberSpan = span[(start + 1)..];
        return int.TryParse(numberSpan, out var value) ? value : 0;
    }
}
