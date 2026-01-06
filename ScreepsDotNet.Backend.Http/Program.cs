using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Core.Storage;
using ScreepsDotNet.Backend.Http.Authentication;
using ScreepsDotNet.Backend.Http.Endpoints;
using ScreepsDotNet.Backend.Http.Health;
using ScreepsDotNet.Backend.Http.Rendering;
using ScreepsDotNet.Storage.MongoRedis.Adapters;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories;
using ScreepsDotNet.Storage.MongoRedis.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<ServerDataOptions>(builder.Configuration.GetSection(ServerDataOptions.SectionName));
builder.Services.Configure<VersionInfoOptions>(builder.Configuration.GetSection(VersionInfoOptions.SectionName));
builder.Services.Configure<MongoRedisStorageOptions>(builder.Configuration.GetSection(MongoRedisStorageOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<IStorageAdapter, MongoRedisStorageAdapter>();
builder.Services.AddSingleton<IMongoDatabaseProvider, MongoDatabaseProvider>();
builder.Services.AddSingleton<IRedisConnectionProvider, RedisConnectionProvider>();
builder.Services.AddSingleton<IUserRepository, MongoUserRepository>();
builder.Services.AddSingleton<IRoomRepository, MongoRoomRepository>();
builder.Services.AddSingleton<IUserWorldRepository, MongoUserWorldRepository>();
builder.Services.AddSingleton<IServerDataRepository, MongoServerDataRepository>();
builder.Services.AddSingleton<IUserCodeRepository, MongoUserCodeRepository>();
builder.Services.AddSingleton<IUserMemoryRepository, MongoUserMemoryRepository>();
builder.Services.AddSingleton<IUserConsoleRepository, MongoUserConsoleRepository>();
builder.Services.AddSingleton<IUserMoneyRepository, MongoUserMoneyRepository>();
builder.Services.AddSingleton<IMarketOrderRepository, MongoMarketOrderRepository>();
builder.Services.AddSingleton<IMarketStatsRepository, MongoMarketStatsRepository>();
builder.Services.AddSingleton<IRoomStatusRepository, MongoRoomStatusRepository>();
builder.Services.AddSingleton<IRoomTerrainRepository, MongoRoomTerrainRepository>();
builder.Services.AddSingleton<IUserRespawnService, MongoUserRespawnService>();
builder.Services.AddSingleton<IBadgeSvgGenerator, BadgeSvgGenerator>();
builder.Services.AddSingleton<IVersionInfoProvider, VersionInfoProvider>();
builder.Services.AddSingleton<ITokenService, RedisTokenService>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHealthChecks().AddCheck<StorageHealthCheck>(StorageHealthCheck.HealthCheckName);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.MapHealthChecks(HealthCheckOptionsFactory.HealthEndpoint, HealthCheckOptionsFactory.Create());
app.MapBackendEndpoints();
app.Run();

