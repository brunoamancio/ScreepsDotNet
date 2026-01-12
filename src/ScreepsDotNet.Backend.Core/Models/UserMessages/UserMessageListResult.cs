namespace ScreepsDotNet.Backend.Core.Models.UserMessages;

using System.Collections.Generic;

public sealed record UserMessageListResult(IReadOnlyList<UserMessage> Messages);
