namespace ScreepsDotNet.Engine.Validation.Models;

/// <summary>
/// Result of intent validation.
/// For Node.js parity, validation failures are silent (no error messages to user).
/// </summary>
public sealed record ValidationResult
{
    public static readonly ValidationResult Success = new(true, null);

    public bool IsValid { get; init; }
    public ValidationErrorCode? ErrorCode { get; init; }

    private ValidationResult(bool isValid, ValidationErrorCode? errorCode)
    {
        IsValid = isValid;
        ErrorCode = errorCode;
    }

    public static ValidationResult Failure(ValidationErrorCode errorCode)
        => new(false, errorCode);
}
