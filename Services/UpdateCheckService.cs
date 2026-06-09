using System;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace LenovoTray.Services;

internal static class UpdateCheckService
{
    // Read from the assembly manifest so the version never drifts out of sync with the csproj.
    private static readonly Version CurrentVersion =
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    /// <summary>
    /// The latest version string retrieved from GitHub, or null if the check has not run
    /// or failed.
    /// </summary>
    public static string? LatestVersion { get; private set; }

    private const string ReleasesPageUrl = "https://github.com/0z00z0/LenovoPowerTray/releases";

    public enum UpdateStatus { UpToDate, Available, NoReleases, Error }

    /// <summary>Result of an on-demand "Check for updates" request.</summary>
    public readonly record struct CheckOutcome(UpdateStatus Status, string? LatestVersion, string ReleaseUrl);

    /// <summary>
    /// On-demand update check that reports every outcome (up-to-date / available / no releases /
    /// error) so a menu action can show a result either way. Never throws.
    /// </summary>
    public static async Task<CheckOutcome> CheckNowAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/repos/0z00z0/LenovoPowerTray/releases/latest");
            request.Headers.UserAgent.ParseAdd("LenovoPowerTray/1.0");

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            // GitHub returns 404 when a repo has tags but no published releases yet.
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new(UpdateStatus.NoReleases, null, ReleasesPageUrl);

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var releaseUrl = root.TryGetProperty("html_url", out var h)
                ? h.GetString() ?? ReleasesPageUrl : ReleasesPageUrl;

            if (!root.TryGetProperty("tag_name", out var tagEl) ||
                tagEl.GetString() is not { Length: > 0 } tag ||
                !Version.TryParse(tag.TrimStart('v'), out var remote))
                return new(UpdateStatus.Error, null, releaseUrl);

            LatestVersion = tag.TrimStart('v');
            return remote > CurrentVersion
                ? new(UpdateStatus.Available, LatestVersion, releaseUrl)
                : new(UpdateStatus.UpToDate, CurrentVersion.ToString(3), releaseUrl);
        }
        catch
        {
            return new(UpdateStatus.Error, null, ReleasesPageUrl);
        }
    }

    /// <summary>
    /// Performs a single update check against the GitHub releases API.
    /// Invokes <paramref name="onUpdateAvailable"/> with the new version string if a newer
    /// release exists. Network failures are silently swallowed.
    /// </summary>
    public static async Task CheckAsync(Action<string> onUpdateAvailable)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                "https://api.github.com/repos/0z00z0/LenovoPowerTray/releases/latest");

            request.Headers.UserAgent.ParseAdd("LenovoPowerTray/1.0");

            using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("tag_name", out var tagNameElement))
                return;

            var tagName = tagNameElement.GetString();
            if (string.IsNullOrWhiteSpace(tagName))
                return;

            // Strip leading 'v' (e.g. "v1.2.3" -> "1.2.3")
            var versionString = tagName.TrimStart('v');

            if (!Version.TryParse(versionString, out var remoteVersion))
                return;

            LatestVersion = versionString;

            if (remoteVersion > CurrentVersion)
            {
                onUpdateAvailable(versionString);
            }
        }
        catch
        {
            // Network or parse failures must never propagate to the caller.
        }
    }
}
