namespace ScreepsDotNet.Driver.Services.History;

public sealed class RoomHistoryUploadOptions
{
    public string BasePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "history");
}
