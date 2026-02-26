using System;
using System.Collections.Generic;
using System.IO;
using System.Timers;

namespace DeskGroup.Services;

public class UngroupedIconWatcherService : IDisposable
{
    private FileSystemWatcher? _userDesktopWatcher;
    private FileSystemWatcher? _publicDesktopWatcher;
    private System.Timers.Timer? _cooldownTimer;
    private readonly HashSet<string> _pendingNewFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();
    private bool _disposed;

    public int CooldownSeconds { get; set; } = 10;
    public event EventHandler<List<string>>? NewUngroupedIconsDetected;

    public void StartWatching()
    {
        var userDesktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var publicDesktop = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

        _userDesktopWatcher = CreateWatcher(userDesktop);
        _userDesktopWatcher.EnableRaisingEvents = true;

        try
        {
            _publicDesktopWatcher = CreateWatcher(publicDesktop);
            _publicDesktopWatcher.EnableRaisingEvents = true;
        }
        catch { }
    }

    public void StopWatching()
    {
        if (_userDesktopWatcher != null)
        {
            _userDesktopWatcher.EnableRaisingEvents = false;
            _userDesktopWatcher.Dispose();
            _userDesktopWatcher = null;
        }
        if (_publicDesktopWatcher != null)
        {
            _publicDesktopWatcher.EnableRaisingEvents = false;
            _publicDesktopWatcher.Dispose();
            _publicDesktopWatcher = null;
        }
        _cooldownTimer?.Stop();
        _cooldownTimer?.Dispose();
        _cooldownTimer = null;
    }

    private FileSystemWatcher CreateWatcher(string path)
    {
        var watcher = new FileSystemWatcher(path)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime,
            IncludeSubdirectories = false,
            Filter = "*.*"
        };
        watcher.Created += OnFileCreated;
        watcher.Renamed += OnFileRenamed;
        return watcher;
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e) => ProcessNewFile(e.FullPath);
    private void OnFileRenamed(object sender, RenamedEventArgs e) => ProcessNewFile(e.FullPath);

    private void ProcessNewFile(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".lnk" && ext != ".url" && ext != ".exe") return;

        lock (_lock)
        {
            _pendingNewFiles.Add(filePath);
            _cooldownTimer?.Stop();
            _cooldownTimer?.Dispose();
            _cooldownTimer = new System.Timers.Timer(CooldownSeconds * 1000);
            _cooldownTimer.AutoReset = false;
            _cooldownTimer.Elapsed += OnCooldownElapsed;
            _cooldownTimer.Start();
        }
    }

    private void OnCooldownElapsed(object? sender, ElapsedEventArgs e)
    {
        List<string> files;
        lock (_lock)
        {
            files = new List<string>(_pendingNewFiles);
            _pendingNewFiles.Clear();
        }
        if (files.Count > 0)
            NewUngroupedIconsDetected?.Invoke(this, files);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
    }
}