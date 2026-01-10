namespace ScreepsDotNet.Backend.Cli.Tests.Formatting;

using System;
using ScreepsDotNet.Backend.Cli.Formatting;

public sealed class FormattableCommandSettingsTests
{
    [Fact]
    public void PreferredOutputFormat_UsesEnvironmentVariableWhenOptionMissing()
    {
        var original = Environment.GetEnvironmentVariable(FormattableCommandSettings.FormatEnvironmentVariableName);
        try
        {
            Environment.SetEnvironmentVariable(FormattableCommandSettings.FormatEnvironmentVariableName, "markdown");
            var settings = new TestSettings();

            Assert.Equal(OutputFormat.Markdown, settings.PreferredOutputFormat);
            Assert.True(settings.Validate().Successful);
        }
        finally
        {
            Environment.SetEnvironmentVariable(FormattableCommandSettings.FormatEnvironmentVariableName, original);
        }
    }

    private sealed class TestSettings : FormattableCommandSettings;
}
