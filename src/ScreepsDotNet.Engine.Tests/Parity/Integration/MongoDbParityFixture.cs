namespace ScreepsDotNet.Engine.Tests.Parity.Integration;

using Testcontainers.MongoDb;

/// <summary>
/// Test fixture that provides MongoDB container for parity testing with Node.js harness.
/// Uses Testcontainers to automatically start/stop MongoDB.
/// </summary>
public sealed class MongoDbParityFixture : IAsyncLifetime
{
    private const string MongoImage = "mongo:7.0";
    private const int MongoPort = 27017;

    private readonly MongoDbContainer _mongoContainer = new MongoDbBuilder(MongoImage)
        .WithPortBinding(MongoPort, MongoPort) // Fixed port so Node.js harness can connect
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        ConnectionString = _mongoContainer.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
        => await _mongoContainer.DisposeAsync();
}

/// <summary>
/// Test collection for MongoDB-dependent parity tests.
/// All tests in this collection share the same MongoDB container and prerequisites fixture.
/// </summary>
#pragma warning disable CA1711 // Identifiers should not have incorrect suffix
[CollectionDefinition(nameof(MongoDbParityCollection))]
public sealed class MongoDbParityCollection : ICollectionFixture<MongoDbParityFixture>, ICollectionFixture<ParityTestPrerequisites>
#pragma warning restore CA1711
{
    // This class is never instantiated - it's just a marker for xUnit
}
