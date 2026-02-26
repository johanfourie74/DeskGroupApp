using System;
using System.IO;
using Microsoft.Win32;

namespace DeskGroup.Services;

public class StartupService
{
    private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DeskGroup";
    private readonly string _executablePath;

    public StartupService()
    {
        _executablePath = Environment.ProcessPath
            ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeskGroup.exe");
    }

    public bool IsRegisteredForStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            var value = key?.GetValue(AppName) as string;
            return !string.IsNullOrEmpty(value);
        }
        catch { return false; }
    }

    public bool EnableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key == null) return false;
            key.SetValue(AppName, $"\"{_executablePath}\" --startup", RegistryValueKind.String);
            return true;
        }
        catch { return false; }
    }

    public bool DisableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key == null) return false;
            if (key.GetValue(AppName) != null)
                key.DeleteValue(AppName, throwOnMissingValue: false);
            return true;
        }
        catch { return false; }
    }

    public void SyncWithSetting(bool startWithWindows)
    {
        if (startWithWindows) EnableStartup();
        else DisableStartup();
    }
}