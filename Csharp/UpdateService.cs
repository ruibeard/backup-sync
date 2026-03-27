using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace WebDavSync;

public static class UpdateService
{
    private const string AppcastUrl =
        "https://raw.githubusercontent.com/ruibeard/backup-sync-tool/main/appcast.json";

    // Read the version from the assembly so it always matches what was built,
    // rather than a separate hardcoded constant that can drift out of sync.
    private static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static async Task<(bool HasUpdate, string DownloadUrl, string NewVersion, string Sha256)> CheckAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        var ver = CurrentVersion;
        http.DefaultRequestHeaders.Add("User-Agent", $"WebDavSync/{ver.Major}.{ver.Minor}.{ver.Build}");

        var json = await http.GetStringAsync(AppcastUrl);
        using var doc = JsonDocument.Parse(json);

        var versionStr  = doc.RootElement.GetProperty("version").GetString() ?? "0.0.0";
        var downloadUrl = doc.RootElement.GetProperty("downloadUrl").GetString() ?? string.Empty;
        var sha256      = doc.RootElement.TryGetProperty("sha256", out var sha256El)
                            ? (sha256El.GetString() ?? string.Empty)
                            : string.Empty;

        if (Version.TryParse(versionStr, out var remote) && remote > CurrentVersion)
            return (true, downloadUrl, versionStr, sha256);

        return (false, string.Empty, string.Empty, string.Empty);
    }

    public static async Task DownloadAndReplaceAsync(string downloadUrl,
        string expectedSha256,
        Action<int>? progressCallback = null)
    {
        var exePath    = Environment.ProcessPath!;
        var backupPath = exePath + ".bak";
        var tmpPath    = exePath + ".new";

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        using var resp = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        var total     = resp.Content.Headers.ContentLength ?? -1;
        long received = 0;

        await using (var stream = await resp.Content.ReadAsStreamAsync())
        await using (var fs     = System.IO.File.Create(tmpPath))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                received += read;
                if (total > 0)
                    progressCallback?.Invoke((int)(received * 100 / total));
            }
        }

        // Verify SHA-256 if the manifest provided one
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            var actualHash = ComputeSha256(tmpPath);
            if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                System.IO.File.Delete(tmpPath);
                throw new Exception(
                    $"SHA-256 verification failed.\nExpected: {expectedSha256}\nActual:   {actualHash}");
            }
        }

        // Backup current, replace with new
        if (System.IO.File.Exists(backupPath))
            System.IO.File.Delete(backupPath);
        System.IO.File.Move(exePath, backupPath);
        System.IO.File.Move(tmpPath, exePath);
    }

    public static void RestartWithUpdatedBinary()
    {
        var exePath = Environment.ProcessPath!;
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
        {
            UseShellExecute = true,
        });
        System.Windows.Application.Current.Shutdown();
    }

    private static string ComputeSha256(string filePath)
    {
        using var sha = SHA256.Create();
        using var fs  = System.IO.File.OpenRead(filePath);
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
