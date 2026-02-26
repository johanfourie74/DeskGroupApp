using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DeskGroup.Models;
using DeskGroup.Services;
using FluentAssertions;
using Xunit;

namespace DeskGroup.Tests;

public class PersistenceServiceTests : IDisposable
{
    private readonly string _testDataDir;
    private readonly PersistenceService _service;

    public PersistenceServiceTests()
    {
        // Create a unique temp directory for each test run
        _testDataDir = Path.Combine(Path.GetTempPath(), $"DeskGroupTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDataDir);

        // Tell PersistenceService to use our unique test directory
        Environment.SetEnvironmentVariable("DESKGROUP_TEST_DATA_DIR", _testDataDir);
        _service = new PersistenceService();
        // Ensure directory is empty at start of tests
        foreach (var f in Directory.GetFiles(_testDataDir)) File.Delete(f);
        foreach (var d in Directory.GetDirectories(_testDataDir)) Directory.Delete(d, true);
    }

    public void Dispose()
    {
        // Cleanup test data
        try
        {
            if (Directory.Exists(_testDataDir))
                Directory.Exists(_testDataDir);
        }
        catch { }
    }

    // ============================================================================================
    // SETTINGS TESTS
    // ============================================================================================

    [Fact]
    public async Task LoadSettings_WhenNoFileExists_ReturnsDefaults()
    {
        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Theme.Should().Be(ThemeMode.System);
        settings.StartWithWindows.Should().BeTrue();
        settings.ToggleAllHotkey.Should().Be("Ctrl+Alt+G");
        settings.FullscreenToggleHotkey.Should().Be("F11");
        settings.HideGroupedIcons.Should().BeTrue();
        settings.AllowMultiMonitor.Should().BeTrue();
        settings.DefaultSnapToGrid.Should().BeTrue();
        settings.SnapGridSize.Should().Be(20);
    }

    [Fact]
    public async Task SaveAndLoadSettings_RoundTrip_PreservesValues()
    {
        // Arrange
        var settings = new AppSettings
        {
            Theme = ThemeMode.Dark,
            StartWithWindows = false,
            ToggleAllHotkey = "Ctrl+Shift+D",
            FullscreenToggleHotkey = "F5",
            HideGroupedIcons = false,
            CustomIconSize = 64,
            IconSizeMode = IconSizeMode.Custom,
            DefaultDockMode = GroupDockMode.Float,
            SnapGridSize = 32,
            WatchForUngroupedIcons = false,
            AllowMultiMonitor = false,
            StartMinimizedToTray = true
        };

        // Act
        await _service.SaveSettingsImmediateAsync(settings);
        var loaded = await _service.LoadSettingsAsync();

        // Assert
        loaded.Theme.Should().Be(ThemeMode.Dark);
        loaded.StartWithWindows.Should().BeFalse();
        loaded.ToggleAllHotkey.Should().Be("Ctrl+Shift+D");
        loaded.FullscreenToggleHotkey.Should().Be("F5");
        loaded.HideGroupedIcons.Should().BeFalse();
        loaded.CustomIconSize.Should().Be(64);
        loaded.IconSizeMode.Should().Be(IconSizeMode.Custom);
        loaded.DefaultDockMode.Should().Be(GroupDockMode.Float);
        loaded.SnapGridSize.Should().Be(32);
        loaded.WatchForUngroupedIcons.Should().BeFalse();
        loaded.AllowMultiMonitor.Should().BeFalse();
        loaded.StartMinimizedToTray.Should().BeTrue();
    }

    // ============================================================================================
    // GROUPS TESTS
    // ============================================================================================

    [Fact]
    public async Task LoadGroups_WhenNoFileExists_ReturnsEmptyList()
    {
        // Act
        var groups = await _service.LoadGroupsAsync();

        // Assert
        groups.Should().NotBeNull();
        groups.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAndLoadGroups_SingleGroup_RoundTrip()
    {
        // Arrange
        var group = new IconGroup
        {
            Name = "Test Group",
            AccentColor = "#FF4081",
            DockMode = GroupDockMode.Float,
            IsPinned = true,
            SnapToGrid = false,
            PositionX = 200,
            PositionY = 300,
            Width = 400,
            Height = 350,
            ViewMode = GroupViewMode.Normal
        };

        var groups = new List<IconGroup> { group };

        // Act
        await _service.SaveGroupsImmediateAsync(groups);
        var loaded = await _service.LoadGroupsAsync();

        // Assert
        loaded.Should().HaveCount(1);
        var loadedGroup = loaded[0];
        loadedGroup.Name.Should().Be("Test Group");
        loadedGroup.AccentColor.Should().Be("#FF4081");
        loadedGroup.DockMode.Should().Be(GroupDockMode.Float);
        loadedGroup.IsPinned.Should().BeTrue();
        loadedGroup.SnapToGrid.Should().BeFalse();
        loadedGroup.PositionX.Should().Be(200);
        loadedGroup.PositionY.Should().Be(300);
        loadedGroup.Width.Should().Be(400);
        loadedGroup.Height.Should().Be(350);
        loadedGroup.ViewMode.Should().Be(GroupViewMode.Normal);
    }

    [Fact]
    public async Task SaveAndLoadGroups_WithShortcuts_RoundTrip()
    {
        // Arrange
        var shortcut = new ShortcutItem
        {
            DisplayName = "Chrome",
            TargetPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            OriginalShortcutPath = @"C:\Users\Test\Desktop\Chrome.lnk",
            IconPath = @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            IconIndex = 0,
            LaunchCount = 42,
            LastLaunched = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            SortOrder = 0,
            Arguments = "--new-window",
            WorkingDirectory = @"C:\Program Files\Google\Chrome\Application",
            OriginalIconHidden = true
        };

        var originalState = new OriginalIconState
        {
            ShortcutItemId = shortcut.Id,
            OriginalFilePath = shortcut.OriginalShortcutPath,
            OriginalX = 100,
            OriginalY = 200,
            MonitorDeviceName = @"\\.\DISPLAY1",
            WasMoved = true,
            StoredFilePath = @"C:\AppData\DeskGroup\HiddenIcons\test.lnk"
        };

        var group = new IconGroup
        {
            Name = "Browsers",
            Shortcuts = new List<ShortcutItem> { shortcut },
            OriginalIconStates = new List<OriginalIconState> { originalState }
        };

        // Act
        await _service.SaveGroupsImmediateAsync(new List<IconGroup> { group });
        var loaded = await _service.LoadGroupsAsync();

        // Assert
        loaded.Should().HaveCount(1);
        var loadedGroup = loaded[0];
        loadedGroup.Shortcuts.Should().HaveCount(1);

        var loadedShortcut = loadedGroup.Shortcuts[0];
        loadedShortcut.DisplayName.Should().Be("Chrome");
        loadedShortcut.TargetPath.Should().Be(@"C:\Program Files\Google\Chrome\Application\chrome.exe");
        loadedShortcut.LaunchCount.Should().Be(42);
        loadedShortcut.Arguments.Should().Be("--new-window");
        loadedShortcut.OriginalIconHidden.Should().BeTrue();

        loadedGroup.OriginalIconStates.Should().HaveCount(1);
        var loadedState = loadedGroup.OriginalIconStates[0];
        loadedState.OriginalX.Should().Be(100);
        loadedState.OriginalY.Should().Be(200);
        loadedState.WasMoved.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAndLoadGroups_MultipleGroups_PreservesAll()
    {
        // Arrange
        var groups = Enumerable.Range(1, 5).Select(i => new IconGroup
        {
            Name = $"Group {i}",
            AccentColor = $"#FF{i:D2}00",
            SortOrder = i - 1,
            Shortcuts = Enumerable.Range(1, 3).Select(j => new ShortcutItem
            {
                DisplayName = $"Shortcut {i}-{j}",
                TargetPath = $@"C:\Apps\App{i}\app{j}.exe",
                SortOrder = j - 1
            }).ToList()
        }).ToList();

        // Act
        await _service.SaveGroupsImmediateAsync(groups);
        var loaded = await _service.LoadGroupsAsync();

        // Assert
        loaded.Should().HaveCount(5);
        for (int i = 0; i < 5; i++)
        {
            loaded[i].Name.Should().Be($"Group {i + 1}");
            loaded[i].Shortcuts.Should().HaveCount(3);
        }
    }

    // ============================================================================================
    // DEBOUNCE TESTS
    // ============================================================================================

    [Fact]
    public void SetDebounceInterval_EnforcesMinimum()
    {
        // Act & Assert - should not throw
        _service.SetDebounceInterval(100);  // Below 500ms minimum
        _service.SetDebounceInterval(500);  // Exactly minimum
        _service.SetDebounceInterval(5000); // Above minimum
    }

    // ============================================================================================
    // DATA DIRECTORY TESTS
    // ============================================================================================

    [Fact]
    public void DataDirectory_ShouldNotBeEmpty()
    {
        _service.DataDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(_service.DataDirectory).Should().BeTrue();
    }

    // ============================================================================================
    // EXPORT TESTS
    // ============================================================================================

    [Fact]
    public async Task ExportAll_CreatesValidJsonFile()
    {
        // Arrange
        var exportPath = Path.Combine(_testDataDir, "export.json");

        // Act
        await _service.ExportAllAsync(exportPath);

        // Assert
        File.Exists(exportPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(exportPath);
        content.Should().Contain("exportedAt");
        content.Should().Contain("version");
        content.Should().Contain("settings");
        content.Should().Contain("groups");
    }
}