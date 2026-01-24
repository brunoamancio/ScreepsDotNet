#!/usr/bin/env pwsh
#Requires -Version 7.0

<#
.SYNOPSIS
    Clones official Screeps repositories for Engine parity testing.

.DESCRIPTION
    This script clones or updates the official Screeps repositories (engine, driver, common)
    from GitHub and installs their npm dependencies. Supports version pinning via versions.json.

.EXAMPLE
    pwsh clone-repos.ps1

.NOTES
    Windows PowerShell version of clone-repos.sh
    Requires: Git, npm, PowerShell 7.0+
#>

$ErrorActionPreference = 'Stop'

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "Cloning official Screeps repositories..."
Write-Host "  (for Engine parity harness)"
Write-Host "=========================================" -ForegroundColor Cyan

# Get script directory and navigate to engine harness root
$scriptDir = $PSScriptRoot
Set-Location (Join-Path $scriptDir "..")

# Load version configuration (from parent parity-harness directory)
$versionsFile = "../versions.json"
if (-not (Test-Path $versionsFile)) {
    Write-Error "ERROR: versions.json not found at $versionsFile!"
    exit 1
}

# Parse versions.json (PowerShell has built-in JSON parsing, no jq needed)
try {
    $versionsConfig = Get-Content $versionsFile -Raw | ConvertFrom-Json
    $pinningEnabled = $versionsConfig.engine.pinningEnabled
    $engineRef = $versionsConfig.engine.pins.engine
    $driverRef = $versionsConfig.engine.pins.driver
    $commonRef = $versionsConfig.engine.pins.common
}
catch {
    Write-Warning "Failed to parse versions.json: $_"
    Write-Host "Using default refs (master)"
    $pinningEnabled = $false
    $engineRef = "master"
    $driverRef = "master"
    $commonRef = "master"
}

if ($pinningEnabled) {
    Write-Host "Using pinned versions:" -ForegroundColor Yellow
    Write-Host "  engine: $engineRef"
    Write-Host "  driver: $driverRef"
    Write-Host "  common: $commonRef"
}
else {
    $engineRef = "master"
    $driverRef = "master"
    $commonRef = "master"
    Write-Host "Using latest versions from master branches"
}

$modulesDir = "screeps-modules"
if (-not (Test-Path $modulesDir)) {
    New-Item -ItemType Directory -Path $modulesDir | Out-Null
}

# Function to clone or update a repository
function Clone-OrUpdate {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Ref
    )

    Write-Host ""
    Write-Host "Processing $Name..." -ForegroundColor Green

    $repoPath = Join-Path $modulesDir $Name
    $gitPath = Join-Path $repoPath ".git"

    if (Test-Path $gitPath) {
        Write-Host "  Repository exists, updating..."
        Push-Location $repoPath
        try {
            git fetch origin
            git checkout $Ref
            if ($Ref -eq "master") {
                git pull origin master
            }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Host "  Cloning repository..."
        git clone $Url $repoPath
        Push-Location $repoPath
        try {
            git checkout $Ref
        }
        finally {
            Pop-Location
        }
    }

    Write-Host "  Installing npm dependencies..."
    Push-Location $repoPath
    try {
        npm install --ignore-scripts --legacy-peer-deps

        # Reset package-lock.json changes (npm install may update it)
        # We don't want to modify the official repos - only use them as-is
        if (Test-Path "package-lock.json") {
            git restore package-lock.json 2>$null
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "  ✓ $Name ready" -ForegroundColor Green
}

# Clone/update all repositories
Clone-OrUpdate -Name "engine" -Url "https://github.com/screeps/engine.git" -Ref $engineRef
Clone-OrUpdate -Name "driver" -Url "https://github.com/screeps/driver.git" -Ref $driverRef
Clone-OrUpdate -Name "common" -Url "https://github.com/screeps/common.git" -Ref $commonRef

Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "✓ All Screeps modules ready for parity testing" -ForegroundColor Green
Write-Host "=========================================" -ForegroundColor Cyan
