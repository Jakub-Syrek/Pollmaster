<#
.SYNOPSIS
    Build and deploy the MAUI client to a USB-connected or wireless Android phone
    pointing at the hosted production backend (Railway / Render / Fly / anywhere
    reachable on the public internet).

.DESCRIPTION
    Production counterpart of dev-phone.ps1. Use this when:
      - the backend is hosted off-prem (Railway by default);
      - you want the phone to work outside your LAN (mobile data, holidays, demos);
      - you do not want to start a local Pollmaster.Api at all.

    What the script does:
      1. Kills stale Pollmaster / cmd / MSBuild / dotnet workers that hold file locks.
      2. Cleans every bin/ and obj/ under the repo.
      3. Reads the production BaseAddress from
         Pollmaster\Resources\Raw\appsettings.Android.json and curls /healthz to
         fail fast if the deployment is offline (skippable with -SkipHealthcheck).
      4. Resolves adb from PATH or the standard Android SDK locations.
      5. Optionally pairs (-PairWith / -PairCode) or adb-connects (-Connect) over
         wireless. mDNS auto-discovery kicks in when neither flag is passed and
         no device is currently online.
      6. Verifies that adb sees at least one authorised device.
      7. Builds + deploys the MAUI Android target via "dotnet build -t:Run".

    Differences from dev-phone.ps1:
      - never starts a local backend (Railway hosts it);
      - never rewrites appsettings.Android.json (production URL is committed
        already - dev-phone.ps1 rewrites it on LAN runs; restore the committed
        Railway URL with `git checkout Pollmaster\Resources\Raw\appsettings.Android.json`).

.PARAMETER Configuration
    Build configuration. Defaults to Release for production-ready APKs.

.PARAMETER PairWith
    First-time wireless pairing target shown under Developer Options ->
    Wireless debugging -> Pair device with pairing code. Format: 192.168.0.123:41123.

.PARAMETER PairCode
    Six-digit code displayed alongside $PairWith on the phone screen.

.PARAMETER Connect
    Already-paired wireless target to adb-connect to before deploy. Use the IP
    and port shown under Wireless debugging (NOT the pairing port).
    Format: 192.168.0.123:5555.

.PARAMETER SkipHealthcheck
    Skip the curl /healthz preflight against the production BaseAddress. Useful
    when the backend is intentionally down (e.g. swapping deployments) and you
    only want to ship a build.

.EXAMPLE
    .\prod-phone.ps1
    # Release build, mDNS auto-discovery, /healthz preflight against Railway.

.EXAMPLE
    .\prod-phone.ps1 -Connect 192.168.0.88:34667
    # Explicit wireless target instead of mDNS discovery.

.EXAMPLE
    .\prod-phone.ps1 -PairWith 192.168.0.88:41123 -PairCode 802429
    # First-time wireless pairing with a fresh code from the phone's Wireless debugging screen.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    # First-time wireless pairing target shown on the phone under Developer Options ->
    # Wireless debugging -> Pair device with pairing code. Format: 192.168.0.123:41123.
    [string]$PairWith,

    # Six-digit code displayed alongside $PairWith on the phone screen.
    [string]$PairCode,

    # Already-paired wireless target the script should adb-connect to before deploy.
    # Use the IP and port shown under Wireless debugging (NOT the pairing port).
    # Format: 192.168.0.123:5555.
    [string]$Connect,

    # Skip the curl /healthz preflight against the production BaseAddress.
    [switch]$SkipHealthcheck
)

$ErrorActionPreference = 'Stop'

$Root = $PSScriptRoot
if (-not $Root) {
    $Root = Split-Path -Parent $MyInvocation.MyCommand.Path
}

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

function Get-ProductionBaseAddress {
    if (-not (Test-Path -LiteralPath $AndroidConfig)) {
        throw "Android config not found: $AndroidConfig"
    }
    $json = Get-Content -LiteralPath $AndroidConfig -Raw | ConvertFrom-Json
    $address = $json.PollmasterApi.BaseAddress
    if (-not $address) {
        throw "BaseAddress missing in $AndroidConfig"
    }
    if ($address -match '^http://(192\.168|10\.|172\.(1[6-9]|2\d|3[01])\.|localhost|127\.)') {
        throw "appsettings.Android.json points at a LAN address ($address). dev-phone.ps1 probably rewrote it - restore with `git checkout $AndroidConfig` before running prod-phone.ps1, or set the production URL manually."
    }
    return $address
}

function Test-BackendHealth {
    param([Parameter(Mandatory)][string]$BaseAddress)
    if ($SkipHealthcheck) {
        Write-Section 'Skipping production /healthz preflight (-SkipHealthcheck)'
        return
    }
    $healthUrl = ($BaseAddress.TrimEnd('/')) + '/healthz'
    Write-Section "Probing production backend at $healthUrl"
    try {
        $response = Invoke-RestMethod -Uri $healthUrl -TimeoutSec 15 -Method Get
        Write-Host "   /healthz -> $(($response | ConvertTo-Json -Compress))" -ForegroundColor DarkGray
    }
    catch {
        throw "Production /healthz failed: $($_.Exception.Message). Either the backend is down, or the BaseAddress in $AndroidConfig is wrong. Re-run with -SkipHealthcheck to bypass."
    }
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

function Invoke-AdbConnect {
    <#
    .DESCRIPTION
    Connects adb to the supplied Wireless-debugging target. Android frequently rotates the
    port - leaving and re-entering the Wireless debugging screen, the screen turning off,
    or doze kicking in all spawn a fresh port. We retry the connect a few times, each time
    re-scanning mDNS so a port that rolled between scan and connect is picked up.
    #>
    param(
        [Parameter(Mandatory)][string]$AdbPath,
        [Parameter(Mandatory)][string]$Target,
        [int]$MaxAttempts = 4,
        [int]$RetryDelaySeconds = 3
    )
    $currentTarget = $Target
    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        Write-Section "Connecting wireless adb to $currentTarget (attempt $attempt/$MaxAttempts)"
        $output = Invoke-AdbQuietly -AdbPath $AdbPath -Arguments @('connect', $currentTarget)
        $lines = @($output | ForEach-Object { $_.ToString() })
        Write-Host ($lines -join [Environment]::NewLine) -ForegroundColor DarkGray
        $connected = $lines | Where-Object { $_ -match '^connected to|already connected' }
        if ($connected) {
            return
        }
        if ($attempt -ge $MaxAttempts) {
            break
        }
        $refreshed = Find-WirelessAdbTarget -AdbPath $AdbPath
        if ($refreshed -and $refreshed -ne $currentTarget) {
            Write-Host "   mDNS now reports $refreshed (was $currentTarget)" -ForegroundColor DarkGray
            $currentTarget = $refreshed
        }
        Start-Sleep -Seconds $RetryDelaySeconds
    }
    throw "adb connect $Target failed after $MaxAttempts attempts. Keep the Wireless debugging screen visible on the phone (the port closes the moment the screen rotates or the dialog is dismissed), then re-run the script."
}

function Get-OnlineDeviceLines {
    param([Parameter(Mandatory)][string]$AdbPath)
    $output = Invoke-AdbQuietly -AdbPath $AdbPath -Arguments @('devices')
    $lines = @($output | ForEach-Object { $_.ToString() })
    return @($lines | Select-Object -Skip 1 | Where-Object { $_ -match '\t(device)$' })
}

function Select-PreferredSerial {
    <#
    .DESCRIPTION
    Picks exactly one adb serial out of the connected device list. Release builds go
    through bundletool, which rejects "more than one device connected, please provide
    --device-id" - so we deterministically choose a single target and pass it as
    -p:AdbTarget=-s <serial> to dotnet build. The same phone often shows up twice
    (e.g. once as 192.168.0.88:34667 and once as the mDNS service name), and this
    function de-duplicates by picking the most stable identifier:
      1. USB serial (no dots, no colons, not an mDNS entry) - rock-solid, no rotating ports.
      2. ip:port wireless serial - bundletool handles these natively.
      3. mDNS _adb-tls-connect._tcp service entry - last resort.
    #>
    param([Parameter(Mandatory)][string[]]$DeviceLines)
    $serials = $DeviceLines |
        ForEach-Object { ($_ -split "`t")[0].Trim() } |
        Where-Object { $_ }
    if (-not $serials -or $serials.Count -eq 0) {
        return $null
    }
    if ($serials.Count -eq 1) {
        return $serials[0]
    }
    $usb = $serials | Where-Object { $_ -notmatch '[.:]' -and $_ -notmatch '_adb-tls-connect' } | Select-Object -First 1
    if ($usb) { return $usb }
    $ipPort = $serials | Where-Object { $_ -match '^\d{1,3}(\.\d{1,3}){3}:\d+$' } | Select-Object -First 1
    if ($ipPort) { return $ipPort }
    return $serials[0]
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

function Deploy-Android {
    <#
    .DESCRIPTION
    Builds and deploys the MAUI Android target. When -Serial is supplied we pass it as
    -p:AdbTarget=-s <serial>, which bundletool / xabuild forward to adb so multi-device
    setups don't blow up with "More than one device connected, please provide --device-id".
    #>
    param([string]$Serial)
    Write-Section "Building + deploying MAUI client to the connected phone ($Configuration)"
    $buildArgs = @(
        'build', $MauiProjectPath,
        '-t:Run',
        '--framework', $AndroidTfm,
        '--configuration', $Configuration,
        '--nologo'
    )
    if ($Serial) {
        Write-Host "   Targeting adb serial: $Serial" -ForegroundColor DarkGray
        $buildArgs += "-p:AdbTarget=-s $Serial"
    }
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "MAUI Android deploy failed (exit $LASTEXITCODE)."
    }
}

Stop-LockingProcesses
Remove-BuildArtefacts

$baseAddress = Get-ProductionBaseAddress
Write-Host "Production BaseAddress: $baseAddress" -ForegroundColor Green
Test-BackendHealth -BaseAddress $baseAddress

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

$allowAutoConnect = -not $Connect -and -not $PairWith
Assert-DeviceConnected -AdbPath $adb -AllowAutoConnect $allowAutoConnect

# Re-read the device list AFTER connection has settled so we can pin bundletool to
# exactly one serial. Multi-device setups (USB + wireless, or wireless ip:port + the
# same phone broadcast over mDNS) otherwise fail the Release build with
# "More than one device connected, please provide --device-id".
$deviceLines = Get-OnlineDeviceLines -AdbPath $adb
$serial = Select-PreferredSerial -DeviceLines $deviceLines
if ($serial) {
    Write-Host "Selected device serial: $serial" -ForegroundColor Green
}

Deploy-Android -Serial $serial

Write-Host ''
Write-Host "MAUI client deployed. Production backend: $baseAddress" -ForegroundColor Green
Write-Host 'Open Pollmaster on your phone - works from any network (no LAN required).' -ForegroundColor Green
