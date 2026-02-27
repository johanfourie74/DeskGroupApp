# Emergency Window Visibility Fix

Write-Host "`n=== Emergency Window Visibility Test ===`n" -ForegroundColor Cyan

# Check if running
$proc = Get-Process DeskGroup -ErrorAction SilentlyContinue
if (-not $proc) {
    Write-Host "DeskGroup is not running. Start it first." -ForegroundColor Red
    exit
}

Write-Host "DeskGroup is running (PID: $($proc.Id))" -ForegroundColor Green

# Check groups file
$groupsFile = "$env:APPDATA\DeskGroup\groups.json"
if (-not (Test-Path $groupsFile)) {
    Write-Host "No groups file found. Create a group first." -ForegroundColor Red
    exit
}

$groups = Get-Content $groupsFile -Raw | ConvertFrom-Json
Write-Host "Groups in file: $(@($groups).Count)" -ForegroundColor Green

foreach ($g in $groups) {
    Write-Host "`nGroup: $($g.name)" -ForegroundColor White
    Write-Host "  IsVisible: $($g.isVisible)" -ForegroundColor Gray
    Write-Host "  ViewMode: $($g.viewMode)" -ForegroundColor Gray
    Write-Host "  DockMode: $($g.dockMode)" -ForegroundColor Gray
    Write-Host "  Position: ($($g.positionX), $($g.positionY))" -ForegroundColor Gray
    Write-Host "  Size: $($g.width) x $($g.height)" -ForegroundColor Gray
}

Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "DIAGNOSTIC ACTIONS:" -ForegroundColor White
Write-Host ("=" * 60) -ForegroundColor Cyan

Write-Host "`n1. CHECK SCREEN LOCATION (100, 100)" -ForegroundColor Yellow
Write-Host "   → Look at TOP-LEFT corner of your primary monitor" -ForegroundColor Gray
Write-Host "   → About 100 pixels from left edge, 100 pixels from top" -ForegroundColor Gray
Write-Host "   → You should see a small colored pill/badge" -ForegroundColor Gray

Write-Host "`n2. THE WIDGET IS IN 'MINIMIZED' MODE" -ForegroundColor Yellow
Write-Host "   → It looks like a small pill (60-200px wide, ~40px tall)" -ForegroundColor Gray
Write-Host "   → NOT a full window - just a compact badge" -ForegroundColor Gray
Write-Host "   → Has the group name and icon count" -ForegroundColor Gray

Write-Host "`n3. THE WIDGET IS IN 'STICKY' MODE" -ForegroundColor Yellow
Write-Host "   → Attached to desktop layer (behind all windows)" -ForegroundColor Gray
Write-Host "   → MINIMIZE or MOVE all your windows to see it!" -ForegroundColor Gray

Write-Host "`n4. TRY SWITCHING TO FLOAT MODE" -ForegroundColor Yellow
Write-Host "   → Right-click tray icon → 'New Group'" -ForegroundColor Gray
Write-Host "   → New groups default to FLOAT mode (on top of windows)" -ForegroundColor Gray
Write-Host "   → You should see it immediately!" -ForegroundColor Gray

Write-Host "`n5. FORCE TO NORMAL VIEW" -ForegroundColor Yellow
Write-Host "   → We can edit groups.json to change viewMode to 'normal'" -ForegroundColor Gray
Write-Host "   → This makes the widget larger and easier to see" -ForegroundColor Gray

Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
$answer = Read-Host "Do you want to force all groups to NORMAL view mode? (y/n)"

if ($answer -eq 'y') {
    Write-Host "`nBacking up groups.json..." -ForegroundColor Yellow
    Copy-Item $groupsFile "$groupsFile.backup" -Force
    
    Write-Host "Modifying groups to use NORMAL view mode..." -ForegroundColor Yellow
    $groups | ForEach-Object { $_.viewMode = "normal"; $_.dockMode = "float" }
    $groups | ConvertTo-Json -Depth 10 | Set-Content $groupsFile
    
    Write-Host "✓ Modified. Restart DeskGroup to see changes." -ForegroundColor Green
    Write-Host "`nRestarting DeskGroup..." -ForegroundColor Yellow
    
    Stop-Process -Name DeskGroup -Force
    Start-Sleep -Seconds 2
    Start-Process "C:\Dev\DeskGroup\src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe"
    Start-Sleep -Seconds 3
    
    Write-Host "✓ DeskGroup restarted with all groups in NORMAL + FLOAT mode" -ForegroundColor Green
    Write-Host "  → Check your screen at (100, 100)" -ForegroundColor Gray
    Write-Host "  → Widgets should now be LARGE and ON TOP of windows" -ForegroundColor Gray
}

Write-Host "`nDone.`n" -ForegroundColor Cyan
