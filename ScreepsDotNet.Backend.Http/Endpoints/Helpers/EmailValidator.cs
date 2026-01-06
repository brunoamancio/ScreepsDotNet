namespace ScreepsDotNet.Backend.Http.Endpoints.Helpers;

using System.Text.RegularExpressions;

internal static partial class EmailValidator
{
    public const string InvalidEmailMessage = "invalid email";

    private static readonly Regex EmailRegex = BuildEmailRegex();

    public static bool IsValid(string? email)
        => !string.IsNullOrWhiteSpace(email) && EmailRegex.IsMatch(email);

    [GeneratedRegex(@"^[\w\d\-.\+&]+\@[\w\d\-\.&]+\.[\w\d\-\.&]{2,}$", RegexOptions.IgnoreCase)]
    private static partial Regex BuildEmailRegex();
}
