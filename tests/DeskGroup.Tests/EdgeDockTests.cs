using System;
using System.IO;
using System.Linq;
using System.Windows;
using DeskGroup.Models;
using DeskGroup.Services;
using DeskGroup.ViewModels;
using FluentAssertions;
using Xunit;

namespace DeskGroup.Tests;

public class EdgeDockTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly PersistenceService _persistence;
    private readonly UsageTrackingService _usageTracker;
    private readonly IconExtractorService _iconExtractor;
    private readonly DesktopIconService _desktopIconService;
    private readonly MonitorInfo _testMonitor;

    public EdgeDockTests()
    {
        _testDataDir = Path.Combine(Path.GetTempPath(), $"DeskGroupDockTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDataDir);
        Environment.SetEnvironmentVariable("DESKGROUP_TEST_DATA_DIR", _testDataDir);
        _persistence = new PersistenceService();
        _usageTracker = new UsageTrackingService(_persistence);
        _iconExtractor = new IconExtractorService(_testDataDir);
        _desktopIconService = new DesktopIconService(_testDataDir);
        _testMonitor = new MonitorInfo
        {
            DeviceName = @"\\.\DISPLAY1",
            IsPrimary = true,
            Bounds = new Rect(0, 0, 1920, 1080),
            WorkingArea = new Rect(0, 0, 1920, 1040)
        };
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDataDir))
                Directory.Delete(_testDataDir, true);
        }
        catch { }
    }

    [Fact]
    public void EdgeDockConfig_Defaults_AreLeftAutoHide1IconWideAnd16pxBubble()
    {
        var cfg = new EdgeDockConfig();
        cfg.Edge.Should().Be(DockEdge.Left);
        cfg.LockState.Should().Be(SideDockLockState.AutoHide);
        cfg.IsPinned.Should().BeFalse();
        cfg.ShowBubbleButton.Should().BeTrue();
        cfg.ShowOnEdgeHover.Should().BeTrue();
        cfg.BubbleSize.Should().Be(16);
        cfg.IconColumns.Should().Be(1);
    }

    [Fact]
    public void EdgeDockViewModel_SetEdge_RespectsPinState_WhileBubbleRemainsMovable()
    {
        var cfg = new EdgeDockConfig { Edge = DockEdge.Left, IsPinned = false };
        var settings = new AppSettings();
        var vm = new EdgeDockViewModel(cfg, _testMonitor, _usageTracker, _iconExtractor, _desktopIconService, _persistence, settings);

        // Unpinned -> can move to Top, Right, Bottom, Left
        vm.SetEdge(DockEdge.Right);
        vm.Edge.Should().Be(DockEdge.Right);
        cfg.Edge.Should().Be(DockEdge.Right);

        vm.SetEdge(DockEdge.Top);
        vm.Edge.Should().Be(DockEdge.Top);
        vm.IsHorizontalEdge.Should().BeTrue();
        vm.IsVerticalEdge.Should().BeFalse();

        // Pin it -> SetEdge should not move the dock
        vm.TogglePin();
        vm.IsPinned.Should().BeTrue();
        vm.SetEdge(DockEdge.Bottom);
        vm.Edge.Should().Be(DockEdge.Top);

        // Even when pinned, the bubble can be moved independently anywhere along any screen edge
        vm.BubbleEdgeOverride = DockEdge.Bottom;
        vm.BubblePositionRatio = 0.82;
        vm.EffectiveBubbleEdge.Should().Be(DockEdge.Bottom);
        vm.EffectiveBubbleRatio.Should().BeApproximately(0.82, 0.001);
        vm.Edge.Should().Be(DockEdge.Top); // Dock itself stays pinned to Top
    }

    [Fact]
    public void EdgeDockViewModel_ColumnSnappingAndHeaderLayout_WorkFor1AndMultiColumns()
    {
        var cfg = new EdgeDockConfig { Edge = DockEdge.Left, IconColumns = 1 };
        var settings = new AppSettings();
        var vm = new EdgeDockViewModel(cfg, _testMonitor, _usageTracker, _iconExtractor, _desktopIconService, _persistence, settings);

        // 1 icon wide -> IsSingleColumn is true (so Pin/Lock/Close is Row 1 and Title is Row 2)
        vm.IconColumns.Should().Be(1);
        vm.IsSingleColumn.Should().BeTrue();
        vm.IsMultiColumn.Should().BeFalse();
        double oneColWidth = vm.CalculateSnappedWidthForColumns(1);
        vm.PanelThickness.Should().Be(oneColWidth);

        // SnapWidthToColumns snaps arbitrary width to nearest exact icon column count
        double twoColWidth = vm.CalculateSnappedWidthForColumns(2);
        vm.SnapWidthToColumns(twoColWidth - 10).Should().Be(2);

        vm.SetIconColumns(2);
        vm.IconColumns.Should().Be(2);
        vm.IsSingleColumn.Should().BeFalse();
        vm.IsMultiColumn.Should().BeTrue();
        vm.PanelThickness.Should().Be(twoColWidth);

        // Per-dock icon resize updates column width & snapped thickness automatically
        vm.DisplayIconSize = 64;
        vm.EffectiveIconSize.Should().Be(64);
        vm.EffectiveTileSize.Should().Be(72);
        vm.SingleIconRowHeight.Should().Be(76);
        vm.PanelThickness.Should().Be(vm.CalculateSnappedWidthForColumns(2));
        DeskGroup.Controls.IconTile.ToolTipShowDelayMs.Should().Be(450);
    }

    [Fact]
    public void EdgeDockViewModel_ZeroOpacity_ClampsAlphaToPreventClickThrough()
    {
        var cfg = new EdgeDockConfig { PerDockOpacity = 0.0 };
        var settings = new AppSettings { GroupBackgroundOpacity = 0.0 };
        var vm = new EdgeDockViewModel(cfg, _testMonitor, _usageTracker, _iconExtractor, _desktopIconService, _persistence, settings);

        vm.EffectiveOpacity.Should().Be(0.0);
        // Alpha must be >= 1 so Windows layered window hit-testing never clicks through to windows underneath
        vm.TransparentAccentBrush.Color.A.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void HotkeyService_ParsesArrowKeyHotkeys_ForSidePanel()
    {
        HotkeyService.IsValidHotkeyString("Ctrl+Alt+Left").Should().BeTrue();
        HotkeyService.IsValidHotkeyString("Ctrl+Alt+Right").Should().BeTrue();
        HotkeyService.IsValidHotkeyString("Ctrl+Alt+Up").Should().BeTrue();
        HotkeyService.IsValidHotkeyString("Ctrl+Alt+Down").Should().BeTrue();
        HotkeyService.TryParseHotkey("Ctrl+Alt+Left", out uint mods, out uint vk).Should().BeTrue();
        mods.Should().NotBe(0);
        vk.Should().NotBe(0);
    }

    [Theory]
    [InlineData("MiddleClick", true)]
    [InlineData("XButton1", true)]
    [InlineData("XButton2", true)]
    [InlineData("ScrollUp", true)]
    [InlineData("ScrollDown", true)]
    [InlineData("Ctrl+MiddleClick", true)]
    [InlineData("Ctrl+RightClick", true)]
    [InlineData("Alt+RightClick", true)]
    [InlineData("Shift+LeftClick", true)]
    [InlineData("LeftClick", false)]
    [InlineData("RightClick", false)]
    public void HotkeyService_ValidatesMouseHotkeys_IncludingModifierClicks(string combo, bool expectedValid)
    {
        HotkeyService.IsMouseHotkeyString(combo).Should().Be(expectedValid);
    }

    [Fact]
    public void SettingsViewModel_SettingMouseHotkey_AutoUpgradesModeToBoth()
    {
        var settings = new AppSettings
        {
            ToggleAllHotkeyMode = HotkeyMode.Keyboard,
            ToggleAllHotkey = "Ctrl+F2",
            FullscreenHotkeyMode = HotkeyMode.Keyboard,
            FullscreenToggleHotkey = "F11",
            ToggleEdgeDockHotkeyMode = HotkeyMode.Keyboard,
            ToggleEdgeDockHotkey = "Ctrl+Alt+Left"
        };

        var vm = new SettingsViewModel(settings, _persistence, new StartupService());
        vm.ToggleAllMouseHotkey = "MiddleClick";
        vm.ToggleAllHotkeyMode.Should().Be(HotkeyMode.Both);

        vm.FullscreenMouseHotkey = "XButton1";
        vm.FullscreenHotkeyMode.Should().Be(HotkeyMode.Both);

        vm.ToggleEdgeDockMouseHotkey = "Ctrl+RightClick";
        vm.ToggleEdgeDockHotkeyMode.Should().Be(HotkeyMode.Both);
    }

    [Fact]
    public void EdgeDockViewModel_LockStates_ControlHoverAndOpenBehavior()
    {
        var cfg = new EdgeDockConfig { LockState = SideDockLockState.AutoHide, ShowOnEdgeHover = true };
        var settings = new AppSettings();
        var vm = new EdgeDockViewModel(cfg, _testMonitor, _usageTracker, _iconExtractor, _desktopIconService, _persistence, settings);

        vm.IsPanelOpen.Should().BeFalse();

        // Hover opens in AutoHide mode
        vm.RequestOpenFromHover();
        vm.IsPanelOpen.Should().BeTrue();

        // Leaving hover closes in AutoHide mode
        vm.RequestCloseFromHoverLeave();
        vm.IsPanelOpen.Should().BeFalse();

        // Lock Open -> immediately opens and does not close on hover leave
        vm.LockState = SideDockLockState.LockedOpen;
        vm.IsPanelOpen.Should().BeTrue();
        vm.RequestCloseFromHoverLeave();
        vm.IsPanelOpen.Should().BeTrue();

        // Clicking an icon closes the Edge Dock even when auto-close (AutoHide) is off
        vm.OnShortcutLaunched();
        vm.IsPanelOpen.Should().BeFalse();

        // Lock Closed -> immediately closes and does not open on hover
        vm.LockState = SideDockLockState.LockedClosed;
        vm.IsPanelOpen.Should().BeFalse();
        vm.RequestOpenFromHover();
        vm.IsPanelOpen.Should().BeFalse();

        // Bubble click still allows toggling when needed, and clicking an icon closes it
        vm.TogglePanel();
        vm.IsPanelOpen.Should().BeTrue();
        vm.OnShortcutLaunched();
        vm.IsPanelOpen.Should().BeFalse();
    }

    [Fact]
    public void EdgeDockViewModel_MoveShortcutToIndex_ReordersAndUpdatesSortOrder()
    {
        var cfg = new EdgeDockConfig();
        cfg.Shortcuts.Add(new ShortcutItem { DisplayName = "First", TargetPath = "a.exe", SortOrder = 0 });
        cfg.Shortcuts.Add(new ShortcutItem { DisplayName = "Second", TargetPath = "b.exe", SortOrder = 1 });
        cfg.Shortcuts.Add(new ShortcutItem { DisplayName = "Third", TargetPath = "c.exe", SortOrder = 2 });

        var vm = new EdgeDockViewModel(cfg, _testMonitor, _usageTracker, _iconExtractor, _desktopIconService, _persistence, new AppSettings());
        vm.AllShortcuts.Select(s => s.DisplayName).Should().ContainInOrder("First", "Second", "Third");

        // Drag "Third" to top (index 0)
        vm.MoveShortcutToIndex(vm.AllShortcuts[2], 0);
        vm.AllShortcuts.Select(s => s.DisplayName).Should().ContainInOrder("Third", "First", "Second");
        cfg.Shortcuts.Select(s => s.DisplayName).Should().ContainInOrder("Third", "First", "Second");
        cfg.Shortcuts.Select(s => s.SortOrder).Should().ContainInOrder(0, 1, 2);

        // Drag "Third" to bottom (index 2)
        vm.MoveShortcut(vm.AllShortcuts[0], vm.AllShortcuts[2]);
        vm.AllShortcuts.Select(s => s.DisplayName).Should().ContainInOrder("First", "Second", "Third");
        cfg.Shortcuts.Select(s => s.SortOrder).Should().ContainInOrder(0, 1, 2);
    }

    [Theory]
    [InlineData("v1.4.6", "1.4.6")]
    [InlineData("1.4.6", "1.4.6")]
    [InlineData("Release-v1.4.6-hotfix", "1.4.6")]
    [InlineData("DeskGroup v1.4.5 — First-run setup wizard", "1.4.5")]
    [InlineData("DeskGroup-Setup-1.4.6.exe", "1.4.6")]
    [InlineData("v1.0-all-fixes-complete", "1.0.0")]
    public void UpdateService_TryParseFlexibleVersion_HandlesVariedTagsAndNames(string input, string expectedNormalized)
    {
        UpdateService.TryParseFlexibleVersion(input, out var version, out var normalized).Should().BeTrue();
        normalized.Should().Be(expectedNormalized);
        version.Should().Be(Version.Parse(expectedNormalized));
    }
}
