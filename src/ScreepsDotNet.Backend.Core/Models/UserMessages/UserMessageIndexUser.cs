namespace ScreepsDotNet.Backend.Core.Models.UserMessages;

public sealed record UserMessageIndexUser(string Id,
                                          string? Username,
                                          IReadOnlyDictionary<string, object?>? Badge);
