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

    public int CooldownSeconds { get; set; } = 5; // Reduced from 10 to 5 seconds
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
        // Filter system files
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();
        if (fileName == "desktop.ini" || fileName == "thumbs.db" || fileName.StartsWith("~"))
        {
            System.Diagnostics.Debug.WriteLine($"UngroupedWatcher: Ignoring system file: {fileName}");
            return;
        }

        // Only watch shortcuts and executables
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".lnk" && ext != ".url" && ext != ".exe")
        {
            System.Diagnostics.Debug.WriteLine($"UngroupedWatcher: Ignoring non-shortcut file: {fileName}");
            return;
        }

        lock (_lock)
        {
            _pendingNewFiles.Add(filePath);
            System.Diagnostics.Debug.WriteLine($"UngroupedWatcher: Added {fileName} to pending list ({_pendingNewFiles.Count} total)");

            // CRITICAL: Only start timer if not already running!
            if (_cooldownTimer == null || !_cooldownTimer.Enabled)
            {
                _cooldownTimer?.Stop();
                _cooldownTimer?.Dispose();
                _cooldownTimer = new System.Timers.Timer(CooldownSeconds * 1000);
                _cooldownTimer.AutoReset = false;
                _cooldownTimer.Elapsed += OnCooldownElapsed;
                _cooldownTimer.Start();
                System.Diagnostics.Debug.WriteLine($"UngroupedWatcher: Started {CooldownSeconds}s cooldown timer");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"UngroupedWatcher: Timer already running, added to batch");
            }
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
        {
            System.Diagnostics.Debug.WriteLine($"UngroupedWatcher: Cooldown elapsed, notifying about {files.Count} new icon(s)");
            NewUngroupedIconsDetected?.Invoke(this, files);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"UngroupedWatcher: Cooldown elapsed but no files to notify");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        StopWatching();
    }
}