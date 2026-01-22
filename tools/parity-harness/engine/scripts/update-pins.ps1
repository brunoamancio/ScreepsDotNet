#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Updates Screeps repository commit pins to latest versions.

.DESCRIPTION
    Fetches latest commit hashes from official Screeps GitHub repositories
    and updates versions.json with new pins. Prompts before making changes.

.EXAMPLE
    pwsh update-pins.ps1

.NOTES
    Windows PowerShell version of update-pins.sh
    Requires: git, PowerShell 7.0+
#>

$ErrorActionPreference = 'Stop'

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Updating Screeps repository commit pins"
Write-Host "=========================================" -ForegroundColor Cyan

# Get script directory and navigate to parity-harness root
$scriptDir = $PSScriptRoot
Set-Location (Join-Path $scriptDir ".." "..")

$versionsFile = "versions.json"

if (-not (Test-Path $versionsFile)) {
    Write-Error "ERROR: versions.json not found!"
    exit 1
}

Write-Host ""
Write-Host "Fetching latest commit hashes from GitHub..."
Write-Host ""

# Fetch latest commit hashes
$engineHash = (git ls-remote https://github.com/screeps/engine.git HEAD).Split()[0]
$driverHash = (git ls-remote https://github.com/screeps/driver.git HEAD).Split()[0]
$commonHash = (git ls-remote https://github.com/screeps/common.git HEAD).Split()[0]

# Get current hashes
$versionsConfig = Get-Content $versionsFile -Raw | ConvertFrom-Json
$currentEngine = $versionsConfig.engine.pins.engine
$currentDriver = $versionsConfig.engine.pins.driver
$currentCommon = $versionsConfig.engine.pins.common

Write-Host "Current pins:" -ForegroundColor Yellow
Write-Host "  engine: $currentEngine"
Write-Host "  driver: $currentDriver"
Write-Host "  common: $currentCommon"
Write-Host ""

Write-Host "Latest commits:" -ForegroundColor Green
Write-Host "  engine: $engineHash"
Write-Host "  driver: $driverHash"
Write-Host "  common: $commonHash"
Write-Host ""

# Check if there are changes
$changes = $false
if ($engineHash -ne $currentEngine) {
    Write-Host "✓ engine has updates" -ForegroundColor Green
    $changes = $true
}
if ($driverHash -ne $currentDriver) {
    Write-Host "✓ driver has updates" -ForegroundColor Green
    $changes = $true
}
if ($commonHash -ne $currentCommon) {
    Write-Host "✓ common has updates" -ForegroundColor Green
    $changes = $true
}

if (-not $changes) {
    Write-Host "No updates available - already at latest commits" -ForegroundColor Cyan
    exit 0
}

Write-Host ""
$response = Read-Host "Update versions.json with latest commits? [y/N]"
if ($response -notmatch '^[Yy]$') {
    Write-Host "Aborted"
    exit 0
}

# Update versions.json
$today = Get-Date -Format "yyyy-MM-dd"
$versionsConfig.engine.pins.engine = $engineHash
$versionsConfig.engine.pins.driver = $driverHash
$versionsConfig.engine.pins.common = $commonHash
$versionsConfig.engine.lastValidated = $today

$versionsConfig | ConvertTo-Json -Depth 10 | Set-Content $versionsFile

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "✓ Updated versions.json" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:"
Write-Host "1. Run parity tests: dotnet test --filter Category=Parity"
Write-Host "2. If tests pass, commit versions.json"
Write-Host "3. If divergences found, fix .NET Engine, then commit"
Write-Host ""
Write-Host "Commands:"
Write-Host "  cd ../../.."
Write-Host "  dotnet test --filter Category=Parity"
Write-Host "  git add tools/parity-harness/versions.json"
Write-Host '  git commit -m "chore: update Screeps repo pins to latest"'
Write-Host ""
