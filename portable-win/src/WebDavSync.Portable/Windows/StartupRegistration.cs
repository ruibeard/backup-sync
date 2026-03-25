using Microsoft.Win32;
using WebDavSync.Portable.Configuration;

namespace WebDavSync.Portable.Windows;

public sealed class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WebDavSyncPortable";

    private readonly PortablePaths _paths;

    public StartupRegistration(PortablePaths paths)
    {
        _paths = paths;
    }

    public void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(RunValueName, $"\"{_paths.ExecutablePath}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }
}
