# Quick Test Guide - Round 2 Fixes

## 🎯 Three Issues to Verify

1. ✅ **Groups 1 and 2 visible on startup** (Sticky mode fix)
2. ✅ **Fullscreen mode works** (Window doesn't disappear)
3. ✅ **Adding 2nd icon doesn't crash** (COM leak fix)

---

## 🧪 Test Script

### SETUP (Do Once)
```powershell
# Stop DeskGroup
Stop-Process -Name "DeskGroup" -Force -ErrorAction SilentlyContinue

# Clean AppData
Remove-Item -Recurse -Force "$env:APPDATA\DeskGroup" -ErrorAction SilentlyContinue

# Rebuild
dotnet build C:\Dev\DeskGroup\DeskGroup.sln -c Debug

# Start fresh
C:\Dev\DeskGroup\src\DeskGroup\bin\Debug\net10.0-windows\DeskGroup.exe
```

---

## TEST 1: Groups Visible on Startup (5 min)

### Steps
1. Right-click tray icon → **Create Group** (3 times)
   - You now have: Group 1, Group 2, Group 3

2. **Set Group Modes**:
   - Group 1: Right-click → Position → **Sticky (desktop layer)**
   - Group 2: Right-click → Position → **Sticky (desktop layer)**
   - Group 3: Leave as **Float** (default)

3. **Add colors for visibility** (optional but helpful):
   - Group 1: Change to Red
   - Group 2: Change to Green  
   - Group 3: Change to Blue

4. **Close DeskGroup** (right-click tray → Exit)

5. **Restart DeskGroup** (F5 in Visual Studio or run exe)

### ✅ PASS Criteria
- [ ] Group 1 (Sticky, Red) is VISIBLE on desktop
- [ ] Group 2 (Sticky, Green) is VISIBLE on desktop
- [ ] Group 3 (Float, Blue) is VISIBLE on top of other windows

### ❌ FAIL Indicators
- Groups 1 or 2 are invisible
- Only Group 3 is visible
- Groups flicker and disappear

### Debug Output to Look For
```
ApplyDockMode: Sticky for Group 1
  Attached to desktop: True
ApplyDockMode: Sticky for Group 2
  Attached to desktop: True
ApplyDockMode: Float for Group 3
  Float mode applied, shown
```

---

## TEST 2: Fullscreen Mode Works (2 min)

### Steps
1. **Double-click Group 1** to expand to Normal view

2. **Click fullscreen button** (or press F11)

3. **Observe**: Window should:
   - Expand to fill screen
   - Stay visible (not disappear!)
   - Be on top of other windows

4. **Press F11 again** (or click fullscreen button again)

5. **Observe**: Window should:
   - Return to normal size
   - Stay visible
   - Return to previous position

### ✅ PASS Criteria
- [ ] Fullscreen mode shows window (no disappearing)
- [ ] Window is on top in fullscreen
- [ ] Exit fullscreen works correctly
- [ ] Window returns to original size/position

### ❌ FAIL Indicators
- Window disappears when entering fullscreen
- Window is behind other windows
- Cannot exit fullscreen
- Window position lost after fullscreen

---

## TEST 3: Adding Multiple Icons No Crash (3 min)

### Steps
1. **Double-click Group 3** to expand to Normal view

2. **Find a desktop shortcut**:
   - Open File Explorer → Navigate to Desktop
   - Find any `.lnk` file (e.g., Chrome, Excel, etc.)

3. **Drag first icon** onto Group 3 window

4. **Observe Debug Output**:
   ```
   AddShortcut: C:\Users\...\Desktop\Icon1.lnk to Group 3
     Creating ViewModel for shortcut: Icon1
     Hiding desktop icon
     AddShortcut completed successfully
   ```

5. **✨ Critical Step: Drag SECOND icon** onto Group 3

6. **Watch for crash** (this is where it used to fail!)

7. **If no crash, drag a THIRD and FOURTH icon** to be sure

### ✅ PASS Criteria
- [ ] First icon adds successfully
- [ ] Second icon adds successfully (NO CRASH!)
- [ ] Third icon adds successfully
- [ ] Fourth icon adds successfully
- [ ] All icons appear in group
- [ ] Desktop icons are hidden

### ❌ FAIL Indicators
- App crashes on 2nd icon (heap corruption error)
- App freezes
- Icons don't appear in group
- Error code 0xc0000374 in output

### Debug Output to Look For
```
AddShortcut: C:\...\Icon1.lnk to Group 3
  Creating ViewModel for shortcut: Icon1
  Hiding desktop icon
  AddShortcut completed successfully

AddShortcut: C:\...\Icon2.lnk to Group 3
  Creating ViewModel for shortcut: Icon2
  Hiding desktop icon
  AddShortcut completed successfully

AddShortcut: C:\...\Icon3.lnk to Group 3
  Creating ViewModel for shortcut: Icon3
  Hiding desktop icon
  AddShortcut completed successfully
```

---

## 🎯 All Tests Pass?

If all three tests pass:
- ✅ **Sticky mode visibility**: FIXED
- ✅ **Fullscreen mode**: FIXED
- ✅ **Add icon crash**: FIXED

---

## 🔍 Troubleshooting

### If Group 1/2 Still Not Visible
1. Check Debug Output for "Attached to desktop: True"
2. Try switching Group 1 to Float mode (should appear)
3. Switch back to Sticky mode (should stay visible)

### If Fullscreen Still Disappears
1. Check if window went to another monitor
2. Try Alt+Tab to see if window is in task switcher
3. Check Debug Output for errors

### If Add Icon Still Crashes
1. Note which icon number causes crash
2. Check Debug Output for error message
3. Try different shortcut file types (.lnk, .url, .exe)

---

## 📊 Quick Status Check

| Test | Status | Time |
|------|--------|------|
| Groups visible on startup | ⬜ | 5 min |
| Fullscreen works | ⬜ | 2 min |
| Add multiple icons | ⬜ | 3 min |

**Total Testing Time**: ~10 minutes

---

## 🚀 After All Tests Pass

1. **Mark tests complete** ✅
2. **Check Debug Output** for clean logs
3. **Proceed to longer testing** (leave app running overnight)
4. **Report success** to team

---

## 📝 Notes Section

```
Test Date: _______________
Tested By: _______________

Results:
[ ] Test 1: Groups Visible - PASS / FAIL
[ ] Test 2: Fullscreen - PASS / FAIL
[ ] Test 3: Multiple Icons - PASS / FAIL

Issues Found:
_________________________________
_________________________________

Additional Observations:
_________________________________
_________________________________
```

---

**Generated**: $(Get-Date)  
**Version**: Round 2  
**Focus**: Visibility + Crash Prevention
