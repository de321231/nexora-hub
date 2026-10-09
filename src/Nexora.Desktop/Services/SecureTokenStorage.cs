using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Nexora.Desktop.Services;

/// <summary>
/// Verschlüsselter Sitzungsspeicher (DPAPI, CurrentUser). Zugangs- und
/// Refresh-Tokens liegen nur als Chiffrat auf der Platte.
/// </summary>
public static class SecureTokenStorage
{
    private static readonly string AppDataDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NexoraAI");

    private static readonly string SessionFile = Path.Combine(AppDataDir, "session.dat");

    public static string? LoadSessionJson()
    {
        try
        {
            if (!File.Exists(SessionFile)) return null;
            var data = ProtectedData.Unprotect(File.ReadAllBytes(SessionFile), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveSessionJson(string json)
    {
        Directory.CreateDirectory(AppDataDir);
        var data = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(SessionFile, data);
    }

    public static void ClearSession()
    {
        try { if (File.Exists(SessionFile)) File.Delete(SessionFile); } catch { /* bewusst ignorieren */ }
    }

    public static string SettingsFile => Path.Combine(AppDataDir, "settings.json");
}