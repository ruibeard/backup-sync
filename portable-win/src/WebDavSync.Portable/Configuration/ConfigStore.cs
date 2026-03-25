using System.IO;
using System.Text.Json;

namespace WebDavSync.Portable.Configuration;

public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly PortablePaths _paths;

    public ConfigStore(PortablePaths paths)
    {
        _paths = paths;
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(_paths.ConfigPath))
            {
                return CreateDefault();
            }

            var json = File.ReadAllText(_paths.ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(_paths.ConfigPath, json);
    }

    public AppConfig CreateDefault()
    {
        const string preferredBackupFolder = @"C:\XDSoftware\backups";

        return new AppConfig
        {
            WatchFolder = Directory.Exists(preferredBackupFolder) ? preferredBackupFolder : string.Empty,
        };
    }
}
