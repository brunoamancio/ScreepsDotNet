namespace ScreepsDotNet.Backend.Core.Models.UserMessages;

public sealed record UserMessage(
    string Id,
    string UserId,
    string RespondentId,
    DateTime Date,
    string Type,
    string Text,
    bool Unread,
    string? OutMessageId);
