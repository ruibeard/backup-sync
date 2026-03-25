namespace WebDavSync.Portable.Updates;

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    UpdateManifest? Manifest,
    string Message);
