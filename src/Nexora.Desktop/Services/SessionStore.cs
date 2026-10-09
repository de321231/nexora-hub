using System.Text.Json;
using Nexora.Shared.Dtos;

namespace Nexora.Desktop.Services;

/// <summary>
/// Verwaltet den Sitzungszustand inkl. DPAPI-verschlüsselter Persistenz und
/// informiert die App über Änderungen (Login/Logout/Token-Refresh).
/// </summary>
public class SessionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private StoredSession? _session;

    public SessionStore(AppSettings settings)
    {
        Settings = settings;
        _session = Deserialize(SecureTokenStorage.LoadSessionJson());
        if (_session != null)
        {
            // Einstellungen übernehmen (neue API-URL, falls geändert)
            if (string.IsNullOrWhiteSpace(_session.ApiUrl))
                _session.ApiUrl = Settings.ApiUrl;
            if (string.IsNullOrWhiteSpace(_session.DeviceIdentifier))
                _session.DeviceIdentifier = Settings.DeviceIdentifier;
            if (string.IsNullOrWhiteSpace(_session.DeviceName))
                _session.DeviceName = Settings.DeviceName;
        }
    }

    public AppSettings Settings { get; }

    public UserDto? User => _session?.User;

    public string BaseUrl => _session?.ApiUrl ?? Settings.ApiUrl;

    public string AccessToken => _session?.AccessToken ?? "";

    public string? RefreshToken => _session?.RefreshToken;

    public string DeviceIdentifier => _session?.DeviceIdentifier ?? Settings.DeviceIdentifier;

    public string DeviceName => _session?.DeviceName ?? Settings.DeviceName;

    public bool HasSession =>
        _session != null && !string.IsNullOrWhiteSpace(_session.AccessToken) &&
        !string.IsNullOrWhiteSpace(_session.RefreshToken);

    public DateTime? AccessTokenExpiresAtUtc => _session?.AccessTokenExpiresAtUtc;

    /// <summary>Ereignis bei Login, Logout oder Token-Refresh.</summary>
    public event Action? Changed;

    public void ApplyAuth(AuthResponse auth)
    {
        _session = new StoredSession
        {
            ApiUrl = BaseUrl,
            AccessToken = auth.AccessToken,
            RefreshToken = auth.RefreshToken,
            AccessTokenExpiresAtUtc = auth.AccessTokenExpiresAtUtc,
            User = auth.User,
            DeviceIdentifier = DeviceIdentifier,
            DeviceName = DeviceName
        };
        Settings.ApiUrl = BaseUrl;
        Settings.Save();
        Persist();
        Changed?.Invoke();
    }

    /// <summary>Zugriffstoken ersetzen (Refresh).</summary>
    public void UpdateTokens(string accessToken, string refreshToken, DateTime expiresAtUtc)
    {
        if (_session == null) return;
        _session.AccessToken = accessToken;
        _session.RefreshToken = refreshToken;
        _session.AccessTokenExpiresAtUtc = expiresAtUtc;
        Persist();
        Changed?.Invoke();
    }

    public void UpdateBaseUrl(string url)
    {
        if (_session != null) _session.ApiUrl = url;
    }

    public void Clear()
    {
        _session = null;
        SecureTokenStorage.ClearSession();
        Changed?.Invoke();
    }

    private void Persist() => SecureTokenStorage.SaveSessionJson(JsonSerializer.Serialize(_session, Json));

    private static StoredSession? Deserialize(string? json) =>
        !string.IsNullOrWhiteSpace(json) ? JsonSerializer.Deserialize<StoredSession>(json, Json) : null;
}