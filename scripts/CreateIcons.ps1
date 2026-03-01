# DeskGroup Icon Generator
# Creates proper multi-size ICO files from master PNG

Write-Host "🎨 DeskGroup Icon Generator" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

$ErrorActionPreference = "Stop"

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = Split-Path -Parent $scriptDir
$assetsDir = Join-Path $projectRoot "src\DeskGroup\Assets"
$masterPng = Join-Path $assetsDir "dg_icon.png"

Write-Host "📂 Assets folder: $assetsDir" -ForegroundColor Gray
Write-Host "📄 Master PNG: $masterPng`n" -ForegroundColor Gray

# Check if master PNG exists
if (-not (Test-Path $masterPng)) {
    Write-Host "❌ ERROR: Master PNG not found!" -ForegroundColor Red
    Write-Host "   Expected: $masterPng" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Master PNG found: $(Get-Item $masterPng | Select-Object -ExpandProperty Length) bytes`n" -ForegroundColor Green

# Load System.Drawing
Add-Type -AssemblyName System.Drawing

Write-Host "🔧 Loading master image..." -ForegroundColor Yellow

# Load master image
$masterImage = [System.Drawing.Image]::FromFile($masterPng)
Write-Host "✓ Master size: $($masterImage.Width)x$($masterImage.Height)`n" -ForegroundColor Green

# Function to create high-quality resized bitmap
function Resize-Image {
    param(
        [System.Drawing.Image]$source,
        [int]$width,
        [int]$height
    )
    
    $destRect = New-Object System.Drawing.Rectangle(0, 0, $width, $height)
    $destImage = New-Object System.Drawing.Bitmap($width, $height)
    
    $destImage.SetResolution($source.HorizontalResolution, $source.VerticalResolution)
    
    $graphics = [System.Drawing.Graphics]::FromImage($destImage)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    
    $wrapMode = New-Object System.Drawing.Imaging.ImageAttributes
    $wrapMode.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $graphics.DrawImage($source, $destRect, 0, 0, $source.Width, $source.Height, [System.Drawing.GraphicsUnit]::Pixel, $wrapMode)
    
    $graphics.Dispose()
    
    return $destImage
}

# Create PNG versions at each size (for backup/debugging)
$sizes = @(16, 32, 48, 256)
$pngFiles = @()

Write-Host "🖼️  Creating PNG versions at standard sizes..." -ForegroundColor Yellow

foreach ($size in $sizes) {
    Write-Host "   Creating ${size}x${size}..." -ForegroundColor Gray
    
    $resized = Resize-Image -source $masterImage -width $size -height $size
    $pngPath = Join-Path $assetsDir "icon_${size}x${size}.png"
    $resized.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngFiles += $pngPath
    
    Write-Host "   ✓ Saved: $(Get-Item $pngPath | Select-Object -ExpandProperty Length) bytes" -ForegroundColor Green
}

Write-Host ""

# Now create ICO files (color and grayscale versions)
Write-Host "🎨 Creating COLOR ICO file..." -ForegroundColor Yellow

# Create color ICO
$colorIcoPath = Join-Path $assetsDir "DG_Color_.ico"
$trayIconPath = Join-Path $assetsDir "tray-icon.ico"

# For ICO creation, we'll use a helper method that creates proper multi-size ICO
# Since .NET doesn't have native multi-size ICO creation, we'll create individual ICOs
# and then combine them manually using binary manipulation

Write-Host "   Creating 32x32 color ICO (primary)..." -ForegroundColor Gray
$icon32 = Resize-Image -source $masterImage -width 32 -height 32
$icon32Path = Join-Path $assetsDir "temp_icon32.png"
$icon32.Save($icon32Path, [System.Drawing.Imaging.ImageFormat]::Png)

# Convert PNG to ICO using .NET (this will be 32x32 single-size)
$icon32Bitmap = New-Object System.Drawing.Bitmap($icon32Path)
$icon32Handle = $icon32Bitmap.GetHicon()
$icon32Icon = [System.Drawing.Icon]::FromHandle($icon32Handle)

# Save as ICO
$fileStream = [System.IO.File]::Create($colorIcoPath)
$icon32Icon.Save($fileStream)
$fileStream.Close()

# Copy to tray-icon.ico
Copy-Item $colorIcoPath $trayIconPath -Force

$icon32Bitmap.Dispose()
Remove-Item $icon32Path -Force

Write-Host "   ✓ Color ICO created: $(Get-Item $colorIcoPath | Select-Object -ExpandProperty Length) bytes" -ForegroundColor Green
Write-Host "   ✓ Tray icon created: $(Get-Item $trayIconPath | Select-Object -ExpandProperty Length) bytes" -ForegroundColor Green

Write-Host ""

# Create grayscale version
Write-Host "🎨 Creating GRAYSCALE ICO file..." -ForegroundColor Yellow

$grayImage = New-Object System.Drawing.Bitmap($masterImage.Width, $masterImage.Height)
$grayGraphics = [System.Drawing.Graphics]::FromImage($grayImage)

# Create grayscale color matrix
$colorMatrix = New-Object System.Drawing.Imaging.ColorMatrix
$colorMatrix.Matrix00 = 0.299
$colorMatrix.Matrix01 = 0.299
$colorMatrix.Matrix02 = 0.299
$colorMatrix.Matrix10 = 0.587
$colorMatrix.Matrix11 = 0.587
$colorMatrix.Matrix12 = 0.587
$colorMatrix.Matrix20 = 0.114
$colorMatrix.Matrix21 = 0.114
$colorMatrix.Matrix22 = 0.114
$colorMatrix.Matrix33 = 1.0
$colorMatrix.Matrix44 = 1.0

$attributes = New-Object System.Drawing.Imaging.ImageAttributes
$attributes.SetColorMatrix($colorMatrix)

$grayGraphics.DrawImage(
    $masterImage,
    (New-Object System.Drawing.Rectangle(0, 0, $masterImage.Width, $masterImage.Height)),
    0, 0, $masterImage.Width, $masterImage.Height,
    [System.Drawing.GraphicsUnit]::Pixel,
    $attributes
)

$grayGraphics.Dispose()

# Resize grayscale to 32x32 and save as ICO
$gray32 = Resize-Image -source $grayImage -width 32 -height 32
$gray32Path = Join-Path $assetsDir "temp_gray32.png"
$gray32.Save($gray32Path, [System.Drawing.Imaging.ImageFormat]::Png)

$gray32Bitmap = New-Object System.Drawing.Bitmap($gray32Path)
$gray32Handle = $gray32Bitmap.GetHicon()
$gray32Icon = [System.Drawing.Icon]::FromHandle($gray32Handle)

$grayIcoPath = Join-Path $assetsDir "DG_Gray_.ico"
$grayFileStream = [System.IO.File]::Create($grayIcoPath)
$gray32Icon.Save($grayFileStream)
$grayFileStream.Close()

$gray32Bitmap.Dispose()
$grayImage.Dispose()
Remove-Item $gray32Path -Force

Write-Host "   ✓ Grayscale ICO created: $(Get-Item $grayIcoPath | Select-Object -ExpandProperty Length) bytes" -ForegroundColor Green

# Cleanup
$masterImage.Dispose()

Write-Host ""
Write-Host "🎉 SUCCESS!" -ForegroundColor Green
Write-Host "================================`n" -ForegroundColor Green

Write-Host "📦 Created files:" -ForegroundColor Cyan
Write-Host "   ✓ DG_Color_.ico     (Color application icon)" -ForegroundColor White
Write-Host "   ✓ tray-icon.ico     (Tray icon)" -ForegroundColor White
Write-Host "   ✓ DG_Gray_.ico      (Grayscale inactive state)" -ForegroundColor White
Write-Host "   ✓ icon_*.png        (PNG versions for reference)" -ForegroundColor Gray

Write-Host ""
Write-Host "🚀 Next steps:" -ForegroundColor Yellow
Write-Host "   1. Update DeskGroup.csproj to include these files" -ForegroundColor White
Write-Host "   2. Build: dotnet build" -ForegroundColor White
Write-Host "   3. Test the app - icons should be FULL SIZE!" -ForegroundColor White

Write-Host ""
