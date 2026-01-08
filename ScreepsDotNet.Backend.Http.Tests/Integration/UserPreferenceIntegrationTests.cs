namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class UserPreferenceIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly HttpClient _client = harness.Factory.CreateClient();

    public Task InitializeAsync() => harness.ResetStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<string> AuthenticateAsync()
    {
        var response = await _client.PostAsJsonAsync(ApiRoutes.AuthSteamTicket, new
        {
            ticket = SeedDataDefaults.Auth.Ticket,
            useNativeAuth = false
        });

        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return payload.RootElement.GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task NotifyPrefs_UpdatesUserDocument()
    {
        var token = await AuthenticateAsync();
        var request = new
        {
            disabled = true,
            disabledOnMessages = false,
            sendOnline = true,
            interval = 60,
            errorsInterval = 30
        };

        var message = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.NotifyPrefs);
        message.Headers.Add("X-Token", token);
        message.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var notifyPrefs = content.GetProperty("notifyPrefs");
        Assert.True(notifyPrefs.GetProperty("disabled").GetBoolean());
        Assert.False(notifyPrefs.GetProperty("disabledOnMessages").GetBoolean());
        Assert.True(notifyPrefs.GetProperty("sendOnline").GetBoolean());
        Assert.Equal(60, notifyPrefs.GetProperty("interval").GetInt32());
        Assert.Equal(30, notifyPrefs.GetProperty("errorsInterval").GetInt32());

        // Verify DB
        var users = harness.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(u => u.Id == SeedDataDefaults.User.Id).FirstOrDefaultAsync();
        Assert.NotNull(user!.NotifyPrefs);
        Assert.Equal(true, user.NotifyPrefs["disabled"]);
        Assert.Equal(60, user.NotifyPrefs["interval"]);
    }

    [Fact]
    public async Task SetSteamVisible_UpdatesUserDocument()
    {
        var token = await AuthenticateAsync();
        var request = new { visible = true };

        var message = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.SetSteamVisible);
        message.Headers.Add("X-Token", token);
        message.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        // Verify DB
        var users = harness.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(u => u.Id == SeedDataDefaults.User.Id).FirstOrDefaultAsync();
        Assert.NotNull(user!.Steam);
        Assert.Equal(false, user.Steam.SteamProfileLinkHidden);
    }

    [Fact]
    public async Task TutorialDone_ReturnsOk()
    {
        var token = await AuthenticateAsync();
        var message = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.TutorialDone);
        message.Headers.Add("X-Token", token);

        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Email_UpdatesUserDocument()
    {
        var token = await AuthenticateAsync();
        const string newEmail = "new-email@example.com";
        var request = new { email = newEmail };

        var message = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Email);
        message.Headers.Add("X-Token", token);
        message.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        // Verify DB
        var users = harness.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(u => u.Id == SeedDataDefaults.User.Id).FirstOrDefaultAsync();
        Assert.Equal(newEmail, user!.Email);
        Assert.Equal(false, user.EmailDirty);
    }

    [Fact]
    public async Task Badge_UpdatesUserDocument()
    {
        var token = await AuthenticateAsync();
        var request = new
        {
            badge = new
            {
                type = 5,
                color1 = "#ff0000",
                color2 = "#00ff00",
                color3 = "#0000ff",
                param = 42,
                flip = true
            }
        };

        var message = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.User.Badge);
        message.Headers.Add("X-Token", token);
        message.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(message);

        response.EnsureSuccessStatusCode();

        // Verify DB
        var users = harness.Database.GetCollection<UserDocument>("users");
        var user = await users.Find(u => u.Id == SeedDataDefaults.User.Id).FirstOrDefaultAsync();
        Assert.NotNull(user!.Badge);
        Assert.Equal(5, user.Badge["type"]);
        Assert.Equal("#ff0000", user.Badge["color1"]);
        Assert.Equal(42d, user.Badge["param"]);
        Assert.Equal(true, user.Badge["flip"]);
    }
}
