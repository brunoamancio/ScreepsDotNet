namespace ScreepsDotNet.TestCommon.Containers;

using DotNet.Testcontainers.Builders;
using Testcontainers.MongoDb;

/// <summary>
/// Centralized MongoDB test container builder with common configuration options.
/// Used across integration tests, driver tests, and parity tests.
/// </summary>
public static class MongoDbTestContainerBuilder
{
    private const string DefaultMongoImage = "mongo:7.0";
    private const int DefaultMongoPort = 27017;

    /// <summary>
    /// Creates a standard MongoDB container for integration tests.
    /// Uses default authentication and dynamic port binding.
    /// </summary>
    public static MongoDbContainer CreateDefault(string? image = null)
    {
        var mongoImage = image ?? DefaultMongoImage;
        return new MongoDbBuilder(mongoImage).Build();
    }

    /// <summary>
    /// Creates a MongoDB container for parity tests with Node.js harness.
    /// Uses dynamic port binding that external Node.js process can access.
    /// IMPORTANT: Use container.GetConnectionString() to get proper connection string.
    /// </summary>
    public static MongoDbContainer CreateForParityTests(string? image = null)
    {
        var mongoImage = image ?? DefaultMongoImage;

        // Use MongoDbBuilder defaults (handles authentication, port binding, wait strategy)
        // MongoDbBuilder automatically:
        // - Binds port 27017 to random host port
        // - Sets up authentication (username: mongo, password: mongo)
        // - Configures wait strategy for "Waiting for connections"
        return new MongoDbBuilder(mongoImage).Build();
    }

    /// <summary>
    /// Creates a MongoDB container with custom configuration.
    /// </summary>
    public static MongoDbContainer CreateCustom(
        string? image = null,
        int? hostPort = null,
        bool disableAuth = false,
        string? waitLogMessage = null)
    {
        var mongoImage = image ?? DefaultMongoImage;
        var builder = new MongoDbBuilder(mongoImage);

        if (hostPort.HasValue)
            builder = builder.WithPortBinding(hostPort.Value, DefaultMongoPort);

        if (disableAuth)
            builder = builder.WithCommand("mongod", "--noauth");

        if (!string.IsNullOrEmpty(waitLogMessage))
            builder = builder.WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(waitLogMessage));

        return builder.Build();
    }
}
