# SIMPLE TEST - Just launch with debugger output

Write-Host "`n=== Launching DeskGroup with Debug Output ===`n" -ForegroundColor Cyan

# Stop any running
Stop-Process -Name DeskGroup -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "Starting DeskGroup..." -ForegroundColor Yellow
Write-Host "Watch Visual Studio Debug Output window for logs!`n" -ForegroundColor Green

# Start and attach DebugView if available, otherwise just start
Start-Process -FilePath "C:\Dev\DeskGroup\src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe"

Start-Sleep -Seconds 3

$proc = Get-Process DeskGroup -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "✓ DeskGroup running (PID: $($proc.Id))" -ForegroundColor Green
} else {
    Write-Host "✗ Failed to start" -ForegroundColor Red
    exit
}

Write-Host "`nCHECK YOUR SCREEN:" -ForegroundColor Cyan
Write-Host "1. Diagnostic message box should appear" -ForegroundColor White
Write-Host "2. Tray icon in system tray (bottom-right)" -ForegroundColor White
Write-Host "3. Desktop widget windows (if groups exist)" -ForegroundColor White

Write-Host "`nCHECK DEBUG OUTPUT IN VISUAL STUDIO:" -ForegroundColor Cyan
Write-Host "View → Output → Show output from: Debug" -ForegroundColor White
Write-Host "`nLook for these log lines:" -ForegroundColor Gray
Write-Host "  • === DeskGroup Starting ===" -ForegroundColor DarkGray
Write-Host "  • MainViewModel initialized. Groups: N" -ForegroundColor DarkGray
Write-Host "  • Creating N group windows..." -ForegroundColor DarkGray
Write-Host "  • GroupWidget.OnLoaded called for: [group name]" -ForegroundColor DarkGray
Write-Host "  • Window.IsVisible: True" -ForegroundColor DarkGray

Write-Host "`n" -ForegroundColor Cyan
Write-Host "If you see groups loaded but no windows, try:" -ForegroundColor Yellow
Write-Host "  1. Right-click tray icon → 'Show All Groups'" -ForegroundColor Gray
Write-Host "  2. Check near top-left corner (100, 100)" -ForegroundColor Gray
Write-Host "  3. Share Debug Output logs with me" -ForegroundColor Gray
