namespace WebDavSync.Portable.Configuration;

public sealed class AppConfig
{
    public string WatchFolder { get; set; } = string.Empty;
    public string WebDavUrl { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string RemoteFolder { get; set; } = string.Empty;
    public string SecretId { get; set; } = string.Empty;
    public bool StartWithWindows { get; set; }
    public bool SyncRemoteChanges { get; set; }
    public bool HasUsableConfiguration()
    {
        return !string.IsNullOrWhiteSpace(WatchFolder) &&
               !string.IsNullOrWhiteSpace(WebDavUrl) &&
               !string.IsNullOrWhiteSpace(Username) &&
               !string.IsNullOrWhiteSpace(RemoteFolder);
    }
}
