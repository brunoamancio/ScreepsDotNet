namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MongoDB.Driver;
using ScreepsDotNet.Backend.Core.Seeding;
using ScreepsDotNet.Backend.Http.Routing;
using ScreepsDotNet.Backend.Http.Tests.TestSupport;
using ScreepsDotNet.Storage.MongoRedis.Repositories.Documents;

[Collection(IntegrationTestSuiteDefinition.Name)]
public sealed class RegistrationEndpointsIntegrationTests(IntegrationTestHarness harness) : IAsyncLifetime
{
    private readonly TestHttpClient _client = new(harness.Factory.CreateClient());
    private const string UsersCollectionName = "users";

    public ValueTask InitializeAsync() => new(harness.ResetStateAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CheckEmail_InvalidFormat_ReturnsBadRequest()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Register.CheckEmail}?email=invalid");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckEmail_Existing_ReturnsExistsError()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Register.CheckEmail}?email={SeedDataDefaults.User.Email}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckEmail_New_ReturnsEmptyPayload()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Register.CheckEmail}?email=fresh@example.com");
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Equal(JsonValueKind.Object, payload.RootElement.ValueKind);
        Assert.Empty(payload.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task CheckUsername_InvalidFormat_ReturnsBadRequest()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Register.CheckUsername}?username=?bad");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckUsername_Existing_ReturnsExistsError()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Register.CheckUsername}?username={SeedDataDefaults.User.Username}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CheckUsername_New_ReturnsSuccess()
    {
        var response = await _client.GetAsync($"{ApiRoutes.Register.CheckUsername}?username=NewUser123");
        response.EnsureSuccessStatusCode();
        using var payload = JsonDocument.Parse(await TestHttpClient.ReadAsStringAsync(response));
        Assert.Empty(payload.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task SetUsername_UserAlreadyHasName_ReturnsBadRequest()
    {
        var token = await AuthenticateAsync();
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Register.SetUsername)
        {
            Content = JsonContent.Create(new { username = "AnotherName" })
        };
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SetUsername_SucceedsWhenUserHasNoName()
    {
        var users = harness.Database.GetCollection<UserDocument>(UsersCollectionName);
        await users.UpdateOneAsync(user => user.Id == SeedDataDefaults.User.Id,
                                   Builders<UserDocument>.Update.Set(user => user.Username, null)
                                                                 .Set(user => user.UsernameLower, null),
                                   cancellationToken: TestContext.Current.CancellationToken);

        var token = await AuthenticateAsync();
        var payload = new
        {
            username = "NewHandle",
            email = "new-handle@example.com"
        };
        var request = new HttpRequestMessage(HttpMethod.Post, ApiRoutes.Register.SetUsername)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.TryAddWithoutValidation(AuthHeaderNames.Token, token);

        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var updated = await users.Find(user => user.Id == SeedDataDefaults.User.Id)
                                 .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(updated);
        Assert.Equal(payload.username, updated!.Username);
        Assert.Equal(payload.username.ToLowerInvariant(), updated.UsernameLower);
        Assert.Equal(payload.email, updated.Email);
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
}
