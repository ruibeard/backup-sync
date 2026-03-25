using System.IO;
using System.Security.Cryptography;
using WebDavSync.Portable.Configuration;

namespace WebDavSync.Portable.Secrets;

public sealed class SecretStore
{
    private readonly PortablePaths _paths;

    public SecretStore(PortablePaths paths)
    {
        _paths = paths;
    }

    public string SavePassword(string password, string? secretId = null)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password is required.", nameof(password));
        }

        Directory.CreateDirectory(_paths.SecretsDirectory);

        var id = string.IsNullOrWhiteSpace(secretId) ? Guid.NewGuid().ToString("N") : secretId;
        var filePath = GetSecretPath(id);
        var plainBytes = System.Text.Encoding.UTF8.GetBytes(password);
        var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(filePath, protectedBytes);
        return id;
    }

    public string? LoadPassword(string secretId)
    {
        if (string.IsNullOrWhiteSpace(secretId))
        {
            return null;
        }

        var filePath = GetSecretPath(secretId);
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var protectedBytes = File.ReadAllBytes(filePath);
            var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    private string GetSecretPath(string secretId)
    {
        return Path.Combine(_paths.SecretsDirectory, $"{secretId}.bin");
    }
}
