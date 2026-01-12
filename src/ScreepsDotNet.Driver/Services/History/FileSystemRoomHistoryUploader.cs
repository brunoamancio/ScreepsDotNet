using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.History;

namespace ScreepsDotNet.Driver.Services.History;

internal sealed class FileSystemRoomHistoryUploader(IOptions<RoomHistoryUploadOptions> options, ILogger<FileSystemRoomHistoryUploader>? logger = null)
    : IRoomHistoryUploader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly RoomHistoryUploadOptions _options = options.Value;

    public async Task UploadAsync(RoomHistoryChunk chunk, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        if (string.IsNullOrWhiteSpace(chunk.Room))
            return;

        var directory = Path.Combine(_options.BasePath, chunk.Room);
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"{chunk.BaseTick}.json");
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, chunk, JsonOptions, token).ConfigureAwait(false);
        logger?.LogDebug("Saved room history chunk for {Room} baseTick {BaseTick} to {Path}.", chunk.Room, chunk.BaseTick, filePath);
    }
}
