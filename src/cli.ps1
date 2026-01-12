param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$CliArgs
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir 'ScreepsDotNet.Backend.Cli/ScreepsDotNet.Backend.Cli.csproj'

if ($null -eq $CliArgs) {
    $CliArgs = @()
}

dotnet run --project $projectPath -- @CliArgs
