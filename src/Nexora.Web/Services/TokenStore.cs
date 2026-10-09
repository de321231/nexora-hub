using Nexora.Shared.Dtos;

namespace Nexora.Web.Services;

/// <summary>
/// Hält die Sitzung (JWT + Benutzer) serverseitig pro Blazor-Circuit.
/// Der Token wird nie an den Browser ausgeliefert und niemals in Cookies gespeichert.
/// </summary>
public class TokenStore
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime AccessTokenExpiresAtUtc { get; private set; }
    public UserDto? User { get; private set; }

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && User != null;
    public bool IsAdmin => User?.Roles.Contains("Admin", StringComparer.OrdinalIgnoreCase) ?? false;

    public event Action? Changed;

    public void Set(AuthResponse auth)
    {
        AccessToken = auth.AccessToken;
        RefreshToken = auth.RefreshToken;
        AccessTokenExpiresAtUtc = auth.AccessTokenExpiresAtUtc;
        User = auth.User;
        Changed?.Invoke();
    }

    public void SetAccessToken(string token, DateTime expiresAtUtc)
    {
        AccessToken = token;
        AccessTokenExpiresAtUtc = expiresAtUtc;
    }

    public void SetUser(UserDto user)
    {
        User = user;
        Changed?.Invoke();
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = default;
        User = null;
        Changed?.Invoke();
    }
}
