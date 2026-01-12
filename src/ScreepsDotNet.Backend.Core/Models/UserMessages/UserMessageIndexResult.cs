namespace ScreepsDotNet.Backend.Core.Models.UserMessages;

using System.Collections.Generic;

public sealed record UserMessageIndexResult(IReadOnlyList<UserMessageIndexEntry> Messages,
                                            IReadOnlyDictionary<string, UserMessageIndexUser> Users);
