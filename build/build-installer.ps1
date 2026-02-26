# ============================================================
# DeskGroup - Build Installer (.exe) Package
# ============================================================
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$InnoSetupPath = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot "src\DeskGroup\DeskGroup.csproj"
$outputDir = Join-Path $PSScriptRoot "output"
$publishDir = Join-Path $PSScriptRoot "publish-installer"
$innoScriptPath = Join-Path $PSScriptRoot "installer.iss"

[xml]$csproj = Get-Content $projectPath
$version = $csproj.Project.PropertyGroup.Version
if ([string]::IsNullOrEmpty($version)) { $version = "1.0.0" }

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DeskGroup Installer Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Version:       $version"
Write-Host "  Configuration: $Configuration"
Write-Host "  Runtime:       $Runtime"
Write-Host ""

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

dotnet restore $projectPath
if ($LASTEXITCODE -ne 0) { throw "NuGet restore failed." }

$publishArgs = @(
    "publish", $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--output", $publishDir,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false",
    "-p:PublishTrimmed=false"
)
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "Publish failed." }

$innoCompiler = ""
if (![string]::IsNullOrEmpty($InnoSetupPath)) {
    $innoCompiler = $InnoSetupPath
} else {
    $searchPaths = @(
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $searchPaths) {
        if (Test-Path $path) { $innoCompiler = $path; break }
    }
}

if (![string]::IsNullOrEmpty($innoCompiler) -and (Test-Path $innoCompiler)) {
    Write-Host "Inno Setup found. Generating installer script..." -ForegroundColor Green

    $issContent = "; DeskGroup Installer Script`r`n" +
        "[Setup]`r`n" +
        "AppId={{B7E3F2A1-5C8D-4F09-A2B6-D8E4F7C19A35}`r`n" +
        "AppName=DeskGroup`r`n" +
        "AppVersion=$version`r`n" +
        "AppVerName=DeskGroup $version`r`n" +
        "AppPublisher=DeskGroup`r`n" +
        "DefaultDirName={autopf}\DeskGroup`r`n" +
        "DefaultGroupName=DeskGroup`r`n" +
        "OutputDir=$outputDir`r`n" +
        "OutputBaseFilename=DeskGroup-Setup-$version`r`n" +
        "Compression=lzma2/ultra64`r`n" +
        "SolidCompression=yes`r`n" +
        "WizardStyle=modern`r`n" +
        "PrivilegesRequired=lowest`r`n" +
        "UninstallDisplayIcon={app}\DeskGroup.exe`r`n" +
        "ArchitecturesAllowed=x64`r`n" +
        "ArchitecturesInstallIn64BitMode=x64`r`n" +
        "MinVersion=10.0`r`n`r`n" +
        "[Languages]`r`n" +
        "Name: `"english`"; MessagesFile: `"compiler:Default.isl`"`r`n`r`n" +
        "[Tasks]`r`n" +
        "Name: `"desktopicon`"; Description: `"{cm:CreateDesktopIcon}`"; GroupDescription: `"{cm:AdditionalIcons}`"; Flags: unchecked`r`n" +
        "Name: `"startup`"; Description: `"Start DeskGroup with Windows`"; GroupDescription: `"Startup:`"; Flags: checked`r`n`r`n" +
        "[Files]`r`n" +
        "Source: `"$publishDir\*`"; DestDir: `"{app}`"; Flags: ignoreversion recursesubdirs`r`n`r`n" +
        "[Icons]`r`n" +
        "Name: `"{group}\DeskGroup`"; Filename: `"{app}\DeskGroup.exe`"`r`n" +
        "Name: `"{group}\Uninstall DeskGroup`"; Filename: `"{uninstallexe}`"`r`n" +
        "Name: `"{autodesktop}\DeskGroup`"; Filename: `"{app}\DeskGroup.exe`"; Tasks: desktopicon`r`n`r`n" +
        "[Run]`r`n" +
        "Filename: `"{app}\DeskGroup.exe`"; Description: `"{cm:LaunchProgram,DeskGroup}`"; Flags: nowait postinstall skipifsilent`r`n`r`n" +
        "[Registry]`r`n" +
        "Root: HKCU; Subkey: `"SOFTWARE\Microsoft\Windows\CurrentVersion\Run`"; ValueType: string; ValueName: `"DeskGroup`"; ValueData: `"`"`"{app}\DeskGroup.exe`"`" --startup`"; Flags: uninsdeletevalue; Tasks: startup`r`n`r`n" +
        "[UninstallRun]`r`n" +
        "Filename: `"{app}\DeskGroup.exe`"; Parameters: `"--uninstall-cleanup`"; Flags: runhidden skipifdoesntexist`r`n"

    [System.IO.File]::WriteAllText($innoScriptPath, $issContent, [System.Text.Encoding]::UTF8)
    & $innoCompiler $innoScriptPath
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }
    Remove-Item $innoScriptPath -Force -ErrorAction SilentlyContinue

} else {
    Write-Host "Inno Setup not found. Creating ZIP fallback..." -ForegroundColor Yellow

    $zipName = "DeskGroup-Setup-$version.zip"
    $zipPath = Join-Path $outputDir $zipName

    $installLines = @(
        "@echo off",
        "echo.",
        "echo  DeskGroup Installer",
        "echo  ====================",
        "echo.",
        "set INSTALL_DIR=%LOCALAPPDATA%\DeskGroup",
        "mkdir ""%INSTALL_DIR%"" 2>nul",
        "xcopy /E /Y /Q ""*.exe"" ""%INSTALL_DIR%\""",
        "xcopy /E /Y /Q ""*.dll"" ""%INSTALL_DIR%\"" 2>nul",
        "echo.",
        "echo  Creating Start Menu shortcut...",
        "powershell -Command ""$s=(New-Object -COM WScript.Shell).CreateShortcut('%APPDATA%\Microsoft\Windows\Start Menu\Programs\DeskGroup.lnk');$s.TargetPath='%INSTALL_DIR%\DeskGroup.exe';$s.WorkingDirectory='%INSTALL_DIR%';$s.Save()""",
        "echo  Installation complete!",
        "start """" ""%INSTALL_DIR%\DeskGroup.exe""",
        "pause"
    )
    $installScriptPath = Join-Path $publishDir "Install-DeskGroup.bat"
    [System.IO.File]::WriteAllLines($installScriptPath, $installLines, [System.Text.Encoding]::ASCII)

    $uninstallLines = @(
        "@echo off",
        "echo.",
        "echo  DeskGroup Uninstaller",
        "echo  =====================",
        "echo.",
        "taskkill /IM DeskGroup.exe /F 2>nul",
        "timeout /t 2 /nobreak >nul",
        "set INSTALL_DIR=%LOCALAPPDATA%\DeskGroup",
        "rmdir /S /Q ""%INSTALL_DIR%"" 2>nul",
        "del ""%APPDATA%\Microsoft\Windows\Start Menu\Programs\DeskGroup.lnk"" 2>nul",
        "reg delete ""HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"" /v ""DeskGroup"" /f 2>nul",
        "echo.",
        "echo  DeskGroup has been uninstalled.",
        "pause"
    )
    $uninstallScriptPath = Join-Path $publishDir "Uninstall-DeskGroup.bat"
    [System.IO.File]::WriteAllLines($uninstallScriptPath, $uninstallLines, [System.Text.Encoding]::ASCII)

    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath -Force
}

Remove-Item $publishDir -Recurse -Force

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Installer Build Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Output: $outputDir" -ForegroundColor White
Write-Host ""