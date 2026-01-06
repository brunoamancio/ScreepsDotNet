namespace ScreepsDotNet.Backend.Http.Tests.Integration;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

internal sealed class IntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private const string IntegrationEnvironmentName = "Integration";
    private const string StorageSection = "Storage:MongoRedis";
    private const string MongoConnectionStringKey = StorageSection + ":MongoConnectionString";
    private const string MongoDatabaseKey = StorageSection + ":MongoDatabase";
    private const string RedisConnectionStringKey = StorageSection + ":RedisConnectionString";
    private const string AuthSection = "Auth";
    private const string UseNativeAuthKey = AuthSection + ":UseNativeAuth";
    private const string TicketsSection = AuthSection + ":Tickets:0";
    private const string TicketKey = TicketsSection + ":Ticket";
    private const string TicketUserIdKey = TicketsSection + ":UserId";
    private const string TicketSteamIdKey = TicketsSection + ":SteamId";

    private readonly string _mongoConnectionString;
    private readonly string _mongoDatabase;
    private readonly string _redisConnectionString;
    private readonly string _userId;
    private readonly string _ticket;
    private readonly string _steamId;

    public IntegrationWebApplicationFactory(string mongoConnectionString, string mongoDatabase, string redisConnectionString, string userId, string ticket, string steamId)
    {
        _mongoConnectionString = mongoConnectionString;
        _mongoDatabase = mongoDatabase;
        _redisConnectionString = redisConnectionString;
        _userId = userId;
        _ticket = ticket;
        _steamId = steamId;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(IntegrationEnvironmentName);
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            var settings = new Dictionary<string, string?>
            {
                [MongoConnectionStringKey] = _mongoConnectionString,
                [MongoDatabaseKey] = _mongoDatabase,
                [RedisConnectionStringKey] = _redisConnectionString,
                [UseNativeAuthKey] = "false",
                [TicketKey] = _ticket,
                [TicketUserIdKey] = _userId,
                [TicketSteamIdKey] = _steamId
            };
            configBuilder.AddInMemoryCollection(settings);
        });
    }
}
