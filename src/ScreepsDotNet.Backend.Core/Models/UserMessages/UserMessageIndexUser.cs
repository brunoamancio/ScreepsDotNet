namespace ScreepsDotNet.Backend.Core.Models.UserMessages;

using System.Collections.Generic;

public sealed record UserMessageIndexUser(string Id,
                                          string? Username,
                                          IReadOnlyDictionary<string, object?>? Badge);
