namespace ScreepsDotNet.Backend.Cli.Infrastructure;

internal interface IConfirmationSettings
{
    string RequiredConfirmationToken { get; }

    string? ConfirmationValue { get; }

    string ConfirmationHelpText { get; }
}
