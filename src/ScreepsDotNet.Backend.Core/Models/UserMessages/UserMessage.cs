namespace ScreepsDotNet.Backend.Core.Models.UserMessages;

using System;

public sealed record UserMessage(
    string Id,
    string UserId,
    string RespondentId,
    DateTime Date,
    string Type,
    string Text,
    bool Unread,
    string? OutMessageId);
