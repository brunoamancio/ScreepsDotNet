using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Http.Constants;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.Web;

namespace ScreepsDotNet.Backend.Http.Tests.Endpoints;

public class UserEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly FakeUserWorldRepository _userWorldRepository;
    private readonly FakeUserRepository _userRepository;

    private const string CustomControllerRoom = "W12N3";
    private const string RoomsQueryUserId = "user-1";
    private const string StatsValidInterval = "8";
    private const string StatsInvalidInterval = "1";
    private static readonly string[] SampleRooms = ["W1N1", "W2N2"];
    private const string UsernameQueryParameter = "?username=TestUser";
    private const string RoomsQueryParameter = "?id=" + RoomsQueryUserId;
    private const string StatsValidQueryParameter = "?interval=" + StatsValidInterval;
    private const string StatsInvalidQueryParameter = "?interval=" + StatsInvalidInterval;

    public UserEndpointTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        var services = factory.Services;
        _userWorldRepository = (FakeUserWorldRepository)services.GetRequiredService<IUserWorldRepository>();
        _userRepository = (FakeUserRepository)services.GetRequiredService<IUserRepository>();
    }

    [Fact]
    public async Task WorldStartRoom_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(ApiRoutes.User.WorldStartRoom);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(AuthResponseMessages.Unauthorized, payload.RootElement.GetProperty(AuthResponseFields.Error).GetString());
    }

    [Fact]
    public async Task WorldStartRoom_WithToken_ReturnsRoom()
    {
        _userWorldRepository.ControllerRoom = CustomControllerRoom;
        var token = await AuthenticateAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.WorldStartRoom);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rooms = payload.RootElement.GetProperty(UserResponseFields.Room).EnumerateArray().Select(element => element.GetString()).ToList();
        Assert.Contains(CustomControllerRoom, rooms);
    }

    [Fact]
    public async Task WorldStatus_WithToken_ReturnsStatus()
    {
        _userWorldRepository.WorldStatus = Core.Models.UserWorldStatus.Lost;
        var token = await AuthenticateAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.WorldStatus);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var expectedStatus = nameof(Core.Models.UserWorldStatus.Lost).ToLowerInvariant();
        Assert.Equal(expectedStatus, payload.RootElement.GetProperty(UserResponseFields.Status).GetString());
    }

    [Fact]
    public async Task RespawnProhibitedRooms_WithToken_ReturnsEmptyList()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.RespawnProhibitedRooms);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(payload.RootElement.GetProperty(UserResponseFields.Rooms).EnumerateArray());
    }

    [Fact]
    public async Task UserMemory_WithToken_ReturnsCompressedPayload()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.Memory);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var data = payload.RootElement.GetProperty(UserResponseFields.Data).GetString();
        Assert.False(string.IsNullOrWhiteSpace(data));
        Assert.StartsWith(MemoryConstants.GzipPrefix, data, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UserMemory_PostTooLarge_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Memory);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            value = new string('x', (1024 * 1024) + 1)
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserMemorySegment_InvalidId_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.MemorySegment + "?segment=200");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserMemorySegment_WithToken_ReturnsEmptyData()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.MemorySegment + "?segment=1");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(string.Empty, payload.RootElement.GetProperty(UserResponseFields.Data).GetString());
    }

    [Fact]
    public async Task UserConsole_WithToken_ReturnsEmptyObject()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Console);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { expression = "console.log('test');" });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, payload.RootElement.ValueKind);
        Assert.Empty(payload.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task UserOverview_WithValidInterval_ReturnsDefaultPayload()
    {
        var token = await AuthenticateAsync();
        _userWorldRepository.ControllerRooms = SampleRooms;
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.Overview + "?interval=8&statName=energyHarvested");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        var rooms = root.GetProperty(UserResponseFields.Rooms).EnumerateArray().Select(element => element.GetString()).ToList();
        Assert.Equal(SampleRooms, rooms);
        Assert.Equal(JsonValueKind.Object, root.GetProperty(UserResponseFields.Stats).ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty(UserResponseFields.Totals).ValueKind);
        Assert.Empty(root.GetProperty(UserResponseFields.GameTimes).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, root.GetProperty(UserResponseFields.StatsMax).ValueKind);
    }

    [Fact]
    public async Task UserOverview_InvalidInterval_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.Overview + "?interval=5");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserCode_PostModules_ReturnsTimestamp()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Code);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            modules = new Dictionary<string, string>
            {
                ["main"] = "module.exports.loop = function () { Game.notify('tick'); };"
            },
            branch = "default",
            hash = "abc123"
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty(UserResponseFields.Timestamp, out _));
    }

    [Fact]
    public async Task UserSetActiveBranch_InvalidName_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.SetActiveBranch);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { branch = "default", activeName = "invalid" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserSetActiveBranch_WithToken_ReturnsTimestamp()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.SetActiveBranch);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { branch = "default", activeName = "activeWorld" });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty(UserResponseFields.Timestamp, out _));
    }

    [Fact]
    public async Task UserCloneBranch_WithToken_ReturnsTimestamp()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.CloneBranch);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { branch = "default", newName = "sim-copy" });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty(UserResponseFields.Timestamp, out _));
    }

    [Fact]
    public async Task UserMoneyHistory_WithToken_ReturnsPagedList()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.MoneyHistory + "?page=0");
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = payload.RootElement;
        Assert.Equal(0, root.GetProperty(UserResponseFields.Page).GetInt32());
        Assert.True(root.GetProperty(UserResponseFields.List).EnumerateArray().Any());
        Assert.False(root.GetProperty(UserResponseFields.HasMore).GetBoolean());
    }

    [Fact]
    public async Task UserDeleteBranch_WithToken_ReturnsTimestamp()
    {
        var token = await AuthenticateAsync();

        var cloneRequest = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.CloneBranch);
        cloneRequest.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        cloneRequest.Content = JsonContent.Create(new { branch = "default", newName = "old-branch" });
        var cloneResponse = await _client.SendAsync(cloneRequest);
        cloneResponse.EnsureSuccessStatusCode();

        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.DeleteBranch);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { branch = "old-branch" });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty(UserResponseFields.Timestamp, out _));
    }

    [Fact]
    public async Task UserTutorialDone_WithToken_ReturnsEmptyPayload()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.TutorialDone);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Object, payload.RootElement.ValueKind);
        Assert.Empty(payload.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task UserFind_WithoutParams_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Find);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserFind_WithUsername_ReturnsProfile()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Find + UsernameQueryParameter);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var userElement = payload.RootElement.GetProperty(UserResponseFields.User);
        Assert.Equal(AuthTestValues.UserId, userElement.GetProperty(UserResponseFields.Id).GetString());
    }

    [Fact]
    public async Task UserRooms_WithoutUserId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Rooms);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserRooms_WithUserId_ReturnsRooms()
    {
        _userWorldRepository.ControllerRooms = SampleRooms;

        var response = await _client.GetAsync(ApiRoutes.User.Rooms + RoomsQueryParameter);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rooms = payload.RootElement.GetProperty(UserResponseFields.Rooms).EnumerateArray().Select(element => element.GetString()).ToList();
        Assert.Contains(SampleRooms[0], rooms);
        Assert.Contains(SampleRooms[1], rooms);
    }

    [Fact]
    public async Task UserStats_InvalidInterval_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Stats + StatsInvalidQueryParameter);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserStats_ValidInterval_ReturnsStats()
    {
        var response = await _client.GetAsync(ApiRoutes.User.Stats + StatsValidQueryParameter);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stats = payload.RootElement.GetProperty(UserResponseFields.Stats);
        Assert.Equal(int.Parse(StatsValidInterval, System.Globalization.CultureInfo.InvariantCulture),
                     stats.GetProperty(UserResponseFields.Interval).GetInt32());
        Assert.True(stats.GetProperty(UserResponseFields.ActiveUsers).GetInt32() > 0);
        Assert.Equal(0, stats.GetProperty(UserResponseFields.RoomsControlled).GetInt32());
    }

    [Fact]
    public async Task UserNotifyPrefs_WithToken_UpdatesPreferences()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.NotifyPrefs);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        var payload = new Dictionary<string, object?>
        {
            [UserResponseFields.NotifyDisabled] = true,
            [UserResponseFields.NotifyInterval] = 10,
            [UserResponseFields.NotifyErrorsInterval] = 30
        };
        request.Content = JsonContent.Create(payload);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        var snapshot = _userRepository.GetNotifyPreferencesSnapshot();
        Assert.True(snapshot.TryGetValue(UserResponseFields.NotifyDisabled, out var disabledValue) && disabledValue is bool disabled && disabled);
        Assert.True(snapshot.TryGetValue(UserResponseFields.NotifyInterval, out var intervalValue) && intervalValue is int interval && interval == 10);
        Assert.True(snapshot.TryGetValue(UserResponseFields.NotifyErrorsInterval, out var errorsIntervalValue) && errorsIntervalValue is int errorsInterval && errorsInterval == 30);
    }

    [Fact]
    public async Task UserBadgeSvg_WithoutUsername_ReturnsBadRequest()
    {
        var response = await _client.GetAsync(ApiRoutes.User.BadgeSvg);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserBadgeSvg_WithUsername_ReturnsSvg()
    {
        var response = await _client.GetAsync(ApiRoutes.User.BadgeSvg + UsernameQueryParameter);

        response.EnsureSuccessStatusCode();
        Assert.Equal(ContentTypes.Svg, response.Content.Headers.ContentType?.MediaType);
        var svg = await response.Content.ReadAsStringAsync();
        Assert.Contains("<svg", svg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UserBranches_WithToken_ReturnsList()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.Branches);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var list = payload.RootElement.GetProperty(UserResponseFields.List);
        Assert.True(list.GetArrayLength() > 0);
    }

    [Fact]
    public async Task UserCode_WithToken_ReturnsModules()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.User.Code);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.TryGetProperty(UserResponseFields.Modules, out _));
    }

    [Fact]
    public async Task UserBadge_WithValidPayload_UpdatesState()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Badge);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            badge = new
            {
                type = 2,
                color1 = "#abcdef",
                color2 = "#123456",
                color3 = "#654321",
                param = 5,
                flip = true
            }
        });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.NotNull(_userRepository.LastBadgeUpdate);
        Assert.Equal(2, _userRepository.LastBadgeUpdate?.Type);
    }

    [Fact]
    public async Task UserBadge_InvalidColor_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Badge);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new
        {
            badge = new
            {
                type = 2,
                color1 = "invalid",
                color2 = "#123456",
                color3 = "#654321",
                param = 5,
                flip = true
            }
        });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserEmail_InvalidFormat_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Email);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { email = "not-an-email" });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserEmail_Duplicate_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Email);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { email = FakeUserRepository.DuplicateEmail });

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserEmail_Valid_UpdatesRepository()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Email);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { email = "new-email@example.com" });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("new-email@example.com", _userRepository.CurrentEmail);
    }

    [Fact]
    public async Task UserSetSteamVisible_TogglesHiddenFlag()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.SetSteamVisible);
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);
        request.Content = JsonContent.Create(new { visible = true });

        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.False(_userRepository.IsSteamProfileHidden);
    }

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = AuthTestValues.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty(AuthResponseFields.Token).GetString()!;
    }
}
