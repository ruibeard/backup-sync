namespace WebDavSync.Portable.Sync;

public sealed record SyncStatusSnapshot(
    SyncState State,
    string Message,
    int Completed,
    int Total);
