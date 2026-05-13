<#
.SYNOPSIS
    Build Pollmaster.Api in LAN mode and deploy the MAUI client to a USB-connected Android phone.

.DESCRIPTION
    End-to-end developer loop for a real Android device:
      1. Kills stale Pollmaster / cmd / MSBuild / dotnet workers that hold file locks.
      2. Cleans every bin/ and obj/ under the repo.
      3. Detects the PC's LAN IPv4 address (private RFC 1918 range, status Up).
      4. Resolves adb either from PATH or from the standard Android SDK locations.
      5. Verifies that adb sees at least one authorised device in the "device" state.
      6. Optionally rewrites Pollmaster/Resources/Raw/appsettings.Android.json so
         the MAUI client points at this PC's LAN IP. Skipped when -SkipConfig is set.
      7. Builds Pollmaster.Api (Release by default), starts it in a new PowerShell
         window with the lan launch profile (http://0.0.0.0:5100).
      8. Builds and deploys the MAUI Android target via "dotnet build -t:Run".

.PARAMETER Configuration
    Build configuration. Defaults to Debug.

.PARAMETER PreferredIp
    LAN IP to embed in appsettings.Android.json. Auto-detected when not supplied.

.PARAMETER SkipConfig
    Skip rewriting appsettings.Android.json (use the value already on disk).

.PARAMETER SkipBackend
    Skip starting the backend (assume one is already running on the LAN).

.EXAMPLE
    .\dev-phone.ps1

.EXAMPLE
    .\dev-phone.ps1 -PreferredIp 192.168.1.42 -Configuration Release
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [string]$PreferredIp,

    [switch]$SkipConfig,

    [switch]$SkipBackend
)

$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if (-not $Root) {
    $Root = Split-Path -Parent $MyInvocation.MyCommand.Path
}

$ApiProjectPath  = Join-Path $Root 'Pollmaster.Api\Pollmaster.Api.csproj'
$MauiProjectPath = Join-Path $Root 'Pollmaster\Pollmaster.csproj'
$AndroidTfm      = 'net10.0-android'
$AndroidConfig   = Join-Path $Root 'Pollmaster\Resources\Raw\appsettings.Android.json'

function Write-Section {
    param([Parameter(Mandatory)][string]$Message)
    Write-Host ''
    Write-Host "== $Message" -ForegroundColor Cyan
}

function Stop-LockingProcesses {
    Write-Section 'Stopping stale processes (Pollmaster*, MSBuild, dotnet workers, cmd)'
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

function Resolve-LanAddress {
    if ($PreferredIp) {
        return $PreferredIp
    }
    $candidates = Get-NetIPAddress -AddressFamily IPv4 -PrefixOrigin Dhcp, Manual -ErrorAction SilentlyContinue |
        Where-Object { $_.AddressState -eq 'Preferred' -and $_.IPAddress -match '^(192\.168\.|10\.|172\.(1[6-9]|2\d|3[01])\.)' }
    foreach ($candidate in $candidates) {
        $adapter = Get-NetAdapter -InterfaceIndex $candidate.InterfaceIndex -ErrorAction SilentlyContinue
        if ($adapter -and $adapter.Status -eq 'Up' -and $adapter.InterfaceDescription -notmatch 'Hyper-V|Virtual|Loopback') {
            return $candidate.IPAddress
        }
    }
    throw 'Could not auto-detect a LAN IPv4 address. Pass -PreferredIp explicitly.'
}

function Update-AndroidConfig {
    param([Parameter(Mandatory)][string]$IpAddress)
    if ($SkipConfig) {
        Write-Section "Skipping appsettings.Android.json update (-SkipConfig)"
        return
    }
    Write-Section "Rewriting appsettings.Android.json to http://$IpAddress`:5100/"
    if (-not (Test-Path -LiteralPath $AndroidConfig)) {
        throw "Android config not found: $AndroidConfig"
    }
    $config = Get-Content -LiteralPath $AndroidConfig -Raw | ConvertFrom-Json
    $config.PollmasterApi.BaseAddress = "http://$IpAddress`:5100/"
    $json = $config | ConvertTo-Json -Depth 5
    Set-Content -LiteralPath $AndroidConfig -Value $json -Encoding UTF8
}

function Resolve-AdbPath {
    $cmd = Get-Command adb -ErrorAction SilentlyContinue
    if ($cmd) {
        return $cmd.Source
    }
    $candidates = @(
        "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe",
        "$env:ProgramFiles\Android\android-sdk\platform-tools\adb.exe",
        "${env:ProgramFiles(x86)}\Android\android-sdk\platform-tools\adb.exe",
        "$env:USERPROFILE\AppData\Local\Android\Sdk\platform-tools\adb.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path -LiteralPath $path) {
            return $path
        }
    }
    throw 'adb.exe was not found. Install Android SDK Platform-Tools or add adb to PATH.'
}

function Assert-DeviceConnected {
    param([Parameter(Mandatory)][string]$AdbPath)
    Write-Section 'Looking for an authorised Android device'
    $output = & $AdbPath devices
    Write-Host ($output -join [Environment]::NewLine) -ForegroundColor DarkGray
    $deviceLines = $output | Select-Object -Skip 1 |
        Where-Object { $_ -match '\t(device)$' }
    if (-not $deviceLines) {
        throw 'No authorised device. Enable USB debugging, plug the phone in and accept the RSA prompt.'
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
    Write-Section 'Launching backend in LAN mode (http://0.0.0.0:5100)'
    $shellPath = Get-PreferredShell
    $command = "`$Host.UI.RawUI.WindowTitle = 'Pollmaster.Api (lan)'; Set-Location -LiteralPath '$Root'; dotnet run --project '$ApiProjectPath' --no-build --launch-profile lan --configuration $Configuration"
    Start-Process -FilePath $shellPath -ArgumentList @('-NoExit', '-NoProfile', '-Command', $command) | Out-Null
}

function Deploy-Android {
    Write-Section 'Building + deploying MAUI client to the connected phone'
    & dotnet build $MauiProjectPath '-t:Run' '--framework' $AndroidTfm '--configuration' $Configuration '--nologo'
    if ($LASTEXITCODE -ne 0) {
        throw "MAUI Android deploy failed (exit $LASTEXITCODE)."
    }
}

Stop-LockingProcesses
Remove-BuildArtefacts

$lanIp = Resolve-LanAddress
Write-Host "Detected LAN IP: $lanIp" -ForegroundColor Green

Update-AndroidConfig -IpAddress $lanIp

$adb = Resolve-AdbPath
Write-Host "Using adb: $adb" -ForegroundColor Green
Assert-DeviceConnected -AdbPath $adb

Invoke-DotnetBuild -Project $ApiProjectPath
Invoke-DotnetBuild -Project $MauiProjectPath -Framework $AndroidTfm

if (-not $SkipBackend) {
    Start-BackendWindow
    Start-Sleep -Seconds 3
}

Deploy-Android

Write-Host ''
Write-Host "Backend on http://$lanIp`:5100  |  Health: http://$lanIp`:5100/healthz" -ForegroundColor Green
Write-Host 'Pollmaster should be opening on your phone now. Allow incoming traffic on Windows Firewall if prompted.' -ForegroundColor Green
