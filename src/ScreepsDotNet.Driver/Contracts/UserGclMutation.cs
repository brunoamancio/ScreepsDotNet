namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Represents a mutation to increment a user's GCL (Global Control Level) progress.
/// </summary>
/// <param name="UserId">The user ID whose GCL should be incremented.</param>
/// <param name="GclIncrement">The amount to increment the GCL progress by (always positive).</param>
public sealed record UserGclMutation(string UserId, int GclIncrement);
