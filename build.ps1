<#
.SYNOPSIS
    RSS_II_RGB build script — .NET 10 / Avalonia.

.DESCRIPTION
    Builds the solution. Three modes:
      - Debug    : dotnet build, Debug config (fast iteration)
      - Release  : dotnet build, Release config (default)
      - Publish  : Native AOT trimmed exe of the App into dist\ (distribution)

    The non-AOT SensorsHost helper is built and copied into dist\sensorshost\
    by the App's own MSBuild target.

.PARAMETER Mode
    Build mode: Debug, Release, or Publish (default: Release)

.PARAMETER Target
    Build target: All, App, Test (default: All)

.PARAMETER Clean
    Remove dist\ as well (bin/obj are always removed before a build)

.PARAMETER SkipRestore
    Skip NuGet package restore (faster if already restored)

.EXAMPLE
    .\build.ps1                       # Release build, all projects + tests
    .\build.ps1 -Mode Debug           # Debug build
    .\build.ps1 -Mode Publish         # Native AOT publish into dist\
    .\build.ps1 -Mode Publish -Clean  # Clean + publish
    .\build.ps1 -Target Test          # Run tests only
    .\build.ps1 -Target App           # Build the app only (no tests)
#>

[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release", "Publish")]
    [string]$Mode = "Release",

    [ValidateSet("All", "App", "Test")]
    [string]$Target = "All",

    [switch]$Clean,
    [switch]$SkipRestore,

    [string]$LogFile = ""
)

# ============================================================
# Configuration
# ============================================================

$ErrorActionPreference = "Stop"
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# Force UTF-8 for console + .NET subprocess output (fixes CJK garbling)
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
[Console]::InputEncoding  = [System.Text.Encoding]::UTF8
$env:DOTNET_CLI_UI_LANGUAGE = "en"

$script:transcriptActive = $false
if ($LogFile) {
    try {
        Start-Transcript -Path $LogFile -Force | Out-Null
        $script:transcriptActive = $true
    }
    catch {
        Write-Warning "Could not start transcript to $LogFile : $_"
    }
}

$ROOT_DIR  = $PSScriptRoot
$SLN       = Join-Path $ROOT_DIR "RSS_II_RGB.slnx"
$APP_PROJ  = Join-Path $ROOT_DIR "src\RSS_II_RGB.App\RSS_II_RGB.App.csproj"
$DIST_DIR  = Join-Path $ROOT_DIR "dist"
$RID       = "win-x64"
$Config    = if ($Mode -eq "Debug") { "Debug" } else { "Release" }

$script:vsDevShellLoaded = $false
$script:vsPath = $null

# ============================================================
# Helper functions
# ============================================================

function Write-Banner([string]$Text) {
    $line = "=" * 60
    Write-Host ""
    Write-Host $line -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host $line -ForegroundColor Cyan
}

function Write-Step([string]$Text) { Write-Host ">> $Text" -ForegroundColor Yellow }
function Write-Ok([string]$Text)   { Write-Host "   [OK] $Text" -ForegroundColor Green }
function Write-Fail([string]$Text) { Write-Host "   [FAIL] $Text" -ForegroundColor Red }
function Write-Info([string]$Text) { Write-Host "   $Text" -ForegroundColor Gray }

function Get-FileSize([string]$Path) {
    if (Test-Path $Path) {
        $size = (Get-Item $Path).Length
        if ($size -gt 1MB) { return "{0:N1} MB" -f ($size / 1MB) }
        if ($size -gt 1KB) { return "{0:N1} KB" -f ($size / 1KB) }
        return "$size B"
    }
    return "N/A"
}

function Enter-VsDevEnvironment() {
    # Native AOT publish needs the MSVC linker (link.exe). Load the x64 dev
    # environment once. JIT builds/tests don't need this.
    if ($script:vsDevShellLoaded) { return $true }

    # Already in an x64 MSVC dev environment? Don't re-enter (Enter-VsDevShell
    # prepends VC paths every time and can blow past the 8191-char command limit).
    if ($env:VSCMD_ARG_TGT_ARCH -eq 'x64') {
        $script:vsDevShellLoaded = $true
        Write-Ok "MSVC x64 environment already loaded (reusing inherited env)"
        return $true
    }

    if (-not $script:vsPath) {
        $vswhere = $null
        $candidates = @()
        $inPath = Get-Command vswhere -ErrorAction SilentlyContinue
        if ($inPath) { $candidates += $inPath.Source }
        $pf86 = ${env:ProgramFiles(x86)}
        if ($pf86) { $candidates += (Join-Path $pf86 "Microsoft Visual Studio\Installer\vswhere.exe") }
        if ($env:ProgramFiles) { $candidates += (Join-Path $env:ProgramFiles "Microsoft Visual Studio\Installer\vswhere.exe") }
        foreach ($c in $candidates) {
            if ($c -and (Test-Path $c -ErrorAction SilentlyContinue)) { $vswhere = $c; break }
        }
        if (-not $vswhere) {
            Write-Fail "vswhere.exe not found. Install Visual Studio with the C++ Desktop workload (needed for Native AOT)."
            return $false
        }
        $script:vsPath = & $vswhere -latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
        if (-not $script:vsPath) { $script:vsPath = & $vswhere -latest -property installationPath }
        if (-not $script:vsPath) {
            Write-Fail "No Visual Studio with the C++ Desktop workload found (needed for Native AOT)."
            return $false
        }
    }

    $devShellDll = Join-Path $script:vsPath "Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
    if (-not (Test-Path $devShellDll)) {
        Write-Fail "DevShell.dll not found: $devShellDll"
        return $false
    }

    try {
        Import-Module $devShellDll
        Enter-VsDevShell -VsInstallPath $script:vsPath -SkipAutomaticLocation -DevCmdArguments "-arch=x64 -host_arch=x64" | Out-Null
        $script:vsDevShellLoaded = $true
        Write-Ok "MSVC x64 environment loaded ($script:vsPath)"
        return $true
    }
    catch {
        Write-Fail "Failed to load VS DevShell: $_"
        return $false
    }
}

function Remove-BinObj() {
    # Always clean-build: remove bin/obj under src\ and tests\ first.
    foreach ($base in @("src", "tests")) {
        $dir = Join-Path $ROOT_DIR $base
        if (-not (Test-Path $dir)) { continue }
        Get-ChildItem -Path $dir -Recurse -Directory -Include bin, obj -ErrorAction SilentlyContinue |
            ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# ============================================================
# Preamble
# ============================================================

Write-Banner "RSS_II_RGB Build  |  Mode: $Mode  |  Target: $Target"
Write-Info "Root:   $ROOT_DIR"
Write-Info "Config: $Config"
Write-Info "Clean:  $Clean"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Fail "dotnet SDK not found in PATH"
    exit 1
}
Write-Info "dotnet: $(dotnet --version)"

# ============================================================
# Clean
# ============================================================

Write-Step "Removing bin/obj (clean build)..."
Remove-BinObj
if ($Clean -and (Test-Path $DIST_DIR)) {
    Remove-Item $DIST_DIR -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Ok "Clean complete"

$exitCode = 0

# ============================================================
# Restore
# ============================================================

if (-not $SkipRestore -and $exitCode -eq 0) {
    Write-Step "Restoring NuGet packages..."
    & dotnet restore $SLN --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Fail "Restore failed"; $exitCode = 1 } else { Write-Ok "Packages restored" }
}

# ============================================================
# Build / Publish
# ============================================================

if ($exitCode -eq 0 -and $Target -in "All", "App") {

    if ($Mode -eq "Publish") {
        Write-Banner "Native AOT publish  |  $RID"

        if (-not (Enter-VsDevEnvironment)) {
            $exitCode = 1
        }
        else {
            New-Item -ItemType Directory -Path $DIST_DIR -Force | Out-Null
            Write-Step "Publishing RSS_II_RGB.App (Native AOT, Release)..."

            # IlcUseEnvironmentalTools: use the MSVC linker already on PATH (loaded
            # above) instead of ILCompiler running its own vcvars discovery, which
            # is fragile when the inherited environment is bloated.
            & dotnet publish $APP_PROJ `
                -c Release `
                -r $RID `
                -p:PublishAot=true `
                -p:IlcUseEnvironmentalTools=true `
                -o $DIST_DIR `
                --nologo

            if ($LASTEXITCODE -ne 0) {
                Write-Fail "AOT publish failed"
                $exitCode = 1
            }
            else {
                $exe = Join-Path $DIST_DIR "RSS_II_RGB.App.exe"
                if (Test-Path $exe) {
                    # Distribution ships without *.pdb symbols.
                    Get-ChildItem -Path $DIST_DIR -Recurse -Filter "*.pdb" -File -ErrorAction SilentlyContinue |
                        Remove-Item -Force -ErrorAction SilentlyContinue
                    Write-Ok "RSS_II_RGB.App.exe ($(Get-FileSize $exe))"
                    if (Test-Path (Join-Path $DIST_DIR "sensorshost\RSS_II_RGB.SensorsHost.exe")) {
                        Write-Ok "SensorsHost helper -> dist\sensorshost\"
                    }
                }
                else {
                    Write-Fail "RSS_II_RGB.App.exe not found in publish output"
                    $exitCode = 1
                }
            }
        }
    }
    else {
        Write-Banner "Build  |  $Config"
        $project = if ($Target -eq "App") { $APP_PROJ } else { $SLN }
        Write-Step "dotnet build ($Config)..."
        & dotnet build $project -c $Config --nologo -v minimal
        if ($LASTEXITCODE -ne 0) { Write-Fail "Build failed"; $exitCode = 1 } else { Write-Ok "Build succeeded" }
    }
}

# ============================================================
# Tests
# ============================================================

if ($exitCode -eq 0 -and $Target -in "All", "Test") {
    Write-Banner "Unit Tests"
    Write-Step "dotnet test ($Config)..."
    & dotnet test $SLN -c $Config --nologo
    if ($LASTEXITCODE -ne 0) { Write-Fail "Tests failed"; $exitCode = 1 } else { Write-Ok "All tests passed" }
}

# ============================================================
# Summary
# ============================================================

$sw.Stop()
Write-Banner "Build Summary"
Write-Host ""
Write-Host "  Mode:    $Mode"  -ForegroundColor White
Write-Host "  Target:  $Target" -ForegroundColor White
Write-Host "  Time:    $($sw.Elapsed.ToString('mm\:ss\.ff'))" -ForegroundColor White

$statusColor = if ($exitCode -eq 0) { "Green" } else { "Red" }
$statusText  = if ($exitCode -eq 0) { "SUCCESS" } else { "FAILED" }
Write-Host "  Status:  $statusText" -ForegroundColor $statusColor
Write-Host ""

if ($Mode -eq "Publish" -and (Test-Path $DIST_DIR)) {
    Write-Host "  Output: $DIST_DIR" -ForegroundColor White
    Get-ChildItem $DIST_DIR -File -ErrorAction SilentlyContinue | ForEach-Object {
        Write-Host "    $($_.Name)  ($(Get-FileSize $_.FullName))" -ForegroundColor Gray
    }
}
Write-Host ""

if ($script:transcriptActive) {
    try { Stop-Transcript | Out-Null } catch { }
}
exit $exitCode
