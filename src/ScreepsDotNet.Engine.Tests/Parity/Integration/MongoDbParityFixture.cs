namespace ScreepsDotNet.Engine.Tests.Parity.Integration;

using ScreepsDotNet.TestCommon.Containers;
using Testcontainers.MongoDb;

/// <summary>
/// Test fixture that provides MongoDB container for parity testing with Node.js harness.
/// Uses centralized <see cref="MongoDbTestContainerBuilder"/> for consistency across test projects.
/// </summary>
public sealed class MongoDbParityFixture : IAsyncLifetime
{
    private readonly MongoDbContainer _mongoContainer = MongoDbTestContainerBuilder.CreateForParityTests();

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
