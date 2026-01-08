namespace ScreepsDotNet.Backend.Core.Context;

using ScreepsDotNet.Backend.Core.Models;

public interface ICurrentUserAccessor
{
    UserProfile? CurrentUser { get; }

    void SetCurrentUser(UserProfile user);
}
