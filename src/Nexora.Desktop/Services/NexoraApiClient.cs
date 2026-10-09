using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Desktop.Services;

public record ApiResult<T>(bool Ok, T? Value, string? Error, int StatusCode)
{
    public static ApiResult<T> Success(T value) => new(true, value, null, 200);
    public static ApiResult<T> Fail(string error, int status) => new(false, default, error, status);
}

/// <summary>
/// Typisierter Client für die Nexora-API (Desktop-Chat-App).
/// Azure: Die App spricht ausschließlich mit der Nexora-API; der LiteLLM-Schlüssel
/// liegt nur serverseitig und wird niemals an Clients übertragen.
/// </summary>
public class NexoraApiClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly SessionStore _session;

    public NexoraApiClient(SessionStore session)
    {
        _session = session;
        _http = new HttpClient
        {
            BaseAddress = new Uri(EnsureTrailingSlash(session.BaseUrl)),
            Timeout = TimeSpan.FromSeconds(120)
        };
    }

    public void ApplyBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        _http.BaseAddress = new Uri(EnsureTrailingSlash(url));
    }

    private static string EnsureTrailingSlash(string url) => url.TrimEnd('/') + "/";

    // --- Authentifizierung ----------------------------------------------------

    public Task<ApiResult<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/login",
            new LoginRequest { Email = email, Password = password, DeviceIdentifier = _session.DeviceIdentifier, DeviceName = _session.DeviceName }, ct);

    public Task<ApiResult<AuthResponse>> RegisterAsync(string email, string password, string? fullName, CancellationToken ct = default) =>
        SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/register",
            new RegisterRequest { Email = email, Password = password, FullName = fullName }, ct);

    public Task<ApiResult<UserDto>> MeAsync(CancellationToken ct = default) =>
        SendAsync<UserDto>(HttpMethod.Get, "api/v1/auth/me", null, ct);

    public Task<ApiResult<object>> LogoutAsync(CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Post, "api/v1/auth/logout",
            new RefreshRequest { RefreshToken = _session.RefreshToken ?? "", DeviceIdentifier = _session.DeviceIdentifier }, ct);

    // --- Lizenzen (Benutzersicht) ---------------------------------------------

    public Task<ApiResult<List<LicenseDto>>> MyLicensesAsync(CancellationToken ct = default) =>
        SendAsync<List<LicenseDto>>(HttpMethod.Get, "api/v1/licenses/mine", null, ct);

    public Task<ApiResult<object>> RedeemKeyAsync(string key, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Post, "api/v1/licenses/redeem",
            new RedeemKeyRequest { Key = key, DeviceIdentifier = _session.DeviceIdentifier, DeviceName = _session.DeviceName }, ct);

    public Task<ApiResult<QuotaStatusDto>> QuotaAsync(Guid licenseId, CancellationToken ct = default) =>
        SendAsync<QuotaStatusDto>(HttpMethod.Get, $"api/v1/licenses/{licenseId}/quota", null, ct);

    public Task<ApiResult<List<UsageEventDto>>> UsageAsync(Guid licenseId, int take = 50, CancellationToken ct = default) =>
        SendAsync<List<UsageEventDto>>(HttpMethod.Get, $"api/v1/licenses/{licenseId}/usage?take={take}", null, ct);

    // --- Geräte ---------------------------------------------------------------

    public Task<ApiResult<List<DeviceDto>>> DevicesAsync(CancellationToken ct = default) =>
        SendAsync<List<DeviceDto>>(HttpMethod.Get, "api/v1/devices", null, ct);

    public Task<ApiResult<DeviceDto>> ActivateDeviceAsync(Guid? licenseId, CancellationToken ct = default) =>
        SendAsync<DeviceDto>(HttpMethod.Post, "api/v1/devices/activate",
            new ActivateDeviceRequest
            {
                DeviceIdentifier = _session.DeviceIdentifier,
                DeviceName = _session.DeviceName,
                Platform = "Windows",
                LicenseId = licenseId
            }, ct);

    public Task<ApiResult<object>> RevokeDeviceAsync(Guid deviceId, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Post, $"api/v1/devices/{deviceId}/revoke", null, ct);

    // --- Chat -----------------------------------------------------------------

    public Task<ApiResult<List<ConversationDto>>> ConversationsAsync(CancellationToken ct = default) =>
        SendAsync<List<ConversationDto>>(HttpMethod.Get, "api/v1/chat/conversations", null, ct);

    public Task<ApiResult<List<ChatMessageDto>>> ConversationMessagesAsync(Guid conversationId, CancellationToken ct = default) =>
        SendAsync<List<ChatMessageDto>>(HttpMethod.Get, $"api/v1/chat/conversations/{conversationId}/messages", null, ct);

    public Task<ApiResult<object>> DeleteConversationAsync(Guid conversationId, CancellationToken ct = default) =>
        SendAsync<object>(HttpMethod.Delete, $"api/v1/chat/conversations/{conversationId}", null, ct);

    public Task<ApiResult<ChatCompletionResponse>> SendMessageAsync(SendMessageRequest req, CancellationToken ct = default) =>
        SendAsync<ChatCompletionResponse>(HttpMethod.Post, "api/v1/chat/send", req, ct);

    // --- Kernel ---------------------------------------------------------------

    public async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct) =>
        await SendCoreAsync<T>(method, url, body, ct, retried: false, isRefresh: false);

    private async Task<ApiResult<T>> SendCoreAsync<T>(
        HttpMethod method, string url, object? body, CancellationToken ct, bool retried, bool isRefresh)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");

        if (!isRefresh && !string.IsNullOrEmpty(_session.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.AccessToken);

        try
        {
            using var response = await _http.SendAsync(request, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized && !retried && !isRefresh && _session.RefreshToken != null)
            {
                if (await TryRefreshAsync(ct))
                    return await SendCoreAsync<T>(method, url, body, ct, retried: true, isRefresh: false);
            }

            if (response.IsSuccessStatusCode)
            {
                if (typeof(T) == typeof(object))
                    return ApiResult<T>.Success((T)(object)new object());
                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                if (value == null)
                    return ApiResult<T>.Fail("Leere Antwort von der API.", (int)response.StatusCode);
                return ApiResult<T>.Success(value);
            }

            return ApiResult<T>.Fail(await ExtractErrorAsync(response, ct), (int)response.StatusCode);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return ApiResult<T>.Fail("Zeitüberschreitung bei der API-Anfrage (120 s).", 0);
        }
        catch (OperationCanceledException)
        {
            return ApiResult<T>.Fail("Anfrage abgebrochen.", 0);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Fail($"Verbindung zur API fehlgeschlagen: {ex.Message}", 0);
        }
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        try
        {
            var refreshToken = _session.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken)) return false;

            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/refresh");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new RefreshRequest
                {
                    RefreshToken = refreshToken,
                    DeviceIdentifier = _session.DeviceIdentifier
                }, Json),
                Encoding.UTF8, "application/json");

            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, ct);
            if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return false;
            _session.UpdateTokens(auth.AccessToken, auth.RefreshToken, auth.AccessTokenExpiresAtUtc);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static async Task<string> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(raw))
            {
                using var doc = JsonDocument.Parse(raw);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                        return error.GetString() ?? raw;
                    if (doc.RootElement.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
                        return title.GetString() ?? raw;
                }
                return raw;
            }
        }
        catch (JsonException) { /* Rohantwort unbrauchbar */ }
        return $"API-Fehler {(int)response.StatusCode} ({response.ReasonPhrase}).";
    }

    public void Dispose() => _http.Dispose();
}