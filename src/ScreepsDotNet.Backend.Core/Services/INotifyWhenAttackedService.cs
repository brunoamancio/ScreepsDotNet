namespace ScreepsDotNet.Backend.Core.Services;

public enum NotifyWhenAttackedResultStatus
{
    Success,
    StructureNotFound,
    NotOwner
}

public sealed record NotifyWhenAttackedResult(NotifyWhenAttackedResultStatus Status, string? ErrorMessage = null);

public interface INotifyWhenAttackedService
{
    Task<NotifyWhenAttackedResult> SetNotifyWhenAttackedAsync(string structureId, string userId, bool enabled, CancellationToken cancellationToken = default);
}
