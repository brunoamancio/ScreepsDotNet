using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ScreepsDotNet.Driver.Abstractions.History;
using ScreepsDotNet.Driver.Services.History;

namespace ScreepsDotNet.Driver.Tests.History;

public sealed class FileSystemRoomHistoryUploaderTests
{
    [Fact]
    public async Task UploadAsync_WritesChunkToDisk()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var options = Options.Create(new RoomHistoryUploadOptions { BasePath = tempDir });
        var uploader = new FileSystemRoomHistoryUploader(options, NullLogger<FileSystemRoomHistoryUploader>.Instance);
        var chunk = new RoomHistoryChunk("W1N1", 12345, DateTimeOffset.UtcNow, new Dictionary<int, JsonNode?>
        {
            [12345] = JsonNode.Parse("{\"energy\":100}")
        });

        await uploader.UploadAsync(chunk);

        var filePath = Path.Combine(tempDir, "W1N1", "12345.json");
        Assert.True(File.Exists(filePath));
        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("\"Room\":\"W1N1\"", content);
        Directory.Delete(tempDir, recursive: true);
    }
}
