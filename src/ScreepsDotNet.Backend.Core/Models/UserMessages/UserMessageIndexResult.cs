namespace ScreepsDotNet.Backend.Core.Models.UserMessages;

public sealed record UserMessageIndexResult(IReadOnlyList<UserMessageIndexEntry> Messages,
                                            IReadOnlyDictionary<string, UserMessageIndexUser> Users);
