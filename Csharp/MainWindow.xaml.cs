using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfMessageBox = System.Windows.MessageBox;
using WpfColor = System.Windows.Media.Color;

namespace WebDavSync;

public partial class MainWindow : Window
{
    private AppConfig _config;
    private SyncEngine? _syncEngine;

    public MainWindow()
    {
        InitializeComponent();
        _config = ConfigStore.Load();
        LoadIntoControls();

        // Set version label from assembly — always in sync with the build
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        VersionLabel.Text = ver != null
            ? $"WEBDAVSYNC V{ver.Major}.{ver.Minor}.{ver.Build}"
            : "WEBDAVSYNC";

        CheckForUpdateAsync();
    }

    // ── Control loading / saving ─────────────────────────────────────────

    private void LoadIntoControls()
    {
        WatchFolderBox.Text  = _config.WatchFolder;
        UrlBox.Text          = _config.WebDavUrl;
        UsernameBox.Text     = _config.Username;
        PasswordBox.Password = _config.Password;
        RemoteFolderBox.Text = _config.RemoteFolder;
        StartupCheckBox.IsChecked        = _config.StartWithWindows;
        DownloadRemoteCheckBox.IsChecked = _config.DownloadRemoteChanges;

        RefreshConfiguredBadge();
    }

    private void SaveFromControls()
    {
        _config.WatchFolder           = WatchFolderBox.Text.Trim();
        _config.WebDavUrl             = UrlBox.Text.Trim();
        _config.Username              = UsernameBox.Text.Trim();
        _config.Password              = PasswordBox.Password;
        _config.RemoteFolder          = RemoteFolderBox.Text.Trim();
        _config.StartWithWindows      = StartupCheckBox.IsChecked == true;
        _config.DownloadRemoteChanges = DownloadRemoteCheckBox.IsChecked == true;
    }

    // ── Section toggles ──────────────────────────────────────────────────

    private void LocalFolderToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (LocalFolderContent == null) return;
        LocalFolderContent.Visibility = LocalFolderToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ServerToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (ServerContent == null) return;
        ServerContent.Visibility = ServerToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OptionsHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OptionsToggle.IsChecked = !OptionsToggle.IsChecked;
    }

    private void OptionsToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (OptionsContent == null) return;
        OptionsContent.Visibility = OptionsToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ActivityHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        ActivityToggle.IsChecked = !ActivityToggle.IsChecked;
    }

    private void ActivityToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (ActivityContent == null) return;
        ActivityContent.Visibility = ActivityToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    // ── Browse ───────────────────────────────────────────────────────────

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description        = "Select the local folder to sync",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
        };

        if (!string.IsNullOrWhiteSpace(WatchFolderBox.Text))
            dialog.SelectedPath = WatchFolderBox.Text;

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            WatchFolderBox.Text = dialog.SelectedPath;
    }

    private void BrowseRemote_Click(object sender, RoutedEventArgs e)
    {
        SaveFromControls();

        if (string.IsNullOrWhiteSpace(_config.WebDavUrl) ||
            string.IsNullOrWhiteSpace(_config.Username)  ||
            string.IsNullOrWhiteSpace(_config.Password))
        {
            WpfMessageBox.Show("Please enter WebDAV URL, Username, and Password first.",
                "WebDavSync", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var dlg = new RemoteFolderDialog(_config) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedFolder != null)
            RemoteFolderBox.Text = dlg.SelectedFolder;
    }

    // ── Connect ──────────────────────────────────────────────────────────

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        SaveFromControls();

        if (string.IsNullOrWhiteSpace(_config.WebDavUrl) ||
            string.IsNullOrWhiteSpace(_config.Username)  ||
            string.IsNullOrWhiteSpace(_config.Password))
        {
            WpfMessageBox.Show("Please enter WebDAV URL, Username, and Password.",
                "WebDavSync", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConnectBtn.IsEnabled = false;
        SetConnectionStatus(null);

        try
        {
            var client = new WebDavClient(_config);
            await Task.Run(() =>
            {
                if (!client.TestConnection(out string err))
                    throw new Exception(err);
            });
            SetConnectionStatus(true);
            AppendActivity("Connected to WebDAV server");
            RefreshConfiguredBadge();
        }
        catch (Exception ex)
        {
            SetConnectionStatus(false);
            WpfMessageBox.Show(ex.Message, "WebDavSync — Connection Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ConnectBtn.IsEnabled = true;
        }
    }

    private void SetConnectionStatus(bool? connected)
    {
        if (connected == null)
        {
            ConnectionDot.Fill        = new SolidColorBrush(WpfColor.FromRgb(0xAA, 0xAA, 0xAA));
            ConnectionStatusText.Text = "CONNECTING…";
        }
        else if (connected == true)
        {
            ConnectionDot.Fill        = (SolidColorBrush)FindResource("GreenDotBrush");
            ConnectionStatusText.Text = "CONNECTED";
        }
        else
        {
            ConnectionDot.Fill        = (SolidColorBrush)FindResource("RedDotBrush");
            ConnectionStatusText.Text = "NOT CONNECTED";
        }
    }

    // ── Open URL ─────────────────────────────────────────────────────────

    private void OpenUrl_Click(object sender, RoutedEventArgs e)
    {
        var url = UrlBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            WpfMessageBox.Show("WebDAV URL is empty.", "WebDavSync",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    // ── Save ─────────────────────────────────────────────────────────────

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveFromControls();

        if (!ValidateConfig(out string err))
        {
            WpfMessageBox.Show(err, "WebDavSync", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var client = new WebDavClient(_config);
            await Task.Run(() =>
            {
                if (!client.TestConnection(out string cerr))
                    throw new Exception(cerr);
            });
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(ex.Message, "WebDavSync — Connection Failed",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        ConfigStore.Save(_config);
        ApplyStartupSetting();
        SetConnectionStatus(true);
        RefreshConfiguredBadge();
        RestartSyncEngine();

        AppendActivity("Configuration saved. Watching for changes.");
    }

    // ── Close ─────────────────────────────────────────────────────────────

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true; // tray app — hide instead of close
        Hide();
    }

    // ── Update ────────────────────────────────────────────────────────────

    // Called on startup (silent — no UI noise if up to date).
    private async void CheckForUpdateAsync()
    {
        try
        {
            var (hasUpdate, downloadUrl, newVersion, sha256) = await UpdateService.CheckAsync();
            if (hasUpdate)
                ShowUpdateAvailable(downloadUrl, newVersion, sha256);
        }
        catch { /* silently ignore on startup */ }
    }

    // Called by the CHECK button and by the tray "Check for Updates" item.
    public async Task RunUpdateCheckAsync(bool manual = false)
    {
        CheckUpdatesBtn.IsEnabled    = false;
        CheckUpdatesBtnText.Text     = "CHECKING…";

        try
        {
            var (hasUpdate, downloadUrl, newVersion, sha256) = await UpdateService.CheckAsync();

            if (hasUpdate)
            {
                ShowUpdateAvailable(downloadUrl, newVersion, sha256);
            }
            else
            {
                // Always show "up to date" feedback — in the label and activity log
                UpdateAvailableLabel.Text       = "up to date";
                UpdateAvailableLabel.Foreground = System.Windows.Media.Brushes.Gray;
                UpdateAvailableLabel.Visibility = Visibility.Visible;
                if (manual)
                    AppendActivity("Already up to date.");
            }
        }
        catch (Exception ex)
        {
            UpdateAvailableLabel.Text       = "check failed";
            UpdateAvailableLabel.Foreground = System.Windows.Media.Brushes.OrangeRed;
            UpdateAvailableLabel.Visibility = Visibility.Visible;
            if (manual)
                AppendActivity($"Update check failed: {ex.Message}");
        }
        finally
        {
            CheckUpdatesBtn.IsEnabled = true;
            CheckUpdatesBtnText.Text  = "CHECK";
        }
    }

    private void ShowUpdateAvailable(string downloadUrl, string newVersion, string sha256)
    {
        // Guard: don't duplicate if already shown
        if (InstallUpdateBtn.Visibility == Visibility.Visible) return;

        UpdateAvailableLabel.Text       = $"v{newVersion} available";
        UpdateAvailableLabel.Foreground = new SolidColorBrush(WpfColor.FromRgb(0x2B, 0x4F, 0xA3));
        UpdateAvailableLabel.Visibility = Visibility.Visible;
        InstallUpdateBtn.Tag            = (downloadUrl, sha256);
        InstallUpdateBtn.Visibility     = Visibility.Visible;
        AppendActivity($"Update available: v{newVersion}");
    }

    private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        => await RunUpdateCheckAsync(manual: true);

    private async void InstallUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (InstallUpdateBtn.Tag is not (string url, string sha256)) return;
        if (string.IsNullOrWhiteSpace(url)) return;

        var result = WpfMessageBox.Show(
            "Download and install the update now?\n\nThe app will restart after the update.",
            "WebDavSync Update", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes) return;

        InstallUpdateBtn.IsEnabled = false;
        CheckUpdatesBtn.IsEnabled  = false;
        AppendActivity("Downloading update…");

        try
        {
            await UpdateService.DownloadAndReplaceAsync(url, sha256,
                progress => Dispatcher.Invoke(() => AppendActivity($"Download: {progress}%")));

            WpfMessageBox.Show("Update downloaded. The app will now restart.",
                "WebDavSync", MessageBoxButton.OK, MessageBoxImage.Information);

            UpdateService.RestartWithUpdatedBinary();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show($"Update failed: {ex.Message}", "WebDavSync",
                MessageBoxButton.OK, MessageBoxImage.Error);
            InstallUpdateBtn.IsEnabled = true;
            CheckUpdatesBtn.IsEnabled  = true;
        }
    }

    /// <summary>Called by App.xaml.cs tray "Check for Updates" after opening the window.</summary>
    public void NotifyUpdateAvailable(string downloadUrl, string newVersion, string sha256 = "")
    {
        Dispatcher.Invoke(() => ShowUpdateAvailable(downloadUrl, newVersion, sha256));
    }

    /// <summary>Triggers an immediate sync pass. Called from the tray "Sync Now" item.</summary>
    public void TriggerSyncNow()
    {
        if (_syncEngine == null)
        {
            AppendActivity("Sync Now: engine not running — save configuration first.");
            return;
        }
        RestartSyncEngine();
        AppendActivity("Sync Now triggered from tray.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private bool ValidateConfig(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(_config.WatchFolder))  { error = "Folder is required.";        return false; }
        if (string.IsNullOrWhiteSpace(_config.WebDavUrl))    { error = "URL is required.";            return false; }
        if (string.IsNullOrWhiteSpace(_config.Username))     { error = "Username is required.";       return false; }
        if (string.IsNullOrWhiteSpace(_config.Password))     { error = "Password is required.";       return false; }
        if (string.IsNullOrWhiteSpace(_config.RemoteFolder)) { error = "Remote folder is required.";  return false; }
        if (!System.IO.Directory.Exists(_config.WatchFolder)){ error = "Local folder does not exist."; return false; }
        return true;
    }

    private void RefreshConfiguredBadge()
    {
        bool configured = !string.IsNullOrWhiteSpace(_config.WebDavUrl) &&
                          !string.IsNullOrWhiteSpace(_config.Username);
        NotConfiguredPanel.Visibility = configured ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyStartupSetting()
    {
        const string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(keyPath, true);
        if (key == null) return;

        if (_config.StartWithWindows)
            key.SetValue("WebDavSync", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("WebDavSync", throwOnMissingValue: false);
    }

    private void RestartSyncEngine()
    {
        _syncEngine?.Stop();
        _syncEngine = new SyncEngine(_config, AppendActivity);
        _syncEngine.Start();
    }

    public void AppendActivity(string text)
    {
        Dispatcher.Invoke(() =>
        {
            var ts = DateTime.Now.ToString("HH:mm:ss");
            ActivityList.Items.Insert(0, $"{ts}  {text}");
            if (ActivityList.Items.Count > 200)
                ActivityList.Items.RemoveAt(ActivityList.Items.Count - 1);

            Logger.Write(text);
        });
    }
}
