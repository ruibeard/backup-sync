using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace WebDavSync.Portable.Updates;

public sealed class UpdateService
{
    private readonly HttpClient _httpClient = new();

    private const string ManifestUrl = "https://raw.githubusercontent.com/ruibeard/backup-sync-tool/main/appcast.json";

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync(ManifestUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var manifest = await JsonSerializer.DeserializeAsync<UpdateManifest>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                return new UpdateCheckResult(false, null, "Update manifest was empty.");
            }

            var currentVersion = GetCurrentVersion();
            if (!Version.TryParse(manifest.Version, out var availableVersion))
            {
                return new UpdateCheckResult(false, null, "Update manifest contained an invalid version.");
            }

            var isNewer = availableVersion > currentVersion;
            return new UpdateCheckResult(
                isNewer,
                manifest,
                isNewer
                    ? $"Version {manifest.Version} is available."
                    : $"Already on the latest version ({currentVersion}).");
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult(false, null, $"Update check failed: {ex.Message}");
        }
    }

    private static Version GetCurrentVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
    }
}
