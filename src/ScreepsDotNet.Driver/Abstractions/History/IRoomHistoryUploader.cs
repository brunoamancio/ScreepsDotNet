namespace ScreepsDotNet.Driver.Abstractions.History;

public interface IRoomHistoryUploader
{
    Task UploadAsync(RoomHistoryChunk chunk, CancellationToken token = default);
}
