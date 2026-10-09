using System.IO;
using System.Text.Json;
using Nexora.Shared.Dtos;

namespace Nexora.Desktop.Services;

/// <summary>Nicht-geheime Einstellungen (API-URL, Gerätekennung).</summary>
public class AppSettings
{
    public string ApiUrl { get; set; } = "http://localhost:5199";
    public string DeviceIdentifier { get; set; } = "";
    public string DeviceName { get; set; } = "NexoraAI Desktop (" + Environment.MachineName + ")";

    public static AppSettings Load()
    {
        try
        {
            var path = SecureTokenStorage.SettingsFile;
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json);
                if (loaded != null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.DeviceIdentifier))
                        loaded.DeviceIdentifier = Guid.NewGuid().ToString("N");
                    if (string.IsNullOrWhiteSpace(loaded.ApiUrl))
                        loaded.ApiUrl = "http://localhost:5199";
                    return loaded;
                }
            }
        }
        catch { /* fehlerhafte settings.json -> Standardwerte */ }

        return new AppSettings { DeviceIdentifier = Guid.NewGuid().ToString("N") };
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SecureTokenStorage.SettingsFile);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(SecureTokenStorage.SettingsFile, JsonSerializer.Serialize(this, Json));
        }
        catch { /* Schreiben darf die Anwendung nicht blockieren */ }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}

/// <summary>Persistierter Sitzungszustand (token auf Platte via DPAPI).</summary>
public class StoredSession
{
    public string ApiUrl { get; set; } = "";
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = new();
    public string DeviceIdentifier { get; set; } = "";
    public string DeviceName { get; set; } = "";
}