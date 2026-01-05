using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Repositories;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Core.Storage;
using ScreepsDotNet.Backend.Http.Endpoints;
using ScreepsDotNet.Backend.Http.Health;
using ScreepsDotNet.Storage.MongoRedis.Adapters;
using ScreepsDotNet.Storage.MongoRedis.Options;
using ScreepsDotNet.Storage.MongoRedis.Providers;
using ScreepsDotNet.Storage.MongoRedis.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<ServerDataOptions>(builder.Configuration.GetSection(ServerDataOptions.SectionName));
builder.Services.Configure<VersionInfoOptions>(builder.Configuration.GetSection(VersionInfoOptions.SectionName));
builder.Services.Configure<MongoRedisStorageOptions>(builder.Configuration.GetSection(MongoRedisStorageOptions.SectionName));
builder.Services.AddSingleton<IStorageAdapter, MongoRedisStorageAdapter>();
builder.Services.AddSingleton<IMongoDatabaseProvider, MongoDatabaseProvider>();
builder.Services.AddSingleton<IUserRepository, MongoUserRepository>();
builder.Services.AddSingleton<IRoomRepository, MongoRoomRepository>();
builder.Services.AddSingleton<IVersionInfoProvider, VersionInfoProvider>();
builder.Services.AddHealthChecks().AddCheck<StorageHealthCheck>(StorageHealthCheck.HealthCheckName);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.MapHealthChecks(HealthCheckOptionsFactory.HealthEndpoint, HealthCheckOptionsFactory.Create());
app.MapBackendEndpoints();
app.Run();
