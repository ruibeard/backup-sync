using WebDavSync.Portable.Configuration;

namespace WebDavSync.Portable.Sync;

public sealed class SyncService
{
    private readonly System.Windows.Forms.Timer _watchdogTimer;
    private AppConfig? _currentConfig;

    public SyncStatusSnapshot CurrentStatus { get; private set; } =
        new(SyncState.NotConfigured, "Not configured", 0, 0);

    public event EventHandler<SyncStatusSnapshot>? StatusChanged;
    public event EventHandler<string>? ActivityLogged;

    public SyncService()
    {
        _watchdogTimer = new System.Windows.Forms.Timer
        {
            Interval = 5 * 60 * 1000
        };
        _watchdogTimer.Tick += (_, _) => LogActivity("Periodic rescan placeholder tick.");
    }

    public void Start(AppConfig config)
    {
        _currentConfig = config;

        if (!config.HasUsableConfiguration())
        {
            UpdateStatus(SyncState.NotConfigured, "Not configured", 0, 0);
            return;
        }

        _watchdogTimer.Start();
        UpdateStatus(SyncState.Idle, "Watching for changes", 0, 0);
        LogActivity("Sync service started.");
    }

    public void Stop()
    {
        _watchdogTimer.Stop();
        LogActivity("Sync service stopped.");
    }

    public Task SyncNowAsync(CancellationToken cancellationToken = default)
    {
        if (_currentConfig is null || !_currentConfig.HasUsableConfiguration())
        {
            UpdateStatus(SyncState.NotConfigured, "Not configured", 0, 0);
            return Task.CompletedTask;
        }

        UpdateStatus(SyncState.Syncing, "Syncing 0 of 1 files...", 0, 1);
        LogActivity("Manual sync requested.");

        return CompleteSyncPlaceholderAsync(cancellationToken);
    }

    private async Task CompleteSyncPlaceholderAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(500, cancellationToken);
        UpdateStatus(SyncState.Idle, "Watching for changes", 1, 1);
        LogActivity("Sync placeholder completed.");
    }

    private void UpdateStatus(SyncState state, string message, int completed, int total)
    {
        CurrentStatus = new SyncStatusSnapshot(state, message, completed, total);
        StatusChanged?.Invoke(this, CurrentStatus);
    }

    private void LogActivity(string message)
    {
        ActivityLogged?.Invoke(this, $"{DateTime.Now:HH:mm:ss} {message}");
    }
}
