using System.IO;

namespace WebDavSync.Portable.Configuration;

public sealed class PortablePaths
{
    public string RootDirectory { get; }
    public string ExecutablePath { get; }
    public string ConfigPath { get; }
    public string LogsDirectory { get; }
    public string SecretsDirectory { get; }
    public string UpdateDirectory { get; }

    private PortablePaths(
        string rootDirectory,
        string executablePath,
        string configPath,
        string logsDirectory,
        string secretsDirectory,
        string updateDirectory)
    {
        RootDirectory = rootDirectory;
        ExecutablePath = executablePath;
        ConfigPath = configPath;
        LogsDirectory = logsDirectory;
        SecretsDirectory = secretsDirectory;
        UpdateDirectory = updateDirectory;
    }

    public static PortablePaths Detect()
    {
        var executablePath = Environment.ProcessPath ?? Application.ExecutablePath;
        var rootDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;

        return new PortablePaths(
            rootDirectory,
            executablePath,
            Path.Combine(rootDirectory, "config.json"),
            Path.Combine(rootDirectory, "logs"),
            Path.Combine(rootDirectory, "secrets"),
            Path.Combine(rootDirectory, "updates"));
    }
}
