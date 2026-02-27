# DeskGroup - Bug Fix Verification Test Script
# Run this after applying the bug fixes

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "DeskGroup Bug Fix Verification" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

$projectPath = "C:\Dev\DeskGroup"
$exePath = "$projectPath\src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe"

# Step 1: Stop any running instances
Write-Host "[1/6] Stopping existing DeskGroup processes..." -ForegroundColor Yellow
Get-Process -Name "DeskGroup" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2

# Step 2: Build the project
Write-Host "[2/6] Building DeskGroup..." -ForegroundColor Yellow
$buildResult = dotnet build "$projectPath\DeskGroup.sln" -c Debug --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build successful" -ForegroundColor Green

# Step 3: Clean app data (fresh start)
Write-Host "[3/6] Cleaning AppData for fresh start..." -ForegroundColor Yellow
$appDataPath = "$env:APPDATA\DeskGroup"
if (Test-Path $appDataPath) {
    Remove-Item -Recurse -Force $appDataPath
    Write-Host "✅ Cleaned AppData" -ForegroundColor Green
} else {
    Write-Host "ℹ️  No existing AppData found" -ForegroundColor Gray
}

# Step 4: Start DeskGroup
Write-Host "[4/6] Starting DeskGroup..." -ForegroundColor Yellow
Start-Process $exePath
Start-Sleep -Seconds 3

# Check if process is running
$process = Get-Process -Name "DeskGroup" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "✅ DeskGroup is running (PID: $($process.Id))" -ForegroundColor Green
} else {
    Write-Host "❌ DeskGroup failed to start!" -ForegroundColor Red
    exit 1
}

# Step 5: Manual Test Instructions
Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "MANUAL TEST CHECKLIST" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "TEST 1: Create Groups" -ForegroundColor Yellow
Write-Host "  1. Right-click tray icon → 'Create Group'" -ForegroundColor White
Write-Host "  2. Create 3 groups (Group 1, Group 2, Group 3)" -ForegroundColor White
Write-Host "  ✅ Expected: All groups appear immediately" -ForegroundColor Green
Write-Host ""

Write-Host "TEST 2: Color Picker Stability" -ForegroundColor Yellow
Write-Host "  1. Double-click Group 1 to expand to Normal view" -ForegroundColor White
Write-Host "  2. Click the color circle in title bar" -ForegroundColor White
Write-Host "  3. Select a new color (e.g., purple #BF0077)" -ForegroundColor White
Write-Host "  4. Repeat for Group 2 and Group 3" -ForegroundColor White
Write-Host "  ✅ Expected: Colors change smoothly, NO CRASHES" -ForegroundColor Green
Write-Host ""

Write-Host "TEST 3: Drag Performance" -ForegroundColor Yellow
Write-Host "  1. Drag Group 1 rapidly around the screen" -ForegroundColor White
Write-Host "  2. Move in circles, fast movements" -ForegroundColor White
Write-Host "  3. Release and observe position" -ForegroundColor White
Write-Host "  ✅ Expected: Smooth dragging, no lag or flicker" -ForegroundColor Green
Write-Host ""

Write-Host "TEST 4: Restart Persistence" -ForegroundColor Yellow
Write-Host "  1. Close DeskGroup (right-click tray → Exit)" -ForegroundColor White
Write-Host "  2. Wait 2 seconds" -ForegroundColor White
Write-Host "  3. Restart DeskGroup" -ForegroundColor White
Write-Host "  ✅ Expected: All groups reappear with saved colors" -ForegroundColor Green
Write-Host ""

# Step 6: Wait for user confirmation
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "[5/6] Waiting for manual testing..." -ForegroundColor Yellow
Write-Host "      Press ENTER after completing tests above" -ForegroundColor White
Read-Host

# Check if still running
$process = Get-Process -Name "DeskGroup" -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "[6/6] DeskGroup still running - Test completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "To view Debug Output:" -ForegroundColor Cyan
    Write-Host "  • In Visual Studio: View → Output → Show output from: Debug" -ForegroundColor White
    Write-Host "  • Look for 'ColorSwatch_Click' and 'ApplyAccentColor' messages" -ForegroundColor White
    Write-Host ""
    
    $cleanup = Read-Host "Stop DeskGroup? (Y/N)"
    if ($cleanup -eq "Y" -or $cleanup -eq "y") {
        Stop-Process -Name "DeskGroup" -Force
        Write-Host "✅ DeskGroup stopped" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  DeskGroup is not running - Did it crash?" -ForegroundColor Red
}

Write-Host ""
Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Test session complete!" -ForegroundColor Cyan
Write-Host "Review results above and check:" -ForegroundColor White
Write-Host "  • No crashes during color changes" -ForegroundColor White
Write-Host "  • Smooth drag performance" -ForegroundColor White
Write-Host "  • Groups visible on startup" -ForegroundColor White
Write-Host "==================================" -ForegroundColor Cyan
