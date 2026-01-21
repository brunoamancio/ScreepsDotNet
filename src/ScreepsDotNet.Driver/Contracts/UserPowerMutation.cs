namespace ScreepsDotNet.Driver.Contracts;

/// <summary>
/// Represents a mutation to change a user's global power balance.
/// </summary>
/// <param name="UserId">The user ID whose power balance should be changed.</param>
/// <param name="PowerChange">The amount to change the power balance (positive = increment, negative = decrement).</param>
public sealed record UserPowerMutation(string UserId, double PowerChange);
