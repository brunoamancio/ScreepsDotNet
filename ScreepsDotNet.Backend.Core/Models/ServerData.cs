namespace ScreepsDotNet.Backend.Core.Models;

public sealed record ServerData(string WelcomeText, IReadOnlyDictionary<string, object> CustomObjectTypes,
                                int HistoryChunkSize, int SocketUpdateThrottle, RendererData Renderer);

public sealed record RendererData(IReadOnlyDictionary<string, object> Resources, IReadOnlyDictionary<string, object> Metadata);
