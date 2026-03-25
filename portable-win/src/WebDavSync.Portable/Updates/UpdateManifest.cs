namespace WebDavSync.Portable.Updates;

public sealed class UpdateManifest
{
    public string Version { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public string ReleaseNotesUrl { get; set; } = string.Empty;
}
