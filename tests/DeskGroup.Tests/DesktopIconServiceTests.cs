using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeskGroup.Models;
using DeskGroup.Services;
using FluentAssertions;
using Xunit;

namespace DeskGroup.Tests;

public class DesktopIconServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _fakeDesktop;
    private readonly string _storageDir;
    private readonly DesktopIconService _service;

    public DesktopIconServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"DeskGroupTests_{Guid.NewGuid():N}");
        _fakeDesktop = Path.Combine(_testDir, "Desktop");
        _storageDir = Path.Combine(_testDir, "AppData");

        Directory.CreateDirectory(_fakeDesktop);
        Directory.CreateDirectory(_storageDir);

        _service = new DesktopIconService(_storageDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, recursive: true);
        }
        catch { }
    }

    // Helpers

    private string CreateFakeShortcut(string name, string extension = ".lnk")
    {
        var path = Path.Combine(_fakeDesktop, $"{name}{extension}");
        File.WriteAllText(path, $"fake shortcut: {name}");
        return path;
    }

    private ShortcutItem CreateShortcutItem(string name, string originalPath)
    {
        return new ShortcutItem
        {
            Id = Guid.NewGuid(),
            DisplayName = name,
            TargetPath = $@"C:\Apps\{name}.exe",
            OriginalShortcutPath = originalPath,
            OriginalIconHidden = false
        };
    }

    // ============================================================================================
    // HIDE DESKTOP ICON TESTS
    // ============================================================================================

    [Fact]
    public void HideDesktopIcon_MovesFileToStorage()
    {
        // Arrange
        var originalPath = CreateFakeShortcut("Chrome");
        var shortcut = CreateShortcutItem("Chrome", originalPath);

        File.Exists(originalPath).Should().BeTrue("file should exist before hiding");

        // Act
        var state = _service.HideDesktopIcon(shortcut);

        // Assert
        state.Should().NotBeNull();
        state!.WasMoved.Should().BeTrue();
        state.OriginalFilePath.Should().Be(originalPath);
        state.StoredFilePath.Should().NotBeNullOrEmpty();
        state.ShortcutItemId.Should().Be(shortcut.Id);

        File.Exists(originalPath).Should().BeFalse("original file should be moved");
        File.Exists(state.StoredFilePath).Should().BeTrue("file should be in storage");
        shortcut.OriginalIconHidden.Should().BeTrue();
    }

    [Fact]
    public void HideDesktopIcon_NonExistentFile_ReturnsNull()
    {
        // Arrange
        var shortcut = CreateShortcutItem("Missing", @"C:\Does\Not\Exist.lnk");

        // Act
        var state = _service.HideDesktopIcon(shortcut);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public void HideDesktopIcon_EmptyPath_ReturnsNull()
    {
        // Arrange
        var shortcut = new ShortcutItem
        {
            DisplayName = "Empty",
            OriginalShortcutPath = string.Empty
        };

        // Act
        var state = _service.HideDesktopIcon(shortcut);

        // Assert
        state.Should().BeNull();
    }

    // ============================================================================================
    // RESTORE DESKTOP ICON TESTS
    // ============================================================================================

    [Fact]
    public void RestoreDesktopIcon_MovesFileBack()
    {
        // Arrange - hide first, then restore
        var originalPath = CreateFakeShortcut("Firefox");
        var shortcut = CreateShortcutItem("Firefox", originalPath);
        var state = _service.HideDesktopIcon(shortcut);

        state.Should().NotBeNull();
        File.Exists(originalPath).Should().BeFalse();

        // Act
        var result = _service.RestoreDesktopIcon(state!);

        // Assert
        result.Should().BeTrue();
        File.Exists(originalPath).Should().BeTrue("file should be restored to original location");
        File.Exists(state!.StoredFilePath).Should().BeFalse("stored file should be removed");
    }

    [Fact]
    public void RestoreDesktopIcon_OriginalAlreadyExists_DeletesStoredCopy()
    {
        // Arrange
        var originalPath = CreateFakeShortcut("VSCode");
        var shortcut = CreateShortcutItem("VSCode", originalPath);
        var state = _service.HideDesktopIcon(shortcut);

        state.Should().NotBeNull();

        // Recreate the original (simulating user manually placing it back)
        File.WriteAllText(originalPath, "user restored this");

        // Act
        var result = _service.RestoreDesktopIcon(state!);

        // Assert
        result.Should().BeTrue();
        File.Exists(originalPath).Should().BeTrue();
        File.Exists(state!.StoredFilePath).Should().BeFalse("stored copy should be deleted");

        // Original content should be the user's version, not our stored one
        File.ReadAllText(originalPath).Should().Be("user restored this");
    }

    [Fact]
    public void RestoreDesktopIcon_NotMoved_ReturnsFalse()
    {
        // Arrange
        var state = new OriginalIconState
        {
            WasMoved = false,
            StoredFilePath = "",
            OriginalFilePath = @"C:\Some\Path.lnk"
        };

        // Act
        var result = _service.RestoreDesktopIcon(state);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RestoreDesktopIcon_StoredFileGone_ReturnsFalse()
    {
        // Arrange
        var state = new OriginalIconState
        {
            WasMoved = true,
            StoredFilePath = @"C:\Does\Not\Exist\stored.lnk",
            OriginalFilePath = @"C:\Users\Test\Desktop\app.lnk"
        };

        // Act
        var result = _service.RestoreDesktopIcon(state);

        // Assert
        result.Should().BeFalse();
    }

    // ============================================================================================
    // RESTORE ALL GROUP ICONS TESTS
    // ============================================================================================

    [Fact]
    public void RestoreAllGroupIcons_RestoresMultipleIcons()
    {
        // Arrange
        var group = new IconGroup { Name = "Test Group" };
        var paths = new List<string>();

        for (int i = 0; i < 3; i++)
        {
            var originalPath = CreateFakeShortcut($"App{i}");
            var shortcut = CreateShortcutItem($"App{i}", originalPath);
            var state = _service.HideDesktopIcon(shortcut);

            state.Should().NotBeNull();
            group.OriginalIconStates.Add(state!);
            paths.Add(originalPath);
        }

        // Verify all are hidden
        foreach (var path in paths)
        {
            File.Exists(path).Should().BeFalse();
        }

        // Act
        _service.RestoreAllGroupIcons(group);

        // Assert - all should be restored
        foreach (var path in paths)
        {
            File.Exists(path).Should().BeTrue($"{Path.GetFileName(path)} should be restored");
        }
    }

    // ============================================================================================
    // GET UNGROUPED SHORTCUTS TESTS
    // ============================================================================================

    [Fact]
    public void GetUngroupedShortcuts_NoGroups_ReturnsAllDesktopIcons()
    {
        // Note: This test uses the real desktop path, so results vary per machine.
        // It mainly validates the method doesn't throw.

        // Act
        var result = _service.GetUngroupedShortcuts(new List<IconGroup>());

        // Assert
        result.Should().NotBeNull();
        // Can't assert exact count since it depends on the test machine's desktop
    }

    [Fact]
    public void GetUngroupedShortcuts_ExcludesGroupedPaths()
    {
        // Arrange
        var group = new IconGroup
        {
            Name = "Grouped",
            Shortcuts = new List<ShortcutItem>
            {
                new ShortcutItem
                {
                    OriginalShortcutPath = @"C:\Users\Test\Desktop\AlreadyGrouped.lnk"
                }
            }
        };

        // Act
        var result = _service.GetUngroupedShortcuts(new List<IconGroup> { group });

        // Assert
        result.Should().NotBeNull();
        result.Should().NotContain(@"C:\Users\Test\Desktop\AlreadyGrouped.lnk");
    }

    // ============================================================================================
    // CLEANUP ORPHANS TESTS
    // ============================================================================================

    [Fact]
    public void CleanupOrphans_NoOrphans_DoesNothing()
    {
        // Arrange
        var originalPath = CreateFakeShortcut("Legit");
        var shortcut = CreateShortcutItem("Legit", originalPath);
        var state = _service.HideDesktopIcon(shortcut);
        state.Should().NotBeNull();

        var group = new IconGroup
        {
            Name = "Test",
            OriginalIconStates = new List<OriginalIconState> { state! }
        };

        // Act
        _service.CleanupOrphans(new List<IconGroup> { group });

        // Assert - stored file should still be there (not orphaned)
        File.Exists(state!.StoredFilePath).Should().BeTrue();
    }

    // ============================================================================================
    // ROUND TRIP TESTS
    // ============================================================================================

    [Fact]
    public void HideAndRestore_FullRoundTrip_PreservesContent()
    {
        // Arrange
        var originalContent = "This is the original shortcut content!";
        var originalPath = Path.Combine(_fakeDesktop, "RoundTrip.lnk");
        File.WriteAllText(originalPath, originalContent);

        var shortcut = CreateShortcutItem("RoundTrip", originalPath);

        // Act - Hide
        var state = _service.HideDesktopIcon(shortcut);
        state.Should().NotBeNull();
        File.Exists(originalPath).Should().BeFalse();

        // Verify stored content matches
        var storedContent = File.ReadAllText(state!.StoredFilePath);
        storedContent.Should().Be(originalContent);

        // Act - Restore
        var restored = _service.RestoreDesktopIcon(state);
        restored.Should().BeTrue();

        // Assert - Content preserved
        File.Exists(originalPath).Should().BeTrue();
        var restoredContent = File.ReadAllText(originalPath);
        restoredContent.Should().Be(originalContent);
    }

    [Fact]
    public void HideMultiple_ThenRestoreAll_AllContentPreserved()
    {
        // Arrange
        var group = new IconGroup { Name = "Batch Test" };
        var expectedContents = new Dictionary<string, string>();

        for (int i = 0; i < 5; i++)
        {
            var content = $"Content for app {i}: {Guid.NewGuid()}";
            var path = Path.Combine(_fakeDesktop, $"BatchApp{i}.lnk");
            File.WriteAllText(path, content);
            expectedContents[path] = content;

            var shortcut = CreateShortcutItem($"BatchApp{i}", path);
            var state = _service.HideDesktopIcon(shortcut);
            state.Should().NotBeNull();
            group.OriginalIconStates.Add(state!);
        }

        // Verify all hidden
        foreach (var path in expectedContents.Keys)
        {
            File.Exists(path).Should().BeFalse();
        }

        // Act
        _service.RestoreAllGroupIcons(group);

        // Assert
        foreach (var (path, expectedContent) in expectedContents)
        {
            File.Exists(path).Should().BeTrue();
            File.ReadAllText(path).Should().Be(expectedContent);
        }
    }
}