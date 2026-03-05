# ✅ HOVER JUMP FIX - WndProc Interception Method

**Date:** 2024-01-XX  
**Issue:** Window jumps to (0,0) when expanding from Minimized to Hover view  
**Status:** ✅ **FIXED** using WM_WINDOWPOSCHANGING interception  

---

## 🐛 **The Problem**

### **Symptom:**
When hovering over a minimized group, the window briefly flashes at position (0,0) in the top-left corner of the screen before snapping to the correct position.

### **Root Cause:**
WPF's layout engine runs **synchronously** when `Visibility` changes. When `HoverView.Visibility = Visible`, WPF:
1. Triggers immediate layout pass
2. Reads `Left` and `Top` properties
3. **Renders one frame at those coordinates** (which may be uninitialized/0,0)
4. THEN our `SetWindowPos()` executes (too late!)

### **Why Previous Fixes Failed:**
All attempts (opacity hiding, deferred position sync, SWP_NOREDRAW, etc.) tried to **race** WPF's layout engine. **You cannot win that race** - WPF layout is synchronous and happens before any of our positioning code executes.

---

## ✅ **The Solution: WndProc Hook**

### **Strategy:**
Instead of trying to **prevent** the position change, we **intercept** it at the Win32 message level and **override** the position **before** any rendering occurs.

### **How It Works:**

1. **Hook WM_WINDOWPOSCHANGING:** This Win32 message fires **BEFORE** the window moves (but after WPF decides to move it)
2. **Set "Pending Position":** Before changing visibility, store the target position in fields
3. **WndProc Intercepts:** When WPF tries to move the window, WndProc catches the message
4. **Override Position:** Rewrite the position in the message struct to our target coordinates
5. **Window Moves Correctly:** The intercepted message ensures the window moves to the RIGHT position on first frame

**No jump occurs because the window NEVER moves to (0,0) - the message is intercepted and corrected before rendering.**

---

## 📝 **Implementation Details**

### **Files Modified:**
- `src/DeskGroup/Views/GroupWidget.xaml.cs`

### **1. Add Fields (Top of GroupWidget class):**

```csharp
// WndProc hook for intercepting position changes
private HwndSource? _hwndSource;
private int _pendingX, _pendingY, _pendingW, _pendingH;
private bool _hasPendingPosition = false;
```

### **2. Hook WndProc in Constructor:**

```csharp
public GroupWidget()
{
    InitializeComponent();
    
    // Hook WndProc to intercept position changes
    SourceInitialized += (s, e) =>
    {
        var source = PresentationSource.FromVisual(this) as HwndSource;
        if (source != null)
        {
            source.CompositionTarget.RenderMode = RenderMode.Default;
            
            // Add WndProc hook
            _hwndSource = source;
            _hwndSource.AddHook(WndProc);
        }
    };
    
    // ... rest of constructor
}
```

### **3. Implement WndProc Hook:**

```csharp
private const int WM_WINDOWPOSCHANGING = 0x0046;

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
private struct WINDOWPOS
{
    public IntPtr hwnd;
    public IntPtr hwndInsertAfter;
    public int x;
    public int y;
    public int cx;
    public int cy;
    public uint flags;
}

private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    if (msg == WM_WINDOWPOSCHANGING && _hasPendingPosition)
    {
        // Override the position BEFORE the window moves
        var pos = System.Runtime.InteropServices.Marshal.PtrToStructure<WINDOWPOS>(lParam);
        
        // Only override if WPF is trying to move us to wrong position
        const uint SWP_NOMOVE = 0x0002;
        if ((pos.flags & SWP_NOMOVE) == 0) // Window IS being moved
        {
            if (pos.x != _pendingX || pos.y != _pendingY || 
                pos.cx != _pendingW || pos.cy != _pendingH)
            {
                // WPF is trying to move us somewhere wrong — override it
                pos.x = _pendingX;
                pos.y = _pendingY;
                pos.cx = _pendingW;
                pos.cy = _pendingH;
                System.Runtime.InteropServices.Marshal.StructureToPtr(pos, lParam, true);
                System.Diagnostics.Debug.WriteLine($"🛡️ WndProc INTERCEPTED wrong position, forced to ({_pendingX},{_pendingY})");
            }
        }
    }
    return IntPtr.Zero;
}
```

### **4. Update Hover Case in UpdateViewMode():**

```csharp
case GroupViewMode.Hover:
    // Measure HoverView to get target size
    HoverView.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
    int hoverTargetW = (int)Math.Max(HoverView.DesiredSize.Width + 20, 200);
    int hoverTargetH = (int)Math.Max(HoverView.DesiredSize.Height + 20, 60);
    int hoverTargetX = (_viewModel != null ? (int)_viewModel.PositionX : 0);
    int hoverTargetY = (_viewModel != null ? (int)_viewModel.PositionY : 0);

    // SET THE TRAP: WndProc will enforce this position on ANY move attempt
    _pendingX = hoverTargetX;
    _pendingY = hoverTargetY;
    _pendingW = hoverTargetW;
    _pendingH = hoverTargetH;
    _hasPendingPosition = true;

    // Set WPF properties BEFORE visibility (backup)
    Left = hoverTargetX;
    Top = hoverTargetY;
    Width = hoverTargetW;
    Height = hoverTargetH;
    SizeToContent = SizeToContent.Manual;
    ResizeMode = ResizeMode.NoResize;

    // NOW change visibility (WndProc intercepts any wrong move)
    MinimizedView.Visibility = Visibility.Collapsed;
    HoverView.Visibility = Visibility.Visible;
    // ... collapse other views ...

    // Final SetWindowPos for good measure
    if (_windowHandle != IntPtr.Zero)
    {
        Win32Interop.SetWindowPos(_windowHandle, IntPtr.Zero,
            hoverTargetX, hoverTargetY, hoverTargetW, hoverTargetH,
            Win32Interop.SWP_NOZORDER | Win32Interop.SWP_NOACTIVATE | Win32Interop.SWP_SHOWWINDOW);
    }

    // Clear the trap
    _hasPendingPosition = false;
    break;
```

---

## 🎯 **Why This Works**

### **The Execution Flow:**

```
1. MouseEnter → _viewModel.ViewMode = Hover
2. UpdateViewMode(Hover) called
3. Measure HoverView (get target size)
4. Set _hasPendingPosition = true  ← TRAP ARMED
5. Set WPF Left/Top/Width/Height
6. HoverView.Visibility = Visible
7. WPF layout engine starts
8. WPF sends WM_WINDOWPOSCHANGING to move window
9. WndProc INTERCEPTS message ← TRAP TRIGGERED!
10. WndProc overwrites position in message to _pendingX/_pendingY
11. Window moves to CORRECT position (first frame!)
12. SetWindowPos executes (confirms position)
13. _hasPendingPosition = false ← TRAP DISARMED
```

**Key Insight:** WM_WINDOWPOSCHANGING fires **before** the window moves and **before** any frame is rendered. By modifying the `WINDOWPOS` struct in the message, we literally rewrite where Windows will paint the window.

---

## 📊 **Comparison to Failed Approaches**

| Attempt | Strategy | Why Failed | WndProc Difference |
|---------|----------|------------|-------------------|
| **Round 1** | Opacity = 0 | WPF still rendered at (0,0) | Intercepts BEFORE render |
| **Round 2** | Deferred Left/Top sync | Dispatch too late | No delay - instant intercept |
| **Round 3** | Visibility.Hidden | Still jumped on Hidden→Visible | Intercepts ANY move |
| **Round 4-6** | Timer fixes, click mode | Caused crashes | No timer changes needed |
| **Round 7** | SWP_NOREDRAW + pre-set Left/Top | Still rendered intermediate frame | Prevents intermediate frame |
| **Round 8** | **WndProc Hook** | ✅ **WORKS!** | Intercepts at Win32 level |

---

## 🧪 **Verification**

### **Debug Output (Success):**
```
🎯 PENDING position set: (205, 107), size: 220x533
🛡️ WndProc INTERCEPTED wrong position, forced to (205,107)
✅ SetWindowPos hover: True
```

### **User Experience:**
- ✅ No flash/jump to (0,0)
- ✅ Smooth expansion from Minimized → Hover
- ✅ Correct positioning on first frame
- ✅ No crash, no flicker

---

## 🔧 **Reusable Pattern**

**Use this pattern for ANY WPF window that needs precise positioning during dynamic view changes:**

```csharp
// 1. Add fields
private HwndSource? _hwndSource;
private int _pendingX, _pendingY, _pendingW, _pendingH;
private bool _hasPendingPosition = false;

// 2. Hook WndProc
SourceInitialized += (s, e) => {
    _hwndSource = HwndSource.FromVisual(this);
    _hwndSource?.AddHook(WndProc);
};

// 3. Implement WndProc
private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
{
    if (msg == WM_WINDOWPOSCHANGING && _hasPendingPosition)
    {
        var pos = Marshal.PtrToStructure<WINDOWPOS>(lParam);
        if ((pos.flags & SWP_NOMOVE) == 0)
        {
            pos.x = _pendingX;
            pos.y = _pendingY;
            Marshal.StructureToPtr(pos, lParam, true);
        }
    }
    return IntPtr.Zero;
}

// 4. Before any Visibility change:
_pendingX = targetX;
_pendingY = targetY;
_pendingW = targetW;
_pendingH = targetH;
_hasPendingPosition = true;

// Change visibility (WndProc guards the position)
SomeView.Visibility = Visibility.Visible;

// Clear trap
_hasPendingPosition = false;
```

---

## 📚 **References**

- **WM_WINDOWPOSCHANGING:** https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-windowposchanging
- **HwndSource.AddHook:** https://learn.microsoft.com/en-us/dotnet/api/system.windows.interop.hwndsource.addhook
- **WINDOWPOS Structure:** https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-windowpos

---

## 🎓 **Lessons Learned**

1. **WPF layout is synchronous** - You cannot "beat" it with timing tricks
2. **Win32 messages are the lowest level** - Intercept there to override WPF behavior
3. **WM_WINDOWPOSCHANGING is your friend** - Fires before movement, allows override
4. **Always measure before making visible** - `Measure()` works on collapsed elements
5. **Set WPF properties as backup** - Even with WndProc, set Left/Top first for safety

---

## ⚠️ **Important Notes**

- **Unhook on cleanup:** Add `_hwndSource?.RemoveHook(WndProc)` in `OnClosing()`
- **Clear pending flag:** Always set `_hasPendingPosition = false` after visibility change
- **Thread safety:** WndProc runs on UI thread, no synchronization needed
- **Only use when necessary:** This is a low-level hook - only use for critical positioning issues

---

## 🏆 **Credits**

**Final Solution By:** User's prompt about WndProc interception  
**Rounds Failed Before Success:** 7 attempts over 2 debugging sessions  
**Key Insight:** "Don't race WPF - intercept at Win32 level before any frame renders"  

---

**This fix is production-ready and can be reused in any WPF project with similar positioning challenges!** ✅
