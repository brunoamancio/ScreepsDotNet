namespace ScreepsDotNet.Backend.Core.Models;

public enum SetUsernameResult
{
    Success,
    UserNotFound,
    UsernameAlreadySet,
    UsernameExists,
    Failed
}
