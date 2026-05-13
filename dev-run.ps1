<#
.SYNOPSIS
    Clean + build both Pollmaster projects, then launch them in dedicated windows.

.DESCRIPTION
    Pollmaster developer loop:
      1. Stops stale cmd.exe, Pollmaster, Pollmaster.Api, MSBuild and dotnet
         worker processes that may hold file locks on bin/obj.
      2. Removes every bin/ and obj/ directory under the repo (.git is skipped).
      3. Builds Pollmaster.Api, then Pollmaster (MAUI, Windows target framework).
      4. Launches the backend in a new PowerShell console window so its logs
         stay readable, then starts the MAUI Windows binary in its own GUI
         window.

.PARAMETER Configuration
    Build configuration. Defaults to Debug.

.PARAMETER BackendProfile
    launchSettings.json profile for the backend. Defaults to https.

.EXAMPLE
    .\dev-run.ps1

.EXAMPLE
    .\dev-run.ps1 -Configuration Release -BackendProfile lan
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [ValidateSet('http', 'https', 'lan')]
    [string]$BackendProfile = 'https'
)

$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if (-not $Root) {
    $Root = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$ApiProjectPath  = Join-Path $Root 'Pollmaster.Api\Pollmaster.Api.csproj'
$MauiProjectPath = Join-Path $Root 'Pollmaster\Pollmaster.csproj'
$WindowsTfm      = 'net10.0-windows10.0.19041.0'
$MauiBinaryPath  = Join-Path $Root "Pollmaster\bin\$Configuration\$WindowsTfm\win-x64\Pollmaster.exe"

function Write-Section {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host ''
    Write-Host "== $Message" -ForegroundColor Cyan
}

function Stop-LockingProcesses {
    Write-Section 'Stopping stale processes (cmd, Pollmaster*, MSBuild, dotnet workers)'
    $names = @('Pollmaster', 'Pollmaster.Api', 'cmd', 'MSBuild', 'VBCSCompiler')
    foreach ($name in $names) {
        Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
            Write-Host "   killing $($_.ProcessName) (PID $($_.Id))" -ForegroundColor DarkGray
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    }
    Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue |
        Where-Object { $_.Id -ne $PID } |
        ForEach-Object {
            Write-Host "   killing dotnet (PID $($_.Id))" -ForegroundColor DarkGray
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
        }
    Start-Sleep -Milliseconds 500
}

function Remove-BuildArtefacts {
    Write-Section 'Removing bin/ and obj/ directories'
    $targets = Get-ChildItem -Path $Root -Recurse -Force -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -in @('bin', 'obj') -and $_.FullName -notmatch '\\\.git\\' }
    foreach ($dir in $targets) {
        $relative = $dir.FullName.Substring($Root.Length + 1)
        Write-Host "   removing $relative" -ForegroundColor DarkGray
        Remove-Item -LiteralPath $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Invoke-DotnetBuild {
    param(
        [Parameter(Mandatory)][string]$Project,
        [string]$Framework
    )
    Write-Section "Building $(Split-Path -Leaf $Project) ($Configuration)"
    $arguments = @('build', $Project, '--configuration', $Configuration, '--nologo', '--verbosity', 'minimal')
    if ($Framework) {
        $arguments += @('--framework', $Framework)
    }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed for $Project (exit $LASTEXITCODE)."
    }
}

function Get-PreferredShell {
    $pwshCommand = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($pwshCommand) {
        return $pwshCommand.Source
    }
    return 'powershell.exe'
}

function Start-BackendWindow {
    Write-Section "Launching backend (profile: $BackendProfile)"
    $shellPath = Get-PreferredShell
    $command = "`$Host.UI.RawUI.WindowTitle = 'Pollmaster.Api'; Set-Location -LiteralPath '$Root'; dotnet run --project '$ApiProjectPath' --no-build --launch-profile $BackendProfile --configuration $Configuration"
    Start-Process -FilePath $shellPath -ArgumentList @('-NoExit', '-NoProfile', '-Command', $command) | Out-Null
}

function Start-ClientWindow {
    Write-Section 'Launching MAUI Windows client'
    if (-not (Test-Path -LiteralPath $MauiBinaryPath)) {
        throw "MAUI Windows binary not found: $MauiBinaryPath"
    }
    Start-Process -FilePath $MauiBinaryPath -WorkingDirectory (Split-Path -Parent $MauiBinaryPath) | Out-Null
}

Stop-LockingProcesses
Remove-BuildArtefacts
Invoke-DotnetBuild -Project $ApiProjectPath
Invoke-DotnetBuild -Project $MauiProjectPath -Framework $WindowsTfm
Start-BackendWindow
Start-Sleep -Seconds 3
Start-ClientWindow

Write-Host ''
Write-Host 'Done. Backend logs are in a new PowerShell window; the MAUI client runs in its own GUI window.' -ForegroundColor Green
