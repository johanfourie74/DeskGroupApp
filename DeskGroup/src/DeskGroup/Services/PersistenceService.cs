using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using DeskGroup.Models;

namespace DeskGroup.Services;

public class PersistenceService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _dataDirectory;
    private readonly string _settingsFilePath;
    private readonly string _groupsFilePath;
    private readonly string _backupDirectory;

    private CancellationTokenSource? _debounceCts;
    private readonly object _debounceLock = new();
    private int _debounceMs;

    public event EventHandler? DataSaved;
    public event EventHandler<PersistenceErrorEventArgs>? ErrorOccurred;

    public PersistenceService()
    {
        bool isPortable = File.Exists(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "portable.marker"));

        if (isPortable)
            _dataDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskGroupData");
        else
            _dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskGroup");

        _settingsFilePath = Path.Combine(_dataDirectory, "settings.json");
        _groupsFilePath = Path.Combine(_dataDirectory, "groups.json");
        _backupDirectory = Path.Combine(_dataDirectory, "backups");

        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_backupDirectory);
        _debounceMs = 2000;
    }

    public string DataDirectory => _dataDirectory;

    public void SetDebounceInterval(int milliseconds)
    {
        _debounceMs = Math.Max(500, milliseconds);
    }

    public async Task<AppSettings> LoadSettingsAsync()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                var defaults = new AppSettings();
                await SaveSettingsImmediateAsync(defaults);
                return defaults;
            }
            var json = await File.ReadAllTextAsync(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            return settings ?? new AppSettings();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new PersistenceErrorEventArgs("Failed to load settings. Using defaults.", ex));
            return new AppSettings();
        }
    }

    public async Task SaveSettingsImmediateAsync(AppSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            await WriteFileAtomicAsync(_settingsFilePath, json);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new PersistenceErrorEventArgs("Failed to save settings.", ex));
        }
    }

    public void SaveSettingsDebounced(AppSettings settings)
    {
        DebouncedSave(() => SaveSettingsImmediateAsync(settings));
    }

    public async Task<List<IconGroup>> LoadGroupsAsync()
    {
        try
        {
            if (!File.Exists(_groupsFilePath))
                return new List<IconGroup>();

            var json = await File.ReadAllTextAsync(_groupsFilePath);
            var groups = JsonSerializer.Deserialize<List<IconGroup>>(json, JsonOptions);
            return groups ?? new List<IconGroup>();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new PersistenceErrorEventArgs("Failed to load groups. Attempting backup restore...", ex));
            var restored = await TryRestoreFromBackupAsync<List<IconGroup>>(_groupsFilePath);
            return restored ?? new List<IconGroup>();
        }
    }

    public async Task SaveGroupsImmediateAsync(List<IconGroup> groups)
    {
        try
        {
            await CreateBackupAsync(_groupsFilePath);
            var json = JsonSerializer.Serialize(groups, JsonOptions);
            await WriteFileAtomicAsync(_groupsFilePath, json);
            DataSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new PersistenceErrorEventArgs("Failed to save groups.", ex));
        }
    }

    public void SaveGroupsDebounced(List<IconGroup> groups)
    {
        DebouncedSave(() => SaveGroupsImmediateAsync(groups));
    }

    private async Task CreateBackupAsync(string sourceFilePath)
    {
        try
        {
            if (!File.Exists(sourceFilePath)) return;
            var fileName = Path.GetFileNameWithoutExtension(sourceFilePath);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.Combine(_backupDirectory, $"{fileName}_{timestamp}.json");
            var content = await File.ReadAllTextAsync(sourceFilePath);
            await File.WriteAllTextAsync(backupPath, content);

            var backupFiles = Directory.GetFiles(_backupDirectory, $"{fileName}_*.json");
            if (backupFiles.Length > 5)
            {
                Array.Sort(backupFiles);
                for (int i = 0; i < backupFiles.Length - 5; i++)
                    File.Delete(backupFiles[i]);
            }
        }
        catch { }
    }

    private async Task<T?> TryRestoreFromBackupAsync<T>(string originalFilePath) where T : class
    {
        try
        {
            var fileName = Path.GetFileNameWithoutExtension(originalFilePath);
            var backupFiles = Directory.GetFiles(_backupDirectory, $"{fileName}_*.json");
            if (backupFiles.Length == 0) return null;
            Array.Sort(backupFiles);
            var latestBackup = backupFiles[^1];
            var json = await File.ReadAllTextAsync(latestBackup);
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch { return null; }
    }

    private static async Task WriteFileAtomicAsync(string filePath, string content)
    {
        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content);
        File.Move(tempPath, filePath, overwrite: true);
    }

    private void DebouncedSave(Func<Task> saveAction)
    {
        lock (_debounceLock)
        {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(_debounceMs, token);
                    if (!token.IsCancellationRequested)
                        await saveAction();
                }
                catch (TaskCanceledException) { }
            }, token);
        }
    }

    public async Task ExportAllAsync(string exportFilePath)
    {
        var exportData = new
        {
            ExportedAt = DateTime.UtcNow,
            Version = "1.0.0",
            Settings = await LoadSettingsAsync(),
            Groups = await LoadGroupsAsync()
        };
        var json = JsonSerializer.Serialize(exportData, JsonOptions);
        await File.WriteAllTextAsync(exportFilePath, json);
    }
}

public class PersistenceErrorEventArgs : EventArgs
{
    public string Message { get; }
    public Exception Exception { get; }
    public PersistenceErrorEventArgs(string message, Exception exception)
    {
        Message = message;
        Exception = exception;
    }
}