$DebugPreference = "Continue"
$VerbosePreference = "Continue"

Write-Host "`n=== DeskGroup Diagnostic Runner ===`n" -ForegroundColor Cyan

# Stop existing
Write-Host "Stopping any running instances..." -ForegroundColor Yellow
Stop-Process -Name DeskGroup -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

# Check groups file
Write-Host "`nChecking existing data..." -ForegroundColor Yellow
$groupsFile = "$env:APPDATA\DeskGroup\groups.json"
if (Test-Path $groupsFile) {
    $json = Get-Content $groupsFile -Raw
    $groups = $json | ConvertFrom-Json
    Write-Host "  Found groups file with $(@($groups).Count) group(s)" -ForegroundColor Green
    
    foreach ($g in $groups) {
        Write-Host "    - Name: $($g.name)" -ForegroundColor Gray
        Write-Host "      Position: ($($g.positionX), $($g.positionY))" -ForegroundColor Gray
        Write-Host "      IsVisible: $($g.isVisible)" -ForegroundColor Gray
        Write-Host "      ViewMode: $($g.viewMode)" -ForegroundColor Gray
        Write-Host "      DockMode: $($g.dockMode)" -ForegroundColor Gray
    }
} else {
    Write-Host "  No groups file found (fresh start)" -ForegroundColor Gray
}

# Start app
Write-Host "`nStarting DeskGroup..." -ForegroundColor Yellow
$exePath = "C:\Dev\DeskGroup\src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe"
Start-Process -FilePath $exePath
Start-Sleep -Seconds 3

# Verify running
Write-Host "`nVerifying process..." -ForegroundColor Yellow
$proc = Get-Process DeskGroup -ErrorAction SilentlyContinue
if ($proc) {
    Write-Host "  ✓ DeskGroup is running (PID: $($proc.Id))" -ForegroundColor Green
    Write-Host "    StartTime: $($proc.StartTime)" -ForegroundColor Gray
    Write-Host "    MainWindowHandle: $($proc.MainWindowHandle)" -ForegroundColor Gray
    Write-Host "    MainWindowTitle: '$($proc.MainWindowTitle)'" -ForegroundColor Gray
} else {
    Write-Host "  ✗ DeskGroup is NOT running!" -ForegroundColor Red
    exit
}

Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "NEXT STEPS:" -ForegroundColor White
Write-Host ("=" * 60) -ForegroundColor Cyan

Write-Host "`n1. Check for DIAGNOSTIC MESSAGE BOX on your screen" -ForegroundColor Yellow
Write-Host "   → Should show group count and instructions" -ForegroundColor Gray

Write-Host "`n2. Look for TRAY ICON in system tray (bottom-right)" -ForegroundColor Yellow
Write-Host "   → May be hidden - click ^ arrow" -ForegroundColor Gray

if (@($groups).Count -gt 0) {
    Write-Host "`n3. Your desktop should have WIDGET WINDOWS visible" -ForegroundColor Yellow
    Write-Host "   → Check position: ($($groups[0].positionX), $($groups[0].positionY))" -ForegroundColor Gray
    Write-Host "   → If you don't see them, they might be:" -ForegroundColor Gray
    Write-Host "      • Behind other windows (sticky mode)" -ForegroundColor Gray
    Write-Host "      • Off-screen (wrong monitor)" -ForegroundColor Gray
    Write-Host "      • Minimized to a small pill" -ForegroundColor Gray
    
    Write-Host "`n4. Try these actions from tray icon menu:" -ForegroundColor Yellow
    Write-Host "   → Right-click tray icon" -ForegroundColor Gray
    Write-Host "   → Click 'Show All Groups' (refreshes visibility)" -ForegroundColor Gray
    Write-Host "   → Look near top-left of screen at (100, 100)" -ForegroundColor Gray
} else {
    Write-Host "`n3. CREATE YOUR FIRST GROUP:" -ForegroundColor Yellow
    Write-Host "   → Right-click tray icon" -ForegroundColor Gray
    Write-Host "   → Select 'New Group'" -ForegroundColor Gray
    Write-Host "   → Widget window should appear!" -ForegroundColor Gray
}

Write-Host "`n" + ("=" * 60) -ForegroundColor Cyan
Write-Host "`nPress Enter when ready to check window status..."
Read-Host

# Check for windows
Write-Host "`nChecking for DeskGroup windows..." -ForegroundColor Yellow
Add-Type @"
    using System;
    using System.Runtime.InteropServices;
    using System.Text;
    public class WindowChecker {
        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll")] public static extern int GetClassName(IntPtr hWnd, StringBuilder className, int maxCount);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    }
"@

$deskGroupPid = $proc.Id
$foundWindows = @()

$callback = {
    param($hwnd, $lParam)
    
    $pid = 0
    [WindowChecker]::GetWindowThreadProcessId($hwnd, [ref]$pid) | Out-Null
    
    if ($pid -eq $deskGroupPid) {
        $title = New-Object System.Text.StringBuilder 256
        [WindowChecker]::GetWindowText($hwnd, $title, 256) | Out-Null
        
        $className = New-Object System.Text.StringBuilder 256
        [WindowChecker]::GetClassName($hwnd, $className, 256) | Out-Null
        
        $isVisible = [WindowChecker]::IsWindowVisible($hwnd)
        
        $script:foundWindows += [PSCustomObject]@{
            HWND = $hwnd.ToString()
            Title = $title.ToString()
            ClassName = $className.ToString()
            Visible = $isVisible
        }
    }
    return $true
}

[WindowChecker]::EnumWindows($callback, [IntPtr]::Zero) | Out-Null

if ($foundWindows.Count -gt 0) {
    Write-Host "  Found $($foundWindows.Count) window(s):" -ForegroundColor Green
    $foundWindows | Format-Table -AutoSize | Out-String | Write-Host -ForegroundColor Gray
} else {
    Write-Host "  No windows found for DeskGroup process" -ForegroundColor Red
}

Write-Host "`nDeskGroup is running. Check your screen for widgets!" -ForegroundColor Cyan
