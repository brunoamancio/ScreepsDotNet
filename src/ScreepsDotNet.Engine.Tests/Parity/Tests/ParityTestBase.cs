namespace ScreepsDotNet.Engine.Tests.Parity.Tests;

using MongoDB.Driver;
using ScreepsDotNet.Engine.Tests.Parity.Integration;

/// <summary>
/// Base class for parity tests that provides MongoDB cleanup after each test.
/// xUnit creates a new instance of test classes for EACH test method, so DisposeAsync
/// runs after every single test, ensuring clean database state and preventing duplicate key errors.
/// </summary>
public abstract class ParityTestBase(MongoDbParityFixture mongoFixture) : IAsyncLifetime
{
    private const string ParityDatabaseName = "screeps-parity-test";

    protected MongoDbParityFixture MongoFixture { get; } = mongoFixture;

    // IAsyncLifetime implementation
    // InitializeAsync runs before each test, DisposeAsync runs after each test
    public virtual ValueTask InitializeAsync()
        => ValueTask.CompletedTask; // Default: no setup needed per test

    public virtual async ValueTask DisposeAsync()
    {
        // Clean up database after EACH test to prevent duplicate key errors
        // This runs even if the test fails (xUnit guarantees cleanup)
        try {
            var client = new MongoClient(MongoFixture.ConnectionString);
            await client.DropDatabaseAsync(ParityDatabaseName);
            Console.WriteLine($"[ParityTestCleanup] Dropped database: {ParityDatabaseName}");
        }
        catch (Exception ex) {
            // Log but don't fail the test if cleanup fails
            Console.WriteLine($"[ParityTestCleanup] Warning: Database cleanup failed: {ex.Message}");
        }

        GC.SuppressFinalize(this);
    }
}
