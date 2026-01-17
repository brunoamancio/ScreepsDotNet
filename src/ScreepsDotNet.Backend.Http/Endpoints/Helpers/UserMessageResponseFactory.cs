namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Collections.Generic;
using ScreepsDotNet.Backend.Core.Models.UserMessages;
using ScreepsDotNet.Backend.Http.Endpoints.Models;

internal static class UserMessageResponseFactory
{
    public static IReadOnlyList<UserMessageResponse> CreateList(IReadOnlyList<UserMessage> messages)
    {
        var results = new List<UserMessageResponse>(messages.Count);
        foreach (var message in messages)
            results.Add(Create(message));

        return results;
    }

    public static UserMessageIndexResponse CreateIndex(UserMessageIndexResult result)
    {
        var entries = new List<UserMessageIndexEntryResponse>(result.Messages.Count);
        foreach (var entry in result.Messages) {
            var response = new UserMessageIndexEntryResponse(entry.Id, Create(entry.Message));
            entries.Add(response);
        }

        var users = new Dictionary<string, UserMessageIndexUserResponse>(result.Users.Count);
        foreach (var (userId, user) in result.Users)
            users[userId] = new UserMessageIndexUserResponse(user.Id, user.Username, user.Badge);

        return new UserMessageIndexResponse(entries, users);
    }

    private static UserMessageResponse Create(UserMessage message)
        => new(message.Id,
                message.UserId,
                message.RespondentId,
                message.Date,
                message.Type,
                message.Text,
                message.Unread,
                message.OutMessageId);
}
