# TEST SCRIPT - Run DeskGroup and Create First Group

Write-Host "=== DeskGroup Test Script ===" -ForegroundColor Cyan
Write-Host ""

# Step 1: Kill any running instances
Write-Host "1. Stopping any running DeskGroup instances..." -ForegroundColor Yellow
Stop-Process -Name DeskGroup -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Step 2: Launch DeskGroup
Write-Host "2. Starting DeskGroup..." -ForegroundColor Yellow
Start-Process -FilePath "C:\Dev\DeskGroup\src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe"
Start-Sleep -Seconds 3

# Step 3: Verify it's running
Write-Host "3. Verifying DeskGroup is running..." -ForegroundColor Yellow
$process = Get-Process DeskGroup -ErrorAction SilentlyContinue
if ($process) {
    Write-Host "   ✓ DeskGroup is running (PID: $($process.Id))" -ForegroundColor Green
} else {
    Write-Host "   ✗ DeskGroup is NOT running!" -ForegroundColor Red
    exit
}

Write-Host ""
Write-Host "=== NEXT STEPS ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. You should see a DIAGNOSTIC MESSAGE BOX on your screen" -ForegroundColor White
Write-Host "   - Click OK to dismiss it" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Look for the TRAY ICON in your system tray (bottom-right)" -ForegroundColor White
Write-Host "   - May be in hidden icons (click ^ arrow)" -ForegroundColor Gray
Write-Host ""
Write-Host "3. RIGHT-CLICK the tray icon" -ForegroundColor White
Write-Host ""
Write-Host "4. Select 'New Group' from the menu" -ForegroundColor White
Write-Host ""
Write-Host "5. A WIDGET WINDOW should appear on your desktop!" -ForegroundColor Green
Write-Host ""
Write-Host "=== EXPLANATION ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Why 'Show All Groups' did nothing:" -ForegroundColor Yellow
Write-Host "  → You have 0 groups created (fresh install)" -ForegroundColor Gray
Write-Host "  → The command works, but there's nothing to show" -ForegroundColor Gray
Write-Host ""
Write-Host "After creating a group with 'New Group':" -ForegroundColor Yellow
Write-Host "  → Widget appears on desktop" -ForegroundColor Gray
Write-Host "  → You can then test 'Show All' / 'Hide All'" -ForegroundColor Gray
Write-Host "  → Drag and drop desktop shortcuts onto the widget" -ForegroundColor Gray
Write-Host ""
Write-Host "Press Enter to check tray icon menu..."
Read-Host
Write-Host ""
Write-Host "INSTRUCTIONS:" -ForegroundColor Cyan
Write-Host "1. RIGHT-CLICK the DeskGroup tray icon (in system tray)" -ForegroundColor White
Write-Host "2. You should see this menu:" -ForegroundColor White
Write-Host "   - Show All Groups" -ForegroundColor Gray
Write-Host "   - Hide All Groups" -ForegroundColor Gray
Write-Host "   - ───────────" -ForegroundColor Gray
Write-Host "   - New Group       ← CLICK THIS!" -ForegroundColor Green
Write-Host "   - ───────────" -ForegroundColor Gray
Write-Host "   - Sort Ungrouped Icons..." -ForegroundColor Gray
Write-Host "   - ───────────" -ForegroundColor Gray
Write-Host "   - Settings..." -ForegroundColor Gray
Write-Host "   - ───────────" -ForegroundColor Gray
Write-Host "   - Exit DeskGroup" -ForegroundColor Gray
Write-Host ""
Write-Host "3. Click 'New Group'" -ForegroundColor White
Write-Host "4. A widget window should appear somewhere on your desktop" -ForegroundColor White
Write-Host ""
Write-Host "Press Enter when you've created a group (or Ctrl+C to exit script)..."
Read-Host
Write-Host ""
Write-Host "Testing if group was created..." -ForegroundColor Yellow
Start-Sleep -Seconds 1

# Check if groups.json was created
$groupsFile = "$env:APPDATA\DeskGroup\groups.json"
if (Test-Path $groupsFile) {
    Write-Host "✓ Groups file exists!" -ForegroundColor Green
    $content = Get-Content $groupsFile -Raw | ConvertFrom-Json
    Write-Host "  Groups in file: $($content.Count)" -ForegroundColor Gray
    if ($content.Count -gt 0) {
        Write-Host "  Group name: $($content[0].name)" -ForegroundColor Gray
        Write-Host "  Position: ($($content[0].positionX), $($content[0].positionY))" -ForegroundColor Gray
        Write-Host "  IsVisible: $($content[0].isVisible)" -ForegroundColor Gray
    }
} else {
    Write-Host "✗ No groups file yet - try creating a group from the tray menu" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== DONE ===" -ForegroundColor Cyan
Write-Host "DeskGroup is running in the background." -ForegroundColor White
Write-Host "Check your system tray for the icon!" -ForegroundColor White
