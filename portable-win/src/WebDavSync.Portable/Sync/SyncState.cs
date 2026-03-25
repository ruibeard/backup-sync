namespace WebDavSync.Portable.Sync;

public enum SyncState
{
    NotConfigured,
    Connecting,
    Idle,
    Syncing,
    UpdateAvailable,
    Error
}
