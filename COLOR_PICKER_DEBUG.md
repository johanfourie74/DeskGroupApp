# Color Picker Crash Investigation

## Issues Reported
1. ✅ Groups not visible by default → **FIXED**: Changed DefaultDockMode to Float
2. ⚠️ Color swatches invisible (but work on hover) → **PARTIALLY FIXED**: Explicit size added
3. 🚨 App crashes when changing color on Group 1 → **INVESTIGATING**

## Fixes Applied

### Fix 1: Default Dock Mode
Changed `AppSettings.DefaultDockMode` from `Sticky` to `Float`:
- New groups now default to Float mode (visible on top)
- Existing groups still load with saved mode

### Fix 2: Color Swatch Rendering
Fixed ColorPickerButton.xaml:
- Added explicit Width/Height to button in DataTemplate
- Changed Background binding to use Self RelativeSource
- Added exception handling to ColorSwatch_Click

### Fix 3: Exception Handling for Color Changes
Added in GroupViewModel:
- `OnAccentColorHexChanged` partial method to handle property changes
- Detailed logging in `ApplyAccentColor`
- Try-catch around brush creation

## Testing Instructions

### Test 1: Color Picker Visibility
1. Start DeskGroup (F5)
2. Right-click any group widget
3. Look for color button (circle with current color)
4. Click it
5. **Expected:** Grid of color swatches appears (6x4 grid, ~28px each)
6. **Report:** Can you see the colored squares now?

### Test 2: Color Change without Crash
1. Click a color swatch (any color except the current one)
2. Widget should change color
3. **Watch Debug Output** for these logs:
   ```
   OnAccentColorHexChanged: #FF8C00
   ApplyAccentColor: #FF8C00
     Successfully created brush for color: #FFFF8C00
   ```
4. **If crash occurs:** VS will break at exception - share the error message

### Test 3: Repeat for Each Group
Test color changes on:
- Group 3: ✅ Worked (you reported)
- Group 2: ✅ Worked (you reported)
- Group 1: 🚨 Crashed (investigate why Group 1 is different)

## Specific Investigation: Why Group 1 Crashes

**Hypothesis:** Group 1 might have:
- Invalid color hex in groups.json
- Different ViewModel state than Group 2/3
- Different bindings or resources

**Debug steps:**
1. Check groups.json for Group 1's accent color
2. Compare Group 1 data vs Group 2/3
3. When crash occurs, note:
   - Exception message
   - Stack trace
   - What color was being selected

## Expected Debug Output (No Crash)

When you click a color swatch on ANY group:
```
ColorSwatch_Click: #FF8C00
OnAccentColorHexChanged: #FF8C00
ApplyAccentColor: #FF8C00
  Successfully created brush for color: #FFFF8C00
```

## Expected Debug Output (Crash)

If crash occurs, VS debugger will show:
```
Exception thrown: 'System.XXXException' in DeskGroup.dll
[Exception details here]
```

## Quick Test Now

Run this PowerShell to check groups.json for color anomalies:

```powershell
$groups = Get-Content "$env:APPDATA\DeskGroup\groups.json" -Raw | ConvertFrom-Json
$groups | ForEach-Object -Begin { $i = 1 } -Process {
    Write-Host "Group $i`: $($_.name)" -ForegroundColor Cyan
    Write-Host "  AccentColor: $($_.accentColor)" -ForegroundColor Gray
    $i++
}
```

This will show if Group 1 has a different/invalid color hex.

## Next Steps

1. **Run F5 in Visual Studio** (debugger attached)
2. Use "🔧 Force Visible" to show all groups
3. Try changing color on each group
4. **When crash happens on Group 1:**
   - VS will pause and show exception
   - Share the exception message + stack trace with me
5. Also report: can you see color swatches now or still invisible?
