using System.IO;
using WebDavSync.Portable.Configuration;
using WebDavSync.Portable.Secrets;
using WebDavSync.Portable.Sync;
using WebDavSync.Portable.Ui;
using WebDavSync.Portable.Updates;
using System.Windows.Forms;

namespace WebDavSync.Portable.Windows;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly ConfigStore _configStore;
    private readonly SecretStore _secretStore;
    private readonly SyncService _syncService;
    private readonly UpdateService _updateService;
    private readonly StartupRegistration _startupRegistration;
    private readonly PortablePaths _paths;
    private readonly NotifyIcon _notifyIcon;
    private readonly SettingsWindow _settingsWindow;
    private readonly string? _loadedPassword;

    private AppConfig _config;

    public TrayAppContext(
        ConfigStore configStore,
        SecretStore secretStore,
        SyncService syncService,
        UpdateService updateService,
        StartupRegistration startupRegistration,
        PortablePaths paths)
    {
        _configStore = configStore;
        _secretStore = secretStore;
        _syncService = syncService;
        _updateService = updateService;
        _startupRegistration = startupRegistration;
        _paths = paths;

        _config = _configStore.Load();
        _loadedPassword = _secretStore.LoadPassword(_config.SecretId);

        _settingsWindow = new SettingsWindow();
        _settingsWindow.SaveRequested += (_, _) => SaveSettings();
        _settingsWindow.ConnectRequested += (_, _) => _ = TestConnectionAsync();
        _settingsWindow.CheckUpdatesRequested += (_, _) => _ = CheckForUpdatesAsync();
        _settingsWindow.LoadConfig(_config, _loadedPassword);

        _syncService.StatusChanged += (_, status) => _settingsWindow.UpdateStatus(status);
        _syncService.ActivityLogged += (_, line) => _settingsWindow.AppendActivity(line);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Settings", null, (_, _) => _ = ShowSettingsAsync());
        menu.Items.Add("Sync Now", null, (_, _) => _ = RunSyncNowAsync());
        menu.Items.Add("Check for Updates", null, (_, _) => _ = CheckForUpdatesAsync());
        menu.Items.Add("Open Logs", null, (_, _) => OpenLogsFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _notifyIcon = new NotifyIcon
        {
            Text = "WebDavSync Portable",
            Visible = true,
            Icon = SystemIcons.Application,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => _ = ShowSettingsAsync();

        Directory.CreateDirectory(_paths.LogsDirectory);

        _syncService.Start(_config);

        if (_config.HasUsableConfiguration() && !string.IsNullOrWhiteSpace(_loadedPassword))
        {
            _settingsWindow.Hide();
        }
        else
        {
            _settingsWindow.AppendActivity($"{DateTime.Now:HH:mm:ss} Configuration incomplete. Review settings before starting sync.");
            _ = ShowSettingsAsync();
        }
    }

    protected override void ExitThreadCore()
    {
        _syncService.Stop();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _settingsWindow.Close();
        base.ExitThreadCore();
    }

    private async Task ShowSettingsAsync()
    {
        await _settingsWindow.Dispatcher.InvokeAsync(() =>
        {
            _settingsWindow.Show();
            _settingsWindow.Activate();
            _settingsWindow.WindowState = System.Windows.WindowState.Normal;
        });
    }

    private void SaveSettings()
    {
        var newConfig = _settingsWindow.ReadConfig();
        var password = _settingsWindow.ReadPassword();
        var existingPassword = _secretStore.LoadPassword(_config.SecretId);

        if (string.IsNullOrWhiteSpace(newConfig.WatchFolder) ||
            string.IsNullOrWhiteSpace(newConfig.WebDavUrl) ||
            string.IsNullOrWhiteSpace(newConfig.Username) ||
            string.IsNullOrWhiteSpace(newConfig.RemoteFolder))
        {
            System.Windows.MessageBox.Show(
                "Local folder, WebDAV URL, username, and remote folder are required.",
                "WebDavSync",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(existingPassword))
        {
            System.Windows.MessageBox.Show(
                "A password is required on first save or after the stored secret is lost.",
                "WebDavSync",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (!string.IsNullOrWhiteSpace(password))
        {
            newConfig.SecretId = _secretStore.SavePassword(password, _config.SecretId);
        }
        else
        {
            newConfig.SecretId = _config.SecretId;
        }

        _configStore.Save(newConfig);
        _startupRegistration.Apply(newConfig.StartWithWindows);

        _config = newConfig;
        _syncService.Stop();
        _syncService.Start(_config);
        _settingsWindow.AppendActivity($"{DateTime.Now:HH:mm:ss} Configuration saved.");
    }

    private async Task RunSyncNowAsync()
    {
        try
        {
            await _syncService.SyncNowAsync();
        }
        catch (Exception ex)
        {
            _settingsWindow.AppendActivity($"{DateTime.Now:HH:mm:ss} Sync failed: {ex.Message}");
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        _settingsWindow.AppendActivity($"{DateTime.Now:HH:mm:ss} Checking for updates...");

        var result = await _updateService.CheckForUpdatesAsync();

        _settingsWindow.AppendActivity($"{DateTime.Now:HH:mm:ss} {result.Message}");

        System.Windows.MessageBox.Show(
            result.Message,
            "WebDavSync — Updates",
            System.Windows.MessageBoxButton.OK,
            result.IsUpdateAvailable
                ? System.Windows.MessageBoxImage.Information
                : System.Windows.MessageBoxImage.None);
    }

    private void OpenLogsFolder()
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = _paths.LogsDirectory,
            UseShellExecute = true
        });
    }

    private async Task TestConnectionAsync()
    {
        _settingsWindow.SetConnectionStatus(false, "CONNECTING...");
        await Task.Delay(500); // Simulate connection test

        // TODO: Implement actual WebDAV connection test
        // For now, just show not connected
        _settingsWindow.SetConnectionStatus(false, "NOT CONNECTED");
        _settingsWindow.AppendActivity($"{DateTime.Now:HH:mm:ss} Connection test completed (placeholder).");
    }
}
