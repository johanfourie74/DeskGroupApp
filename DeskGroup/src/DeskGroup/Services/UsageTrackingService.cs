using System;
using System.Collections.Generic;
using System.Linq;
using DeskGroup.Models;

namespace DeskGroup.Services;

public class UsageTrackingService
{
    private readonly PersistenceService _persistenceService;

    public UsageTrackingService(PersistenceService persistenceService)
    {
        _persistenceService = persistenceService;
    }

    public void RecordLaunch(ShortcutItem shortcut)
    {
        shortcut.LaunchCount++;
        shortcut.LastLaunched = DateTime.UtcNow;
    }

    public List<ShortcutItem> GetTopUsed(IconGroup group, int count = 10)
    {
        return group.Shortcuts
            .OrderByDescending(s => s.LaunchCount)
            .ThenByDescending(s => s.LastLaunched ?? DateTime.MinValue)
            .ThenBy(s => s.SortOrder)
            .Take(count)
            .ToList();
    }

    public List<ShortcutItem> GetTopUsedWithFallback(IconGroup group, int count = 10)
    {
        var launched = group.Shortcuts
            .Where(s => s.LaunchCount > 0)
            .OrderByDescending(s => s.LaunchCount)
            .ThenByDescending(s => s.LastLaunched ?? DateTime.MinValue)
            .ToList();

        if (launched.Count >= count)
            return launched.Take(count).ToList();

                var launchedIds = new HashSet<Guid>(launched.Select(s => s.Id));
        var filler = group.Shortcuts
            .Where(s => !launchedIds.Contains(s.Id))
            .OrderByDescending(s => s.DateAdded)
            .Take(count - launched.Count);

        return launched.Concat(filler).ToList();
    }

    public void ResetUsage(ShortcutItem shortcut)
    {
        shortcut.LaunchCount = 0;
        shortcut.LastLaunched = null;
    }

    public void ResetGroupUsage(IconGroup group)
    {
        foreach (var shortcut in group.Shortcuts)
            ResetUsage(shortcut);
    }

    public GroupUsageSummary GetGroupSummary(IconGroup group)
    {
        var shortcuts = group.Shortcuts;
        return new GroupUsageSummary
        {
            TotalLaunches = shortcuts.Sum(s => s.LaunchCount),
            ShortcutCount = shortcuts.Count,
            MostUsed = shortcuts.OrderByDescending(s => s.LaunchCount).FirstOrDefault(),
            LeastUsed = shortcuts.Where(s => s.LaunchCount > 0).OrderBy(s => s.LaunchCount).FirstOrDefault(),
            NeverLaunched = shortcuts.Where(s => s.LaunchCount == 0).ToList()
        };
    }
}

public class GroupUsageSummary
{
    public int TotalLaunches { get; set; }
    public int ShortcutCount { get; set; }
    public ShortcutItem? MostUsed { get; set; }
    public ShortcutItem? LeastUsed { get; set; }
    public List<ShortcutItem> NeverLaunched { get; set; } = new();
}