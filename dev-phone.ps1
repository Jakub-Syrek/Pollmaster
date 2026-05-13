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

    [switch]$SkipBackend,

    # First-time wireless pairing target shown on the phone under Developer Options →
    # Wireless debugging → Pair device with pairing code. Format: 192.168.0.123:41123.
    [string]$PairWith,

    # Six-digit code displayed alongside $PairWith on the phone screen.
    [string]$PairCode,

    # Already-paired wireless target the script should adb-connect to before deploy.
    # Use the IP and port shown under Wireless debugging (NOT the pairing port).
    # Format: 192.168.0.123:5555.
    [string]$Connect
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
        Write-Section 'Skipping appsettings.Android.json update (-SkipConfig)'
        return
    }
    Write-Section "Rewriting appsettings.Android.json to http://$IpAddress`:5100/"
    if (-not (Test-Path -LiteralPath $AndroidConfig)) {
        throw "Android config not found: $AndroidConfig"
    }

    # Targeted regex substitution preserves the original indentation, the inline _comment
    # block and avoids the ConvertTo-Json round-trip that reformats and escapes the file.
    $content = Get-Content -LiteralPath $AndroidConfig -Raw
    $pattern = '"BaseAddress"\s*:\s*"[^"]*"'
    if (-not [regex]::IsMatch($content, $pattern)) {
        throw "Did not find a BaseAddress entry to update in $AndroidConfig."
    }
    $newBaseAddress = "http://$IpAddress`:5100/"
    $replacement = '"BaseAddress": "' + $newBaseAddress + '"'
    $updated = [regex]::Replace($content, $pattern, $replacement)
    if ($updated -eq $content) {
        Write-Host '   BaseAddress already set, no rewrite needed.' -ForegroundColor DarkGray
        return
    }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($AndroidConfig, $updated, $utf8NoBom)
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

function Invoke-AdbQuietly {
    <#
    .DESCRIPTION
    Calls adb with native-command error promotion temporarily disabled. adb routinely
    writes informational lines ("* daemon not running; starting now ...") to stderr, and
    PowerShell 7 with $ErrorActionPreference = 'Stop' would otherwise throw on them.
    #>
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [Parameter(Mandatory)][string[]]$Arguments
    )
    $previousPolicy = $ErrorActionPreference
    $previousNative = $null
    $nativeFlagExists = Test-Path Variable:\PSNativeCommandUseErrorActionPreference
    if ($nativeFlagExists) {
        $previousNative = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
    }
    $ErrorActionPreference = 'Continue'
    try {
        return & $AdbPath @Arguments 2>&1
    }
    finally {
        $ErrorActionPreference = $previousPolicy
        if ($nativeFlagExists) {
            $PSNativeCommandUseErrorActionPreference = $previousNative
        }
    }
}

function Invoke-AdbPair {
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [Parameter(Mandatory)][string]$Target,
        [Parameter(Mandatory)][string]$Code
    )
    Write-Section "Pairing with $Target"
    # adb pair reads the six-digit code from stdin; pipe it in so the script stays headless.
    $previousPolicy = $ErrorActionPreference
    $previousNative = $null
    $nativeFlagExists = Test-Path Variable:\PSNativeCommandUseErrorActionPreference
    if ($nativeFlagExists) {
        $previousNative = $PSNativeCommandUseErrorActionPreference
        $PSNativeCommandUseErrorActionPreference = $false
    }
    $ErrorActionPreference = 'Continue'
    try {
        $output = $Code | & $AdbPath pair $Target 2>&1
    }
    finally {
        $ErrorActionPreference = $previousPolicy
        if ($nativeFlagExists) {
            $PSNativeCommandUseErrorActionPreference = $previousNative
        }
    }
    $lines = @($output | ForEach-Object { $_.ToString() })
    Write-Host ($lines -join [Environment]::NewLine) -ForegroundColor DarkGray
    if (-not ($lines -match 'Successfully paired')) {
        throw "adb pair $Target failed. Re-open Wireless debugging on the phone to get a fresh code and port."
    }
}

function Invoke-AdbConnect {
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [Parameter(Mandatory)][string]$Target
    )
    Write-Section "Connecting wireless adb to $Target"
    $output = Invoke-AdbQuietly -AdbPath $AdbPath -Arguments @('connect', $Target)
    $lines = @($output | ForEach-Object { $_.ToString() })
    Write-Host ($lines -join [Environment]::NewLine) -ForegroundColor DarkGray
    if ($lines -match 'failed to connect|cannot connect') {
        throw "adb connect $Target failed. Verify the IP/port shown under Wireless debugging."
    }
}

function Get-OnlineDeviceLines {
    param([Parameter(Mandatory)][string]$AdbPath)
    $output = Invoke-AdbQuietly -AdbPath $AdbPath -Arguments @('devices')
    $lines = @($output | ForEach-Object { $_.ToString() })
    return @($lines | Select-Object -Skip 1 | Where-Object { $_ -match '\t(device)$' })
}

function Find-WirelessAdbTarget {
    <#
    .DESCRIPTION
    Probes adb's mDNS browser for paired Wireless-debugging devices and returns the first
    "<ip>:<port>" found under the _adb-tls-connect._tcp service type. Returns $null when
    mDNS discovery is disabled, blocked by the network, or no paired phone is broadcasting.
    #>
    param([Parameter(Mandatory)][string]$AdbPath)
    $output = Invoke-AdbQuietly -AdbPath $AdbPath -Arguments @('mdns', 'services')
    $lines = @($output | ForEach-Object { $_.ToString() })
    foreach ($line in $lines) {
        if ($line -match '_adb-tls-connect\._tcp\s+(\d{1,3}(?:\.\d{1,3}){3}):(\d{1,5})') {
            return "$($matches[1]):$($matches[2])"
        }
    }
    return $null
}

function Assert-DeviceConnected {
    <#
    .DESCRIPTION
    Ensures adb sees at least one authorised device. When the caller did not pass an
    explicit -PairWith / -Connect target and the device list is empty we fall back to
    mDNS discovery so a previously paired phone reconnects on its own.
    #>
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [bool]$AllowAutoConnect = $true
    )
    Write-Section 'Looking for an authorised Android device'

    # Pre-warm the adb daemon so its first-run startup messages do not interleave with
    # the "adb devices" output we actually want to parse.
    Invoke-AdbQuietly -AdbPath $AdbPath -Arguments @('start-server') | Out-Null

    $deviceLines = Get-OnlineDeviceLines -AdbPath $AdbPath
    if ($deviceLines) {
        Write-Host ($deviceLines -join [Environment]::NewLine) -ForegroundColor DarkGray
        return
    }

    if ($AllowAutoConnect) {
        Write-Host '   no device online, scanning mDNS for a paired phone...' -ForegroundColor DarkGray
        $discovered = Find-WirelessAdbTarget -AdbPath $AdbPath
        if ($discovered) {
            Write-Host "   discovered $discovered via mDNS, connecting..." -ForegroundColor DarkGray
            Invoke-AdbConnect -AdbPath $AdbPath -Target $discovered
            $deviceLines = Get-OnlineDeviceLines -AdbPath $AdbPath
            if ($deviceLines) {
                Write-Host ($deviceLines -join [Environment]::NewLine) -ForegroundColor DarkGray
                return
            }
        }
    }

    throw 'No authorised device. Plug the phone in via USB, or enable Wireless debugging and pass -PairWith / -Connect.'
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

if ($PairWith) {
    if (-not $PairCode) {
        throw '-PairWith requires -PairCode (the six-digit code shown on the phone).'
    }
    Invoke-AdbPair -AdbPath $adb -Target $PairWith -Code $PairCode
}

if ($Connect) {
    Invoke-AdbConnect -AdbPath $adb -Target $Connect
}

# Auto-discover only when the caller did not already point us at a specific target —
# otherwise an explicit -Connect that just failed would be silently shadowed by mDNS.
$allowAutoConnect = -not $Connect -and -not $PairWith
Assert-DeviceConnected -AdbPath $adb -AllowAutoConnect $allowAutoConnect

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
