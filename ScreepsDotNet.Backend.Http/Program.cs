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
using ScreepsDotNet.Storage.MongoRedis.Seeding;
using ScreepsDotNet.Storage.MongoRedis.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<MongoRedisStorageOptions>(builder.Configuration.GetSection(MongoRedisStorageOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<BotManifestOptions>(builder.Configuration.GetSection(nameof(BotManifestOptions)));
builder.Services.PostConfigure<BotManifestOptions>(options => {
    options.ManifestFile ??= builder.Configuration["modfile"]
                             ?? builder.Configuration["MODFILE"]
                             ?? Environment.GetEnvironmentVariable("MODFILE");
});
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
builder.Services.AddSingleton<IWorldStatsRepository, MongoWorldStatsRepository>();
builder.Services.AddSingleton<IWorldMetadataRepository, MongoWorldMetadataRepository>();
builder.Services.AddSingleton<IRoomOverviewRepository, MongoRoomOverviewRepository>();
builder.Services.AddSingleton<IVersionMetadataRepository, MongoVersionMetadataRepository>();
builder.Services.AddSingleton<IUserRespawnService, MongoUserRespawnService>();
builder.Services.AddSingleton<IPlayerSpawnService, MongoPlayerSpawnService>();
builder.Services.AddSingleton<IConstructionService, MongoConstructionService>();
builder.Services.AddSingleton<IFlagService, MongoFlagService>();
builder.Services.AddSingleton<INotifyWhenAttackedService, MongoNotifyWhenAttackedService>();
builder.Services.AddSingleton<IInvaderService, MongoInvaderService>();
builder.Services.AddSingleton<IIntentService, MongoIntentService>();
builder.Services.AddSingleton<IPowerCreepService, MongoPowerCreepService>();
builder.Services.AddSingleton<IStrongholdTemplateProvider, EmbeddedStrongholdTemplateProvider>();
builder.Services.AddSingleton<IStrongholdControlService, MongoStrongholdControlService>();
builder.Services.AddSingleton<IBotDefinitionProvider, FileSystemBotDefinitionProvider>();
builder.Services.AddSingleton<IBotControlService, MongoBotControlService>();
builder.Services.AddSingleton<ISystemControlService, RedisSystemControlService>();
builder.Services.AddSingleton<IMapControlService, MongoMapControlService>();
builder.Services.AddSingleton<IBadgeSvgGenerator, BadgeSvgGenerator>();
builder.Services.AddSingleton<IVersionInfoProvider, VersionInfoProvider>();
builder.Services.AddSingleton<ITokenService, RedisTokenService>();
builder.Services.AddSingleton<ISeedDataService, SeedDataService>();
builder.Services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
builder.Services.AddHealthChecks().AddCheck<StorageHealthCheck>(StorageHealthCheck.HealthCheckName);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.MapHealthChecks(HealthCheckOptionsFactory.HealthEndpoint, HealthCheckOptionsFactory.Create());
app.MapBackendEndpoints();
app.Run();
