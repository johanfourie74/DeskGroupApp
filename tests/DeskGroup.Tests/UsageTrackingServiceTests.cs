using System;
using System.Collections.Generic;
using System.Linq;
using DeskGroup.Models;
using DeskGroup.Services;
using FluentAssertions;
using Xunit;

namespace DeskGroup.Tests;

public class UsageTrackingServiceTests
{
    private readonly PersistenceService _persistenceService;
    private readonly UsageTrackingService _service;

    public UsageTrackingServiceTests()
    {
        _persistenceService = new PersistenceService();
        _service = new UsageTrackingService(_persistenceService);
    }

    // Helper

    private static IconGroup CreateGroupWithShortcuts(int count)
    {
        var group = new IconGroup { Name = "Test Group" };
        for (int i = 0; i < count; i++)
        {
            group.Shortcuts.Add(new ShortcutItem
            {
                DisplayName = $"Shortcut {i + 1}",
                TargetPath = $@"C:\Apps\app{i + 1}.exe",
                SortOrder = i,
                DateAdded = DateTime.UtcNow.AddMinutes(-count + i)
            });
        }
        return group;
    }

    // ============================================================================================
    // RECORD LAUNCH TESTS
    // ============================================================================================

    [Fact]
    public void RecordLaunch_IncrementsCount()
    {
        // Arrange
        var shortcut = new ShortcutItem { DisplayName = "Test" };
        shortcut.LaunchCount.Should().Be(0);

        // Act
        _service.RecordLaunch(shortcut);

        // Assert
        shortcut.LaunchCount.Should().Be(1);
    }

    [Fact]
    public void RecordLaunch_MultipleTimes_AccumulatesCount()
    {
        // Arrange
        var shortcut = new ShortcutItem { DisplayName = "Test" };

        // Act
        for (int i = 0; i < 10; i++)
        {
            _service.RecordLaunch(shortcut);
        }

        // Assert
        shortcut.LaunchCount.Should().Be(10);
    }

    [Fact]
    public void RecordLaunch_SetsLastLaunched()
    {
        // Arrange
        var shortcut = new ShortcutItem { DisplayName = "Test" };
        shortcut.LastLaunched.Should().BeNull();

        // Act
        var before = DateTime.UtcNow;
        _service.RecordLaunch(shortcut);
        var after = DateTime.UtcNow;

        // Assert
        shortcut.LastLaunched.Should().NotBeNull();
        shortcut.LastLaunched.Should().BeOnOrAfter(before);
        shortcut.LastLaunched.Should().BeOnOrBefore(after);
    }

    // ============================================================================================
    // GET TOP USED TESTS
    // ============================================================================================

    [Fact]
    public void GetTopUsed_EmptyGroup_ReturnsEmptyList()
    {
        // Arrange
        var group = new IconGroup { Name = "Empty" };

        // Act
        var result = _service.GetTopUsed(group);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetTopUsed_ReturnsInDescendingOrder()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(5);
        group.Shortcuts[0].LaunchCount = 10;
        group.Shortcuts[1].LaunchCount = 50;
        group.Shortcuts[2].LaunchCount = 30;
        group.Shortcuts[3].LaunchCount = 5;
        group.Shortcuts[4].LaunchCount = 20;

        // Act
        var result = _service.GetTopUsed(group);

        // Assert
        result.Should().HaveCount(5);
        result[0].LaunchCount.Should().Be(50);
        result[1].LaunchCount.Should().Be(30);
        result[2].LaunchCount.Should().Be(20);
        result[3].LaunchCount.Should().Be(10);
        result[4].LaunchCount.Should().Be(5);
    }

    [Fact]
    public void GetTopUsed_RespectsTakeCount()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(20);
        for (int i = 0; i < 20; i++)
        {
            group.Shortcuts[i].LaunchCount = 20 - i;
        }

        // Act
        var result = _service.GetTopUsed(group, 5);

        // Assert
        result.Should().HaveCount(5);
        result[0].LaunchCount.Should().Be(20);
        result[4].LaunchCount.Should().Be(16);
    }

    [Fact]
    public void GetTopUsed_TiedCounts_SortsByLastLaunched()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(3);
        group.Shortcuts[0].LaunchCount = 10;
        group.Shortcuts[0].LastLaunched = DateTime.UtcNow.AddHours(-3);
        group.Shortcuts[1].LaunchCount = 10;
        group.Shortcuts[1].LastLaunched = DateTime.UtcNow.AddHours(-1); // Most recent
        group.Shortcuts[2].LaunchCount = 10;
        group.Shortcuts[2].LastLaunched = DateTime.UtcNow.AddHours(-2);

        // Act
        var result = _service.GetTopUsed(group);

        // Assert
        result[0].Should().BeSameAs(group.Shortcuts[1]); // Most recently launched first
        result[1].Should().BeSameAs(group.Shortcuts[2]);
        result[2].Should().BeSameAs(group.Shortcuts[0]);
    }

    // ============================================================================================
    // GET TOP USED WITH FALLBACK TESTS
    // ============================================================================================

    [Fact]
    public void GetTopUsedWithFallback_FillsWithRecentlyAdded()
    {
        // Arrange - only 2 have been launched, but we ask for 5
        var group = CreateGroupWithShortcuts(8);
        group.Shortcuts[0].LaunchCount = 5;
        group.Shortcuts[0].LastLaunched = DateTime.UtcNow;
        group.Shortcuts[3].LaunchCount = 3;
        group.Shortcuts[3].LastLaunched = DateTime.UtcNow.AddMinutes(-5);
        // Rest have LaunchCount = 0

        // Act
        var result = _service.GetTopUsedWithFallback(group, 5);

        // Assert
        result.Should().HaveCount(5);

        // First two should be the launched ones (by count desc)
        result[0].Should().BeSameAs(group.Shortcuts[0]); // Count 5
        result[1].Should().BeSameAs(group.Shortcuts[3]); // Count 3

        // Remaining 3 should be filled from never-launched, ordered by DateAdded desc
        var fillers = result.Skip(2).ToList();
        fillers.Should().HaveCount(3);
        fillers.All(s => s.LaunchCount == 0).Should().BeTrue();
    }

    [Fact]
    public void GetTopUsedWithFallback_AllLaunched_NoFallbackNeeded()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(5);
        for (int i = 0; i < 5; i++)
        {
            group.Shortcuts[i].LaunchCount = (i + 1) * 10;
            group.Shortcuts[i].LastLaunched = DateTime.UtcNow;
        }

        // Act
        var result = _service.GetTopUsedWithFallback(group, 3);

        // Assert
        result.Should().HaveCount(3);
        result[0].LaunchCount.Should().Be(50);
        result[1].LaunchCount.Should().Be(40);
        result[2].LaunchCount.Should().Be(30);
    }

    [Fact]
    public void GetTopUsedWithFallback_NoneLaunched_ReturnsMostRecentlyAdded()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(8);
        // All have LaunchCount = 0

        // Act
        var result = _service.GetTopUsedWithFallback(group, 5);

        // Assert
        result.Should().HaveCount(5);
        result.All(s => s.LaunchCount == 0).Should().BeTrue();

        // Should be ordered by DateAdded descending (most recent first)
        for (int i = 0; i < result.Count - 1; i++)
        {
            result[i].DateAdded.Should().BeOnOrAfter(result[i + 1].DateAdded);
        }
    }

    [Fact]
    public void GetTopUsedWithFallback_FewerThanRequested_ReturnsAll()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(3);

        // Act
        var result = _service.GetTopUsedWithFallback(group, 10);

        // Assert
        result.Should().HaveCount(3);
    }

    // ============================================================================================
    // RESET TESTS
    // ============================================================================================

    [Fact]
    public void ResetUsage_ClearsCountAndTimestamp()
    {
        // Arrange
        var shortcut = new ShortcutItem
        {
            LaunchCount = 42,
            LastLaunched = DateTime.UtcNow
        };

        // Act
        _service.ResetUsage(shortcut);

        // Assert
        shortcut.LaunchCount.Should().Be(0);
        shortcut.LastLaunched.Should().BeNull();
    }

    [Fact]
    public void ResetGroupUsage_ResetsAllShortcuts()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(5);
        foreach (var s in group.Shortcuts)
        {
            s.LaunchCount = 10;
            s.LastLaunched = DateTime.UtcNow;
        }

        // Act
        _service.ResetGroupUsage(group);

        // Assert
        group.Shortcuts.All(s => s.LaunchCount == 0).Should().BeTrue();
        group.Shortcuts.All(s => s.LastLaunched == null).Should().BeTrue();
    }

    // ============================================================================================
    // GROUP SUMMARY TESTS
    // ============================================================================================

    [Fact]
    public void GetGroupSummary_EmptyGroup_ReturnsZeros()
    {
        // Arrange
        var group = new IconGroup { Name = "Empty" };

        // Act
        var summary = _service.GetGroupSummary(group);

        // Assert
        summary.TotalLaunches.Should().Be(0);
        summary.ShortcutCount.Should().Be(0);
        summary.MostUsed.Should().BeNull();
        summary.LeastUsed.Should().BeNull();
        summary.NeverLaunched.Should().BeEmpty();
    }

    [Fact]
    public void GetGroupSummary_PopulatedGroup_ReturnsCorrectStats()
    {
        // Arrange
        var group = CreateGroupWithShortcuts(5);
        group.Shortcuts[0].LaunchCount = 10;
        group.Shortcuts[1].LaunchCount = 50;
        group.Shortcuts[2].LaunchCount = 0;
        group.Shortcuts[3].LaunchCount = 5;
        group.Shortcuts[4].LaunchCount = 0;

        // Act
        var summary = _service.GetGroupSummary(group);

        // Assert
        summary.TotalLaunches.Should().Be(65);
        summary.ShortcutCount.Should().Be(5);
        summary.MostUsed.Should().BeSameAs(group.Shortcuts[1]);
        summary.LeastUsed.Should().BeSameAs(group.Shortcuts[3]); // Least among launched
        summary.NeverLaunched.Should().HaveCount(2);
        summary.NeverLaunched.Should().Contain(group.Shortcuts[2]);
        summary.NeverLaunched.Should().Contain(group.Shortcuts[4]);
    }
}