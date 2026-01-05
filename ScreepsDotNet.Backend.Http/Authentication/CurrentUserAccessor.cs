namespace ScreepsDotNet.Backend.Http.Authentication;

using System;
using ScreepsDotNet.Backend.Core.Context;
using ScreepsDotNet.Backend.Core.Models;

internal sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    public UserProfile? CurrentUser { get; private set; }

    public void SetCurrentUser(UserProfile user)
        => CurrentUser = user ?? throw new ArgumentNullException(nameof(user));
}
