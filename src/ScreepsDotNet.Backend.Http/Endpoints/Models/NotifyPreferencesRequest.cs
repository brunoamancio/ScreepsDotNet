namespace ScreepsDotNet.Backend.Http.Endpoints.Models;

public sealed class NotifyPreferencesRequest
{
    public bool? Disabled { get; init; }
    public bool? DisabledOnMessages { get; init; }
    public bool? SendOnline { get; init; }
    public int? Interval { get; init; }
    public int? ErrorsInterval { get; init; }
}
