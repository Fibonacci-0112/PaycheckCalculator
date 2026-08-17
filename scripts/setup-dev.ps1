<#
.SYNOPSIS
    One-shot development environment setup for Windows.

.DESCRIPTION
    Installs the .NET SDK pinned in global.json into a repo-local .dotnet\ directory, then
    installs the full `maui` workload. Windows is the only host that can build the
    net11.0-windows10.0.19041.0 (WinUI) target; it can also build net11.0-android.
    iOS and Mac Catalyst still require macOS — no Windows workaround exists.

.PARAMETER Emulator
    Also install the Android emulator and a system image, so the app can be run, not just built.

.PARAMETER Appium
    Also install Appium with the uiautomator2 and windows drivers for the MAUI UI tests.

.PARAMETER Playwright
    Also install Playwright browsers for the Blazor end-to-end tests.

.PARAMETER All
    Equivalent to -Emulator -Appium -Playwright.

.EXAMPLE
    pwsh scripts/setup-dev.ps1 -All
#>
[CmdletBinding()]
param(
    [switch] $Emulator,
    [switch] $Appium,
    [switch] $Playwright,
    [switch] $All
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ($All) { $Emulator = $true; $Appium = $true; $Playwright = $true }

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$dotnetRoot = Join-Path $repoRoot '.dotnet'
$dotnetExe = Join-Path $dotnetRoot 'dotnet.exe'

# ---------------------------------------------------------------- .NET SDK ---
Write-Host '==> [1/4] .NET SDK' -ForegroundColor Cyan

$sdkVersion = (Get-Content (Join-Path $repoRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
if (-not $sdkVersion) { throw "Could not read sdk.version from global.json" }

$alreadyInstalled = (Test-Path $dotnetExe) -and
                    ((& $dotnetExe --list-sdks) -match [regex]::Escape($sdkVersion))
if ($alreadyInstalled) {
    Write-Host ".NET SDK $sdkVersion already installed in $dotnetRoot"
} else {
    Write-Host "Installing .NET SDK $sdkVersion into $dotnetRoot ..."
    $installer = Join-Path ([System.IO.Path]::GetTempPath()) 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer -UseBasicParsing
    & $installer -Version $sdkVersion -InstallDir $dotnetRoot
}

# The pinned SDK must shadow any machine-wide dotnet for the rest of this session.
$env:DOTNET_ROOT = $dotnetRoot
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:PATH = "$dotnetRoot;$env:PATH"

# -------------------------------------------------------------- Workloads ---
Write-Host ''
Write-Host '==> [2/4] MAUI workloads (maui)' -ForegroundColor Cyan
# Invoke the repo-local host explicitly: `dotnet workload` writes its install records
# relative to the dotnet root of whichever host runs it.
& $dotnetExe workload install maui --skip-sign-check
& $dotnetExe workload list

# ------------------------------------------------------------ Android SDK ---
Write-Host ''
Write-Host '==> [3/4] Android SDK' -ForegroundColor Cyan

$androidHome = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
$sdkManager = Join-Path $androidHome 'cmdline-tools\latest\bin\sdkmanager.bat'

if (-not (Get-Command java -ErrorAction SilentlyContinue)) {
    throw "A JDK is required (sdkmanager and the Android build both need one). Install Microsoft OpenJDK 21: winget install Microsoft.OpenJDK.21"
}

if (-not (Test-Path $sdkManager)) {
    $cmdlineToolsVersion = if ($env:CMDLINE_TOOLS_VERSION) { $env:CMDLINE_TOOLS_VERSION } else { '13114758' }
    Write-Host "Installing Android command-line tools ($cmdlineToolsVersion) into $androidHome ..."
    New-Item -ItemType Directory -Force -Path (Join-Path $androidHome 'cmdline-tools') | Out-Null
    $zip = Join-Path ([System.IO.Path]::GetTempPath()) 'android-cmdline-tools.zip'
    Invoke-WebRequest -UseBasicParsing -OutFile $zip `
        -Uri "https://dl.google.com/android/repository/commandlinetools-win-${cmdlineToolsVersion}_latest.zip"
    # The archive expands to a bare cmdline-tools\ folder; sdkmanager requires being at
    # cmdline-tools\latest\ so it can locate the rest of the SDK relative to itself.
    $staging = Join-Path $androidHome 'cmdline-tools'
    Remove-Item -Recurse -Force (Join-Path $staging 'latest'), (Join-Path $staging 'cmdline-tools') -ErrorAction SilentlyContinue
    Expand-Archive -Path $zip -DestinationPath $staging -Force
    Move-Item (Join-Path $staging 'cmdline-tools') (Join-Path $staging 'latest')
    Remove-Item $zip -Force
}

$env:ANDROID_HOME = $androidHome
$env:ANDROID_SDK_ROOT = $androidHome

# Note the ".0": from API 36 on, Google publishes minor-versioned platform packages, so the
# id is `platforms;android-37.0`, not `platforms;android-37`.
$packages = @('platform-tools', 'platforms;android-37.0', 'build-tools;37.0.0')
if ($Emulator) { $packages += @('emulator', 'system-images;android-35;google_apis;x86_64') }

Write-Host 'Accepting Android SDK licenses ...'
'y' * 20 -split '' | Out-String | & $sdkManager --licenses 2>&1 | Out-Null

Write-Host "Installing: $($packages -join ' ')"
& $sdkManager --install @packages

# --------------------------------------------------------- Test tooling ---
Write-Host ''
Write-Host '==> [4/4] Optional test tooling' -ForegroundColor Cyan

if ($Appium) {
    if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
        throw "Node.js (with npm) is required for Appium. Install it: winget install OpenJS.NodeJS.LTS"
    }
    if (-not (Get-Command appium -ErrorAction SilentlyContinue)) {
        Write-Host 'Installing Appium ...'
        npm install -g appium@3
    }
    foreach ($driver in @('uiautomator2', 'windows')) {
        if ((appium driver list --installed 2>&1) -match $driver) {
            Write-Host "appium driver '$driver' already installed"
        } else {
            appium driver install $driver
        }
    }
    Write-Host ''
    Write-Host 'NOTE: the Appium windows driver automates apps through WinAppDriver.' -ForegroundColor Yellow
    Write-Host '      Install WinAppDriver 1.2.1 (that exact version) from:' -ForegroundColor Yellow
    Write-Host '      https://github.com/microsoft/WinAppDriver/releases/tag/v1.2.1' -ForegroundColor Yellow
    Write-Host '      Windows must also be in Developer Mode for UI automation to attach.' -ForegroundColor Yellow
} else {
    Write-Host 'skipped Appium (pass -Appium to install the MAUI UI-test driver stack)'
}

if ($Playwright) {
    & $dotnetExe build PaycheckCalculator.E2ETests -c Debug
    $pw = Get-ChildItem -Path 'PaycheckCalculator.E2ETests\bin' -Filter 'playwright.ps1' -Recurse |
          Select-Object -First 1
    if ($pw) { & $pw.FullName install chromium }
    else { Write-Warning 'playwright.ps1 not found; build PaycheckCalculator.E2ETests first.' }
} else {
    Write-Host 'skipped Playwright (pass -Playwright to install browsers for Blazor E2E tests)'
}

Write-Host ''
Write-Host ('=' * 80)
Write-Host 'Setup complete on Windows.'
Write-Host ''
Write-Host 'Every new shell needs the repo-local SDK on PATH:'
Write-Host '    $env:DOTNET_ROOT = "$PWD\.dotnet"; $env:PATH = "$env:DOTNET_ROOT;$env:PATH"'
Write-Host ''
Write-Host 'Then verify:'
Write-Host '    dotnet test  PaycheckCalculator.Tests'
Write-Host '    dotnet build PaycheckCalculator.Blazor'
Write-Host '    dotnet build PaycheckCalculator.App -f net11.0-android'
Write-Host '    dotnet build PaycheckCalculator.App -f net11.0-windows10.0.19041.0'
Write-Host ''
Write-Host 'Not buildable on Windows (toolchain is OS-locked, no workaround exists):'
Write-Host '    net11.0-ios / net11.0-maccatalyst -> requires macOS + Xcode'
Write-Host 'CI covers those on macos-latest runners.'
Write-Host ('=' * 80)
