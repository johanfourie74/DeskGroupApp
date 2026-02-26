# ============================================================
# DeskGroup â€” Build Portable (.zip) Package
# ============================================================
# Run from the repository root:
#   .\build\build-portable.ps1
#
# Requirements:
#   - .NET 6 SDK or later
#   - PowerShell 5.1+
#
# Output:
#   build\output\DeskGroup-Portable-{version}.zip
# ============================================================

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

# â”€â”€ Paths â”€â”€
$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot "src\DeskGroup\DeskGroup.csproj"
$outputDir = Join-Path $PSScriptRoot "output"
$publishDir = Join-Path $PSScriptRoot "publish-portable"

# â”€â”€ Read version from .csproj â”€â”€
[xml]$csproj = Get-Content $projectPath
$version = $csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrEmpty($version)) { $version = "1.0.0" }

$zipName = "DeskGroup-Portable-$version.zip"
$zipPath = Join-Path $outputDir $zipName

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DeskGroup Portable Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Version:        $version"
Write-Host "  Configuration:  $Configuration"
Write-Host "  Runtime:        $Runtime"
Write-Host "  Self-contained: $SelfContained"
Write-Host ""

# â”€â”€ Clean â”€â”€
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

# â”€â”€ Restore â”€â”€
Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed." }

# â”€â”€ Publish â”€â”€
Write-Host "Publishing..." -ForegroundColor Yellow

$publishArgs = @(
    "publish", $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--output", $publishDir,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
)

if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
    $publishArgs += "-p:PublishTrimmed=false"  # Trimming can break WPF reflection; keep off
} else {
    $publishArgs += "--self-contained", "false"
}

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

# â”€â”€ Add portable marker â”€â”€
Write-Host "Adding portable marker..." -ForegroundColor Yellow
"This file tells DeskGroup to store data next to the exe." | Out-File (Join-Path $publishDir "portable.marker") -Encoding UTF8

# â”€â”€ Add README â”€â”€
$readmeSrc = Join-Path $repoRoot "README.md"
if (Test-Path $readmeSrc) {
    Copy-Item $readmeSrc $publishDir
}

# â”€â”€ Create ZIP â”€â”€
Write-Host "Creating ZIP archive..." -ForegroundColor Yellow
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force

# â”€â”€ Cleanup â”€â”€
Remove-Item $publishDir -Recurse -Force

# â”€â”€ Done â”€â”€
$zipSize = [math]::Round((Get-Item $zipPath).Length / 1MB, 2)
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Output: $zipPath" -ForegroundColor White
Write-Host "  Size:   $zipSize MB" -ForegroundColor White
Write-Host ""
Write-Host "  To use:" -ForegroundColor Yellow
Write-Host "    1. Extract the ZIP anywhere" -ForegroundColor Gray
Write-Host "    2. Run DeskGroup.exe" -ForegroundColor Gray
Write-Host "    3. Data stored in DeskGroupData\ next to exe" -ForegroundColor Gray
Write-Host ""