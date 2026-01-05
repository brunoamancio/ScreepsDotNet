namespace ScreepsDotNet.Backend.Core.Configuration;

public sealed class ServerDataOptions
{
    public const string SectionName = "ServerData";

    public string WelcomeText { get; init; } = "Welcome to ScreepsDotNet";

    public IDictionary<string, object> CustomObjectTypes { get; init; } = new Dictionary<string, object>();

    public int HistoryChunkSize { get; init; } = 20;

    public int SocketUpdateThrottle { get; init; } = 100;

    public RendererOptions Renderer { get; init; } = new();

    public sealed class RendererOptions
    {
        public IDictionary<string, object> Resources { get; init; } = new Dictionary<string, object>();

        public IDictionary<string, object> Metadata { get; init; } = new Dictionary<string, object>();
    }
}
