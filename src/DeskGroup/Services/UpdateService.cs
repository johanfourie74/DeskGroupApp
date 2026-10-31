using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DeskGroup.Services;

public sealed class UpdateService
{
    private const string Owner = "johanfourie74";
    private const string PrivateRepo = "DeskGroup";
    private const string PublicRepo = "DeskGroupApp";

    private static readonly string[] CandidateRepos = { PublicRepo, PrivateRepo };

    private static readonly Regex VersionRegex = new(
        @"(?<!\d)(\d+\.\d+(?:\.\d+){0,2})(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CsprojVersionRegex = new(
        @"<Version>\s*([^<\s]+)\s*</Version>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public sealed record UpdateInfo(
        string TagName,
        string Version,
        string DownloadUrl,
        string ReleaseNotes,
        string HtmlUrl,
        DateTimeOffset? PublishedAt = null,
        bool IsSameVersionRebuild = false);

    public sealed record UpdateCheckResult(
        UpdateInfo? UpdateAvailable,
        UpdateInfo? LatestRelease,
        string? DiagnosticMessage);

    private static readonly HttpClient _http;
    private static string? _cachedToken;
    private static bool _tokenResolved;

    static UpdateService()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DeskGroup-UpdateChecker/1.4");
    }

    // ── Version helpers ────────────────────────────────────────────────────────

    public static Version GetCurrentVersion()
    {
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        return v != null ? NormalizeVersion(v) : new Version(1, 4, 0);
    }

    public static Version NormalizeVersion(Version v)
    {
        int major = Math.Max(0, v.Major);
        int minor = Math.Max(0, v.Minor);
        int build = v.Build >= 0 ? v.Build : 0;
        return new Version(major, minor, build);
    }

    /// <summary>
    /// Flexibly extracts a 3-part Version (Major.Minor.Build) from any tag, title, or filename
    /// (e.g. "v1.4.6", "1.4.6", "Release-v1.4.6-hotfix", "DeskGroup-Setup-1.4.6.exe", "v1.0-all-fixes").
    /// </summary>
    public static bool TryParseFlexibleVersion(string? raw, out Version version, out string normalizedVersionString)
    {
        version = new Version(0, 0, 0);
        normalizedVersionString = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var match = VersionRegex.Match(raw);
        if (!match.Success) return false;

        var candidate = match.Groups[1].Value;
        // If only Major.Minor (e.g. "1.0"), append ".0" so Version.TryParse has 3 components
        var parts = candidate.Split('.');
        if (parts.Length == 2)
            candidate += ".0";

        if (!Version.TryParse(candidate, out var parsed) || parsed == null)
            return false;

        version = NormalizeVersion(parsed);
        normalizedVersionString = $"{version.Major}.{version.Minor}.{version.Build}";
        return true;
    }

    private static string? GetGitHubToken()
    {
        if (_tokenResolved) return _cachedToken;
        _tokenResolved = true;

        try
        {
            var envToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                        ?? Environment.GetEnvironmentVariable("GH_TOKEN");
            if (!string.IsNullOrWhiteSpace(envToken))
            {
                _cachedToken = envToken.Trim();
                return _cachedToken;
            }

            // Check if GitHub CLI (gh) is installed and authenticated
            var ghPaths = new[]
            {
                @"C:\Program Files\GitHub CLI\gh.exe",
                @"C:\Program Files (x86)\GitHub CLI\gh.exe",
                "gh"
            };

            foreach (var ghPath in ghPaths)
            {
                if (ghPath != "gh" && !File.Exists(ghPath)) continue;
                try
                {
                    using var proc = Process.Start(new ProcessStartInfo
                    {
                        FileName = ghPath,
                        Arguments = "auth token",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });
                    if (proc != null)
                    {
                        string output = proc.StandardOutput.ReadToEnd().Trim();
                        proc.WaitForExit(2000);
                        if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) && output.Length > 10)
                        {
                            _cachedToken = output;
                            return _cachedToken;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        return null;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? accept = null)
    {
        var req = new HttpRequestMessage(method, url);
        var token = GetGitHubToken();
        if (!string.IsNullOrEmpty(token) &&
            (url.Contains("api.github.com", StringComparison.OrdinalIgnoreCase) ||
             url.Contains("github.com", StringComparison.OrdinalIgnoreCase)))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (!string.IsNullOrEmpty(accept))
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        return req;
    }

    // ── GitHub & Multi-Source Release Check ────────────────────────────────────

    /// <summary>
    /// Returns update info if a newer release exists on GitHub (or local build output),
    /// null if up to date or on error.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(
        string skippedVersion = "",
        CancellationToken ct = default,
        bool ignoreSkipped = false)
    {
        var detailed = await CheckDetailedAsync(skippedVersion, ignoreSkipped, ct).ConfigureAwait(false);
        return detailed.UpdateAvailable;
    }

    /// <summary>
    /// Performs a comprehensive multi-source check across all GitHub Releases (public + private repos),
    /// GitHub tags/csproj, and local build outputs, returning both any newer update and the latest known release.
    /// </summary>
    public static async Task<UpdateCheckResult> CheckDetailedAsync(
        string skippedVersion = "",
        bool ignoreSkipped = false,
        CancellationToken ct = default)
    {
        var candidates = new List<(Version ParsedVersion, UpdateInfo Info, int Priority)>();
        var errors = new List<string>();

        // 1. Query ALL releases (/releases?per_page=25) across PublicRepo and PrivateRepo
        foreach (var repo in CandidateRepos)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var listUrl = $"https://api.github.com/repos/{Owner}/{repo}/releases?per_page=25";
                using var req = CreateRequest(HttpMethod.Get, listUrl, "application/vnd.github+json");
                using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct).ConfigureAwait(false);
                    if (json.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var rel in json.EnumerateArray())
                        {
                            var parsed = ParseReleaseElement(rel, repo);
                            if (parsed.HasValue)
                                candidates.Add((parsed.Value.Version, parsed.Value.Info, string.IsNullOrEmpty(parsed.Value.Info.DownloadUrl) ? 1 : 3));
                        }
                    }
                }
                else if (repo == PublicRepo)
                {
                    errors.Add($"GitHub API ({repo}) HTTP {(int)resp.StatusCode}");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                if (repo == PublicRepo)
                    errors.Add(ex.Message);
            }
        }

        // 2. Also check DeskGroup.csproj on main / Dev branches in case version was bumped without a formal GitHub Release
        foreach (var repo in CandidateRepos)
        {
            foreach (var branch in new[] { "main", "Dev" })
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var rawUrl = $"https://raw.githubusercontent.com/{Owner}/{repo}/{branch}/src/DeskGroup/DeskGroup.csproj";
                    using var req = CreateRequest(HttpMethod.Get, rawUrl);
                    using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;

                    var xml = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var m = CsprojVersionRegex.Match(xml);
                    if (m.Success && TryParseFlexibleVersion(m.Groups[1].Value, out var csprojVer, out var verStr))
                    {
                        // Check if we already have a candidate with a DownloadUrl for this version
                        if (!candidates.Any(c => c.ParsedVersion == csprojVer && !string.IsNullOrEmpty(c.Info.DownloadUrl)))
                        {
                            string resolvedUrl = await ResolveFallbackAssetUrlAsync(verStr, ct).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(resolvedUrl))
                            {
                                candidates.Add((csprojVer, new UpdateInfo(
                                    $"v{verStr}",
                                    verStr,
                                    resolvedUrl,
                                    $"DeskGroup v{verStr} ({branch} branch build)",
                                    $"https://github.com/{Owner}/{PublicRepo}/releases"), 2));
                            }
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { }
            }
        }

        // 3. Also check local workspace build/output folders if present on this machine
        foreach (var localCandidate in ScanLocalBuildOutputs())
        {
            if (!candidates.Any(c => c.ParsedVersion == localCandidate.ParsedVersion && c.Priority >= 3))
            {
                candidates.Add((localCandidate.ParsedVersion, localCandidate.Info, 2));
            }
        }

        // For any candidate missing a DownloadUrl, try resolving fallback URLs
        for (int i = 0; i < candidates.Count; i++)
        {
            if (string.IsNullOrEmpty(candidates[i].Info.DownloadUrl))
            {
                string fallback = await ResolveFallbackAssetUrlAsync(candidates[i].Info.Version, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(fallback))
                {
                    var updated = candidates[i].Info with { DownloadUrl = fallback };
                    candidates[i] = (candidates[i].ParsedVersion, updated, 3);
                }
            }
        }

        if (candidates.Count == 0)
        {
            string diag = errors.Count > 0
                ? $"Could not reach update server ({errors[0]})."
                : "No releases found.";
            return new UpdateCheckResult(null, null, diag);
        }

        // Sort by: highest Version first, then has DownloadUrl (Priority), then newest PublishedAt
        var best = candidates
            .OrderByDescending(c => c.ParsedVersion)
            .ThenByDescending(c => !string.IsNullOrEmpty(c.Info.DownloadUrl))
            .ThenByDescending(c => c.Priority)
            .ThenByDescending(c => c.Info.PublishedAt ?? DateTimeOffset.MinValue)
            .First();

        var current = GetCurrentVersion();

        // Check if strictly newer version
        if (best.ParsedVersion > current)
        {
            if (!ignoreSkipped &&
                !string.IsNullOrEmpty(skippedVersion) &&
                TryParseFlexibleVersion(skippedVersion, out var skippedVer, out _) &&
                best.ParsedVersion == skippedVer)
            {
                return new UpdateCheckResult(null, best.Info, $"v{best.Info.Version} is skipped.");
            }

            return new UpdateCheckResult(best.Info, best.Info, null);
        }

        // If manual check (ignoreSkipped == true) and same version, check if the release/artifact was published AFTER the running exe was built
        if (ignoreSkipped && best.ParsedVersion == current && !string.IsNullOrEmpty(best.Info.DownloadUrl))
        {
            var runningExeTime = GetRunningExecutableTimestampUtc();
            if (runningExeTime.HasValue && best.Info.PublishedAt.HasValue &&
                best.Info.PublishedAt.Value.UtcDateTime > runningExeTime.Value.AddMinutes(5))
            {
                var rebuildInfo = best.Info with
                {
                    IsSameVersionRebuild = true,
                    ReleaseNotes = string.IsNullOrWhiteSpace(best.Info.ReleaseNotes)
                        ? $"A newer build of v{best.Info.Version} was published ({best.Info.PublishedAt.Value.ToLocalTime():g})."
                        : best.Info.ReleaseNotes
                };
                return new UpdateCheckResult(rebuildInfo, best.Info, null);
            }
        }

        return new UpdateCheckResult(null, best.Info, null);
    }

    private static (Version Version, UpdateInfo Info)? ParseReleaseElement(JsonElement json, string repo)
    {
        if (json.TryGetProperty("draft", out var draftProp) && draftProp.ValueKind == JsonValueKind.True)
            return null;

        var tagName = json.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
        var relName = json.TryGetProperty("name", out var rn) ? rn.GetString() ?? "" : "";
        var body    = json.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
        var htmlUrl = json.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

        DateTimeOffset? publishedAt = null;
        if (json.TryGetProperty("published_at", out var pubProp) &&
            pubProp.ValueKind == JsonValueKind.String &&
            DateTimeOffset.TryParse(pubProp.GetString(), out var parsedPub))
        {
            publishedAt = parsedPub;
        }

        string exeDownloadUrl = string.Empty;
        string zipDownloadUrl = string.Empty;
        string assetVersionHint = string.Empty;
        bool isPrivateRepo = string.Equals(repo, PrivateRepo, StringComparison.OrdinalIgnoreCase);
        bool hasToken = !string.IsNullOrEmpty(GetGitHubToken());

        if (json.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var assetName = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(assetVersionHint) && !string.IsNullOrEmpty(assetName))
                    assetVersionHint = assetName;

                var browserUrl = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() ?? "" : "";
                var apiUrl = asset.TryGetProperty("url", out var au) ? au.GetString() ?? "" : "";

                // Private repo assets require API URL with Accept: application/octet-stream when using Bearer token
                var chosenUrl = (isPrivateRepo && hasToken && !string.IsNullOrEmpty(apiUrl)) ? apiUrl : browserUrl;
                if (string.IsNullOrEmpty(chosenUrl)) continue;

                if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(exeDownloadUrl) || assetName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                        exeDownloadUrl = chosenUrl;
                }
                else if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(zipDownloadUrl) || assetName.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                        zipDownloadUrl = chosenUrl;
                }
            }
        }

        // Try parsing version from tag_name -> release name -> asset filename
        if (!TryParseFlexibleVersion(tagName, out var ver, out var verStr) &&
            !TryParseFlexibleVersion(relName, out ver, out verStr) &&
            !TryParseFlexibleVersion(assetVersionHint, out ver, out verStr))
        {
            return null;
        }

        string finalDownloadUrl = !string.IsNullOrEmpty(exeDownloadUrl) ? exeDownloadUrl : zipDownloadUrl;
        string effectiveTag = string.IsNullOrWhiteSpace(tagName) ? $"v{verStr}" : tagName;

        return (ver, new UpdateInfo(effectiveTag, verStr, finalDownloadUrl, body, htmlUrl, publishedAt));
    }

    private static async Task<string> ResolveFallbackAssetUrlAsync(string versionStr, CancellationToken ct)
    {
        // 1. Check local workspace build/output first if present
        foreach (var local in ScanLocalBuildOutputs())
        {
            if (string.Equals(local.Info.Version, versionStr, StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(local.Info.DownloadUrl))
            {
                return local.Info.DownloadUrl;
            }
        }

        // 2. Probe direct GitHub release download URLs and raw build/output URLs
        var candidateUrls = new[]
        {
            $"https://github.com/{Owner}/{PublicRepo}/releases/download/v{versionStr}/DeskGroup-Setup-{versionStr}.exe",
            $"https://github.com/{Owner}/{PublicRepo}/releases/download/{versionStr}/DeskGroup-Setup-{versionStr}.exe",
            $"https://github.com/{Owner}/{PublicRepo}/releases/download/v{versionStr}/DeskGroup-Portable-{versionStr}.zip",
            $"https://raw.githubusercontent.com/{Owner}/{PublicRepo}/main/build/output/DeskGroup-Setup-{versionStr}.exe",
            $"https://raw.githubusercontent.com/{Owner}/{PublicRepo}/Dev/build/output/DeskGroup-Setup-{versionStr}.exe"
        };

        foreach (var url in candidateUrls)
        {
            try
            {
                using var req = CreateRequest(HttpMethod.Head, url);
                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return url;
            }
            catch { }
        }

        return string.Empty;
    }

    private static List<(Version ParsedVersion, UpdateInfo Info)> ScanLocalBuildOutputs()
    {
        var results = new List<(Version, UpdateInfo)>();
        var dirs = new[]
        {
            @"C:\Dev\DeskGroup\build\output",
            @"C:\Dev\DeskGroupApp\build\output"
        };

        foreach (var dir in dirs)
        {
            try
            {
                if (!Directory.Exists(dir)) continue;

                foreach (var file in Directory.GetFiles(dir, "DeskGroup-Setup-*.exe"))
                {
                    var name = Path.GetFileName(file);
                    if (TryParseFlexibleVersion(name, out var ver, out var verStr))
                    {
                        var writeTime = new DateTimeOffset(File.GetLastWriteTimeUtc(file), TimeSpan.Zero);
                        results.Add((ver, new UpdateInfo(
                            $"v{verStr}",
                            verStr,
                            file,
                            $"Local installer package ({name})",
                            $"https://github.com/{Owner}/{PublicRepo}/releases",
                            writeTime)));
                    }
                }
            }
            catch { }
        }

        return results;
    }

    private static DateTime? GetRunningExecutableTimestampUtc()
    {
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                return File.GetLastWriteTimeUtc(exePath);
        }
        catch { }
        return null;
    }

    // ── Download ───────────────────────────────────────────────────────────────

    public static async Task<string> DownloadAsync(
        string downloadUrl,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        bool isZip = downloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        var ext = isZip ? ".zip" : ".exe";
        var tempPath = Path.Combine(Path.GetTempPath(), $"DeskGroup-Update-Setup{ext}");

        // Support direct local file path
        if (File.Exists(downloadUrl))
        {
            progress?.Report(50);
            File.Copy(downloadUrl, tempPath, overwrite: true);
            progress?.Report(100);
            return tempPath;
        }

        bool isApiAsset = downloadUrl.Contains("api.github.com/repos/", StringComparison.OrdinalIgnoreCase) &&
                          downloadUrl.Contains("/releases/assets/", StringComparison.OrdinalIgnoreCase);

        using var req = CreateRequest(
            HttpMethod.Get,
            downloadUrl,
            isApiAsset ? "application/octet-stream" : null);

        using var response = await _http.SendAsync(
            req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Check Content-Disposition header in case an API asset URL returned a .zip instead of .exe
        var fileNameHeader = response.Content.Headers.ContentDisposition?.FileName?.Trim('"');
        if (!string.IsNullOrEmpty(fileNameHeader) &&
            fileNameHeader.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
            !tempPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            tempPath = Path.Combine(Path.GetTempPath(), "DeskGroup-Update-Setup.zip");
        }

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        var buffer = new byte[81920];
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

        progress?.Report(100);
        return tempPath;
    }

    // ── Install & Guaranteed Restart ───────────────────────────────────────────

    /// <summary>
    /// Spawns a detached background PowerShell updater script that waits for the current
    /// DeskGroup process to exit, installs the update (.exe installer or .zip portable bundle),
    /// and reliably relaunches DeskGroup.exe.
    /// </summary>
    public static void LaunchInstallerAndShutdown(string installerOrZipPath)
    {
        int currentPid = Environment.ProcessId;
        string currentExe = Environment.ProcessPath
                            ?? Path.Combine(AppContext.BaseDirectory, "DeskGroup.exe");
        string installDir = Path.GetDirectoryName(currentExe) ?? AppContext.BaseDirectory;

        string scriptPath = Path.Combine(Path.GetTempPath(), "DeskGroup-Updater.ps1");
        string escapedPkg = installerOrZipPath.Replace("'", "''");
        string escapedExe = currentExe.Replace("'", "''");
        string escapedDir = installDir.Replace("'", "''");

        var script = new StringBuilder();
        script.AppendLine("$ErrorActionPreference = 'SilentlyContinue'");
        script.AppendLine($"$targetPid = {currentPid}");
        script.AppendLine($"$pkgPath = '{escapedPkg}'");
        script.AppendLine($"$exePath = '{escapedExe}'");
        script.AppendLine($"$installDir = '{escapedDir}'");
        script.AppendLine("for ($i = 0; $i -lt 30; $i++) {");
        script.AppendLine("    if (-not (Get-Process -Id $targetPid -ErrorAction SilentlyContinue)) { break }");
        script.AppendLine("    Start-Sleep -Milliseconds 300");
        script.AppendLine("}");
        script.AppendLine("Get-Process -Id $targetPid -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue");
        script.AppendLine("Start-Sleep -Milliseconds 400");
        script.AppendLine("if ($pkgPath.ToLowerInvariant().EndsWith('.zip')) {");
        script.AppendLine("    $extractDir = Join-Path $env:TEMP 'DeskGroup-Update-Extract'");
        script.AppendLine("    if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }");
        script.AppendLine("    Expand-Archive -Path $pkgPath -DestinationPath $extractDir -Force");
        script.AppendLine("    $setupExe = Get-ChildItem -Path $extractDir -Filter 'DeskGroup-Setup*.exe' -Recurse | Select-Object -First 1");
        script.AppendLine("    if ($setupExe) {");
        script.AppendLine("        Start-Process -FilePath $setupExe.FullName -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS' -Wait");
        script.AppendLine("    } else {");
        script.AppendLine("        Copy-Item -Path \"$extractDir\\*\" -Destination $installDir -Recurse -Force");
        script.AppendLine("    }");
        script.AppendLine("} else {");
        script.AppendLine("    Start-Process -FilePath $pkgPath -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS' -Wait");
        script.AppendLine("}");
        script.AppendLine("Start-Sleep -Milliseconds 500");
        script.AppendLine("if (-not (Get-Process -Name 'DeskGroup' -ErrorAction SilentlyContinue)) {");
        script.AppendLine("    $candidates = @($exePath, (Join-Path $env:LOCALAPPDATA 'Programs\\DeskGroup\\DeskGroup.exe'), (Join-Path $env:ProgramFiles 'DeskGroup\\DeskGroup.exe'))");
        script.AppendLine("    foreach ($c in $candidates) {");
        script.AppendLine("        if ($c -and (Test-Path $c)) { Start-Process -FilePath $c; break }");
        script.AppendLine("    }");
        script.AppendLine("}");

        File.WriteAllText(scriptPath, script.ToString(), Encoding.UTF8);

        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        System.Windows.Application.Current.Shutdown();
    }
}
