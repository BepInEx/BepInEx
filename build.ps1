# Simple build script for BepInEx NuGet packages
param(
    [string]$Configuration = "Release",
    [string]$VersionSuffix = "",
    [switch]$SkipTests,
    [switch]$SkipPack,
    [string]$OutputDir = "./artifacts",
    [switch]$Help
)

# Check for help flags in all command line arguments
$allArgs = [Environment]::GetCommandLineArgs()
foreach ($arg in $allArgs) {
    if ($arg -eq "--help" -or $arg -eq "-h") {
        $Help = $true
        break
    }
}

# Also check if Configuration was set to --help by mistake
if ($Configuration -eq "--help" -or $Configuration -eq "-h") {
    $Help = $true
    $Configuration = "Release"  # Reset to default
}

if ($Help) {
    Write-Host "Usage: .\build.ps1 [options]"
    Write-Host "Options:"
    Write-Host "  -Configuration <CONFIG>  Build configuration (Debug/Release) [default: Release]"
    Write-Host "  -VersionSuffix <SUFFIX>  Version suffix for pre-release versions"
    Write-Host "  -SkipTests               Skip running tests"
    Write-Host "  -SkipPack                Skip creating NuGet packages"
    Write-Host "  -OutputDir <DIR>         Output directory for NuGet packages [default: ./artifacts]"
    Write-Host "  -Help, --help, -h        Show this help message"
    exit 0
}

Write-Host "Building BepInEx with configuration: $Configuration" -ForegroundColor Green

# Clean previous artifacts
if (Test-Path $OutputDir) {
    Write-Host "Cleaning previous artifacts..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}
New-Item -Path $OutputDir -ItemType Directory -Force | Out-Null

# Restore dependencies
Write-Host "Restoring dependencies..." -ForegroundColor Green
dotnet restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Build
Write-Host "Building solution..." -ForegroundColor Green
if ($VersionSuffix) {
    dotnet build --configuration $Configuration --no-restore -p:VersionSuffix=$VersionSuffix
} else {
    dotnet build --configuration $Configuration --no-restore
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Run tests if not skipped
if (-not $SkipTests) {
    Write-Host "Running tests..." -ForegroundColor Green
    if ((Test-Path "tests") -or (Test-Path "Tests")) {
        dotnet test --configuration $Configuration --no-build --verbosity normal
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    } else {
        Write-Host "No tests directory found, skipping tests" -ForegroundColor Yellow
    }
}

# Pack NuGet packages if not skipped
if (-not $SkipPack) {
    Write-Host "Creating NuGet packages..." -ForegroundColor Green
    if ($VersionSuffix) {
        dotnet pack --configuration $Configuration --no-build -p:VersionSuffix=$VersionSuffix --output $OutputDir
    } else {
        dotnet pack --configuration $Configuration --no-build --output $OutputDir
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    
    Write-Host "NuGet packages created in ${OutputDir}:" -ForegroundColor Green
    Get-ChildItem -Path "$OutputDir/*.nupkg" | ForEach-Object { Write-Host $_.Name }
}

Write-Host "Build completed successfully!" -ForegroundColor Green
exit 0