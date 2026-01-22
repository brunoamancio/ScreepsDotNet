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
    /// Disables authentication and binds to fixed port 27017 (required for Node.js connection).
    /// </summary>
    public static MongoDbContainer CreateForParityTests(string? image = null, int? port = null)
    {
        var mongoImage = image ?? DefaultMongoImage;
        var mongoPort = port ?? DefaultMongoPort;

        return new MongoDbBuilder(mongoImage)
            .WithPortBinding(mongoPort, mongoPort) // Fixed port for Node.js harness
            .WithCommand("mongod", "--noauth") // Disable authentication
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Waiting for connections"))
            .Build();
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
