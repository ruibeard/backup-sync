using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using WebDavSync.Portable.Configuration;
using WebDavSync.Portable.Sync;

namespace WebDavSync.Portable.Ui;

public partial class SettingsWindow : Window
{
    private bool _activityExpanded = false;

    public event EventHandler? SaveRequested;
    public event EventHandler? ConnectRequested;
    public event EventHandler? CheckUpdatesRequested;

    public SettingsWindow()
    {
        // ModernWpf requires its resource dictionaries to be merged before InitializeComponent.
        // Normally this happens via App.xaml, but we use a WinForms ApplicationContext so we
        // do it here manually.
        Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/ModernWpfUI;component/Styles/Styles.xaml")
        });
        Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/ModernWpfUI;component/Styles/Default/Resources.xaml")
        });

        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionTextBlock.Text = $"v{version?.Major}.{version?.Minor}";

        const string defaultFolder = @"C:\XDSoftware\backups";
        if (Directory.Exists(defaultFolder) && string.IsNullOrEmpty(WatchFolderTextBox.Text))
            WatchFolderTextBox.Text = defaultFolder;
    }

    // ── Public API ──────────────────────────────────────────────────────────

    public AppConfig ReadConfig() => new AppConfig
    {
        WatchFolder         = WatchFolderTextBox.Text.Trim(),
        WebDavUrl           = WebDavUrlTextBox.Text.Trim(),
        Username            = UsernameTextBox.Text.Trim(),
        RemoteFolder        = RemoteFolderTextBox.Text.Trim(),
        StartWithWindows    = StartWithWindowsCheckBox.IsChecked ?? false,
        SyncRemoteChanges   = SyncRemoteChangesCheckBox.IsChecked ?? false,
    };

    public string ReadPassword() => PasswordTextBox.Password;

    public void LoadConfig(AppConfig config, string? password)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => LoadConfig(config, password)); return; }

        WatchFolderTextBox.Text              = config.WatchFolder;
        WebDavUrlTextBox.Text                = config.WebDavUrl;
        UsernameTextBox.Text                 = config.Username;
        PasswordTextBox.Password             = password ?? string.Empty;
        RemoteFolderTextBox.Text             = config.RemoteFolder;
        StartWithWindowsCheckBox.IsChecked   = config.StartWithWindows;
        SyncRemoteChangesCheckBox.IsChecked  = config.SyncRemoteChanges;

        const string defaultFolder = @"C:\XDSoftware\backups";
        if (string.IsNullOrWhiteSpace(WatchFolderTextBox.Text) && Directory.Exists(defaultFolder))
            WatchFolderTextBox.Text = defaultFolder;
    }

    public void AppendActivity(string line)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => AppendActivity(line)); return; }
        ActivityTextBox.AppendText(line + Environment.NewLine);
        ActivityTextBox.ScrollToEnd();
    }

    public void UpdateStatus(SyncStatusSnapshot snapshot) { }

    public void SetConnectionStatus(bool connected, string message)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => SetConnectionStatus(connected, message)); return; }

        var green = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 163, 74));
        var red   = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 51,  51));

        ConnectionStatusEllipse.Fill    = connected ? green : red;
        ConnectionStatusText.Text       = message;
        ConnectionStatusText.Foreground = connected ? green : red;
    }

    // ── Section toggles ─────────────────────────────────────────────────────

    private void ActivityHeader_Click(object sender, RoutedEventArgs e)
    {
        _activityExpanded = !_activityExpanded;
        ActivityPanel.Visibility = _activityExpanded ? Visibility.Visible : Visibility.Collapsed;
        ActivityChevron.Text = _activityExpanded ? "\uE70E" : "\uE70D";
    }

    // ── Button handlers ──────────────────────────────────────────────────────

    private void BrowseLocalFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dlg = new FolderBrowserDialog
        {
            Description        = "Select local sync folder",
            UseDescriptionForTitle = true,
        };
        if (Directory.Exists(WatchFolderTextBox.Text))
            dlg.SelectedPath = WatchFolderTextBox.Text;
        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            WatchFolderTextBox.Text = dlg.SelectedPath;
    }

    private void BrowseRemoteFolder_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.MessageBox.Show(
            "Type the remote path manually (e.g. /backups/documents).\nBrowsing will be available once connected.",
            "WebDavSync", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ConnectButton_Click(object sender, RoutedEventArgs e)
        => ConnectRequested?.Invoke(this, EventArgs.Empty);

    private void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        => CheckUpdatesRequested?.Invoke(this, EventArgs.Empty);

    private void SaveButton_Click(object sender, RoutedEventArgs e)
        => SaveRequested?.Invoke(this, EventArgs.Empty);

    private void CloseButton_Click(object sender, RoutedEventArgs e)
        => Hide();

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
