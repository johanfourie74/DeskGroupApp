using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DeskGroup.Services;

public sealed class UpdateService
{
    private const string Owner = "johanfourie74";
    private const string Repo  = "DeskGroup";
    private const string ApiLatest = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";

    public sealed record UpdateInfo(
        string TagName,
        string Version,
        string DownloadUrl,
        string ReleaseNotes,
        string HtmlUrl);

    private static readonly HttpClient _http;

    static UpdateService()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeskGroup-UpdateChecker/1.2");
    }

    // ── Version helpers ────────────────────────────────────────────────────────

    public static Version GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v != null ? new Version(v.Major, v.Minor, v.Build) : new Version(1, 2, 0);
    }

    // ── GitHub release check ───────────────────────────────────────────────────

    /// <summary>
    /// Returns update info if a newer release exists on GitHub, null if up to date or on error.
    /// Pass skippedVersion to suppress a specific version the user previously chose to skip.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(
        string skippedVersion = "",
        CancellationToken ct = default)
    {
        try
        {
            var json = await _http.GetFromJsonAsync<JsonElement>(ApiLatest, ct).ConfigureAwait(false);

            var tagName  = json.TryGetProperty("tag_name",  out var t) ? t.GetString() ?? "" : "";
            var body     = json.TryGetProperty("body",       out var b) ? b.GetString() ?? "" : "";
            var htmlUrl  = json.TryGetProperty("html_url",   out var h) ? h.GetString() ?? "" : "";

            var versionStr = tagName.TrimStart('v');
            if (!Version.TryParse(versionStr, out var latest)) return null;

            var current = GetCurrentVersion();
            if (latest <= current) return null;

            // User previously chose to skip this exact version
            if (!string.IsNullOrEmpty(skippedVersion) &&
                string.Equals(versionStr, skippedVersion.TrimStart('v'), StringComparison.OrdinalIgnoreCase))
                return null;

            // Find installer .exe asset
            var downloadUrl = string.Empty;
            if (json.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString() ?? ""
                            : "";
                        break;
                    }
                }
            }

            return new UpdateInfo(tagName, versionStr, downloadUrl, body, htmlUrl);
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    // ── Download ───────────────────────────────────────────────────────────────

    public static async Task<string> DownloadAsync(
        string downloadUrl,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "DeskGroup-Update-Setup.exe");

        using var response = await _http.GetAsync(
            downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[8192];
        long downloaded = 0;

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file   = File.Create(tempPath);

        int read;
        while ((read = await stream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            downloaded += read;
            if (totalBytes > 0)
                progress?.Report((int)(downloaded * 100 / totalBytes));
        }

        return tempPath;
    }

    // ── Install & restart ──────────────────────────────────────────────────────

    public static void LaunchInstallerAndShutdown(string installerPath)
    {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName        = installerPath,
            Arguments       = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
            UseShellExecute = true
        });
        System.Windows.Application.Current.Shutdown();
    }
}
