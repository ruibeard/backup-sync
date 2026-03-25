using WebDavSync.Portable.Configuration;
using WebDavSync.Portable.Secrets;
using WebDavSync.Portable.Sync;
using WebDavSync.Portable.Updates;
using WebDavSync.Portable.Windows;

namespace WebDavSync.Portable;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var paths = PortablePaths.Detect();
        var secretStore = new SecretStore(paths);
        var configStore = new ConfigStore(paths);
        var syncService = new SyncService();
        var updateService = new UpdateService();
        var startupRegistration = new StartupRegistration(paths);

        var appContext = new TrayAppContext(
            configStore,
            secretStore,
            syncService,
            updateService,
            startupRegistration,
            paths);

        Application.Run(appContext);
    }
}
