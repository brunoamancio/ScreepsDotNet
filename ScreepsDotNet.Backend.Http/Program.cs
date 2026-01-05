using ScreepsDotNet.Backend.Core.Configuration;
using ScreepsDotNet.Backend.Core.Services;
using ScreepsDotNet.Backend.Http.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.Configure<ServerInfoOptions>(builder.Configuration.GetSection(ServerInfoOptions.SectionName));
builder.Services.AddSingleton<IServerInfoProvider, InMemoryServerInfoProvider>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseHttpsRedirection();
app.MapBackendEndpoints();
app.Run();
