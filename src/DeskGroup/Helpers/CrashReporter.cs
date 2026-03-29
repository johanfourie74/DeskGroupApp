using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace DeskGroup.Helpers;

internal static class CrashReporter
{
    private static readonly string CrashDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DeskGroup", "crash-reports");

    private const string IssuesBaseUrl = "https://github.com/johanfourie74/DeskGroupApp/issues/new";
    private const int MaxFiles = 10;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    // ----------------------------------------------------------------
    // Public API
    // ----------------------------------------------------------------

    /// <summary>Writes a crash log to %LOCALAPPDATA%\DeskGroup\crash-reports\ and prunes old logs.</summary>
    public static void WriteCrashLog(Exception ex, string context = "")
    {
        try
        {
            Directory.CreateDirectory(CrashDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            var filePath = Path.Combine(CrashDir, $"crash-{timestamp}.txt");

            var sb = new StringBuilder();
            sb.AppendLine("DeskGroup Crash Report");
            sb.AppendLine(new string('=', 60));
            sb.AppendLine($"Timestamp    : {DateTime.UtcNow:O}");
            sb.AppendLine($"Version      : {GetVersion()}");
            sb.AppendLine($"OS           : {Environment.OSVersion}");
            sb.AppendLine($"Architecture : {RuntimeInformation.OSArchitecture}");
            sb.AppendLine($".NET Runtime : {RuntimeInformation.FrameworkDescription}");
            if (!string.IsNullOrWhiteSpace(context))
                sb.AppendLine($"Context      : {context}");
            sb.AppendLine();
            sb.AppendLine("Exception:");
            sb.AppendLine(ex.ToString());

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);

            CleanOldLogs();
        }
        catch
        {
            // Best-effort — never crash the crash reporter
        }
    }

    /// <summary>Shows a "Would you like to report this?" dialog. Yes → opens pre-filled GitHub Issues URL.</summary>
    public static void ShowCrashDialog(Exception ex)
    {
        try
        {
            var result = MessageBox.Show(
                $"An unexpected error occurred:\n\n{ex.Message}\n\n" +
                "DeskGroup will try to continue running.\n\n" +
                "Would you like to report this issue on GitHub?",
                "DeskGroup Error",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
                OpenUrl(BuildIssueUrl(ex, GetVersion()));
        }
        catch
        {
            // Silent
        }
    }

    /// <summary>Builds a pre-filled GitHub Issues URL for an exception-based bug report.</summary>
    public static string BuildIssueUrl(Exception ex, string version)
    {
        var body =
            $"## Bug Report\n\n" +
            $"**Version:** {version}\n" +
            $"**OS:** {Environment.OSVersion}\n" +
            $"**Architecture:** {RuntimeInformation.OSArchitecture}\n" +
            $"**.NET Runtime:** {RuntimeInformation.FrameworkDescription}\n\n" +
            $"## Steps to Reproduce\n\n1. \n2. \n3. \n\n" +
            $"## Exception\n\n```\n{ex}\n```\n\n" +
            $"## Additional Context\n\n<!-- Add any other context here -->\n";

        return $"{IssuesBaseUrl}?title={Uri.EscapeDataString($"Bug: {ex.GetType().Name}")}" +
               $"&body={Uri.EscapeDataString(body)}&labels=bug";
    }

    /// <summary>Builds a pre-filled GitHub Issues URL for a manually triggered bug report (no exception).</summary>
    public static string BuildManualIssueUrl()
    {
        var body =
            $"## Bug Report\n\n" +
            $"**Version:** {GetVersion()}\n" +
            $"**OS:** {Environment.OSVersion}\n" +
            $"**Architecture:** {RuntimeInformation.OSArchitecture}\n" +
            $"**.NET Runtime:** {RuntimeInformation.FrameworkDescription}\n\n" +
            $"## Steps to Reproduce\n\n1. \n2. \n3. \n\n" +
            $"## Expected Behavior\n\n\n\n" +
            $"## Actual Behavior\n\n\n\n" +
            $"## Additional Context\n\n<!-- Screenshots, logs, etc. -->\n";

        return $"{IssuesBaseUrl}?title={Uri.EscapeDataString("Bug: ")}" +
               $"&body={Uri.EscapeDataString(body)}&labels=bug";
    }

    // ----------------------------------------------------------------
    // Private helpers
    // ----------------------------------------------------------------

    private static void CleanOldLogs()
    {
        try
        {
            var files = new DirectoryInfo(CrashDir)
                .GetFiles("crash-*.txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .ToList();

            // Keep only the newest MaxFiles
            foreach (var f in files.Skip(MaxFiles))
                try { f.Delete(); } catch { }

            // Remove files older than MaxAge
            var cutoff = DateTime.UtcNow - MaxAge;
            foreach (var f in files.Where(f => f.LastWriteTimeUtc < cutoff))
                try { f.Delete(); } catch { }
        }
        catch { }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private static string GetVersion()
    {
        var ver = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return ver != null ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : "unknown";
    }
}
