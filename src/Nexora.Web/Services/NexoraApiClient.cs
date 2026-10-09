using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Web.Services;

public record ApiResult<T>(bool Ok, T? Value, string? Error, int StatusCode)
{
    public static ApiResult<T> Success(T value) => new(true, value, null, 200);
    public static ApiResult<T> Fail(string error, int status) => new(false, default, error, status);
}

/// <summary>
/// Typisierter Client für die Nexora-API. Der Zugriffstoken wird ausschließlich
/// serverseitig aus dem <see cref="TokenStore"/> bezogen.
/// </summary>
public class NexoraApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;
    private readonly TokenStore _tokens;
    private readonly IConfiguration _config;

    public NexoraApiClient(HttpClient http, TokenStore tokens, IConfiguration config)
    {
        _http = http;
        _tokens = tokens;
        _config = config;
    }

    // --- Authentifizierung ---
    public async Task<ApiResult<JsonElement>> SetupStatusAsync(CancellationToken ct = default)
        => await SendAsync<JsonElement>(HttpMethod.Get, "api/v1/admin/setup/status", null, ct);

    /// <summary>
    /// Legt den ersten Administrator an. Sofern ein Bootstrap-Geheimnis
    /// (NEXORA_BOOTSTRAP_SECRET) in der Web-Konfiguration gesetzt ist, wird es
    /// serverseitig als X-Bootstrap-Secret an die API übertragen – der Browser
    /// erhält das Geheimnis nie.
    /// </summary>
    public Task<ApiResult<JsonElement>> BootstrapAdminAsync(string email, string password, string? fullName, CancellationToken ct = default)
        => SendAsync<JsonElement>(HttpMethod.Post, "api/v1/admin/setup",
            new { email, password, fullName }, ct,
            bootstrapSecret: _config["NEXORA_BOOTSTRAP_SECRET"]);

    public async Task<ApiResult<AuthResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
        => await SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/login", new { email, password }, ct);

    public async Task<ApiResult<AuthResponse>> RegisterAsync(string email, string password, string? fullName, CancellationToken ct = default)
        => await SendAsync<AuthResponse>(HttpMethod.Post, "api/v1/auth/register", new { email, password, fullName }, ct);

    public async Task<ApiResult<UserDto>> MeAsync(CancellationToken ct = default)
        => await SendAsync<UserDto>(HttpMethod.Get, "api/v1/auth/me", null, ct);

    public async Task<ApiResult<object>> LogoutAsync(CancellationToken ct = default)
        => await SendAsync<object>(HttpMethod.Post, "api/v1/auth/logout", new { refreshToken = _tokens.RefreshToken }, ct);

    // --- Dashboard ---
    public Task<ApiResult<DashboardStatsDto>> DashboardAsync(CancellationToken ct = default)
        => SendAsync<DashboardStatsDto>(HttpMethod.Get, "api/v1/admin/dashboard/stats", null, ct);

    // --- Benutzer ---
    public Task<ApiResult<List<UserDto>>> UsersAsync(string? search = null, CancellationToken ct = default)
        => SendAsync<List<UserDto>>(HttpMethod.Get, $"api/v1/admin/users{Query(("search", search))}", null, ct);

    public Task<ApiResult<UserDetailDto>> UserDetailAsync(string id, CancellationToken ct = default)
        => SendAsync<UserDetailDto>(HttpMethod.Get, $"api/v1/admin/users/{id}", null, ct);

    public Task<ApiResult<UserDto>> UpdateUserAsync(string id, UpdateUserRequest req, CancellationToken ct = default)
        => SendAsync<UserDto>(HttpMethod.Put, $"api/v1/admin/users/{id}", req, ct);

    public Task<ApiResult<JsonElement>> SetUserRolesAsync(string id, List<string> roles, CancellationToken ct = default)
        => SendAsync<JsonElement>(HttpMethod.Post, $"api/v1/admin/users/{id}/roles", new SetUserRolesRequest { Roles = roles }, ct);

    public Task<ApiResult<object>> AssignLicenseAsync(string userId, Guid licenseId, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/users/{userId}/assign-license", new AssignLicenseRequest { UserId = userId, LicenseId = licenseId }, ct);

    public Task<ApiResult<object>> RemoveLicenseAsync(string userId, Guid licenseId, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/users/{userId}/remove-license/{licenseId}", null, ct);

    public Task<ApiResult<List<DeviceDto>>> UserDevicesAsync(string id, CancellationToken ct = default)
        => SendAsync<List<DeviceDto>>(HttpMethod.Get, $"api/v1/admin/users/{id}/devices", null, ct);

    public Task<ApiResult<object>> RevokeUserDeviceAsync(string id, Guid deviceId, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/users/{id}/devices/{deviceId}/revoke", null, ct);

    // --- Apps ---
    public Task<ApiResult<List<AppDto>>> AppsAsync(CancellationToken ct = default)
        => SendAsync<List<AppDto>>(HttpMethod.Get, "api/v1/admin/apps", null, ct);

    public Task<ApiResult<AppDto>> CreateAppAsync(CreateAppRequest req, CancellationToken ct = default)
        => SendAsync<AppDto>(HttpMethod.Post, "api/v1/admin/apps", req, ct);

    public Task<ApiResult<AppDto>> UpdateAppAsync(Guid id, UpdateAppRequest req, CancellationToken ct = default)
        => SendAsync<AppDto>(HttpMethod.Put, $"api/v1/admin/apps/{id}", req, ct);

    public Task<ApiResult<AppDto>> PublishAppAsync(Guid id, CancellationToken ct = default)
        => SendAsync<AppDto>(HttpMethod.Post, $"api/v1/admin/apps/{id}/publish", null, ct);

    public Task<ApiResult<AppDto>> ArchiveAppAsync(Guid id, CancellationToken ct = default)
        => SendAsync<AppDto>(HttpMethod.Post, $"api/v1/admin/apps/{id}/archive", null, ct);

    // --- Modelle ---
    public Task<ApiResult<List<AiModelDto>>> ModelsAsync(CancellationToken ct = default)
        => SendAsync<List<AiModelDto>>(HttpMethod.Get, "api/v1/admin/models", null, ct);

    public Task<ApiResult<AiModelDto>> CreateModelAsync(CreateModelRequest req, CancellationToken ct = default)
        => SendAsync<AiModelDto>(HttpMethod.Post, "api/v1/admin/models", req, ct);

    public Task<ApiResult<AiModelDto>> UpdateModelAsync(Guid id, UpdateModelRequest req, CancellationToken ct = default)
        => SendAsync<AiModelDto>(HttpMethod.Put, $"api/v1/admin/models/{id}", req, ct);

    public Task<ApiResult<object>> DeleteModelAsync(Guid id, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Delete, $"api/v1/admin/models/{id}", null, ct);

    public Task<ApiResult<JsonElement>> SyncModelsAsync(CancellationToken ct = default)
        => SendAsync<JsonElement>(HttpMethod.Post, "api/v1/admin/models/sync", null, ct);

    // --- Lizenzen ---
    public Task<ApiResult<List<AdminLicenseDto>>> LicensesAsync(Guid? appId = null, LicenseStatus? status = null, string? search = null, CancellationToken ct = default)
        => SendAsync<List<AdminLicenseDto>>(HttpMethod.Get,
            $"api/v1/admin/licenses{Query(("appId", appId), ("status", status), ("search", search))}", null, ct);

    public Task<ApiResult<LicenseDetailDto>> LicenseDetailAsync(Guid id, CancellationToken ct = default)
        => SendAsync<LicenseDetailDto>(HttpMethod.Get, $"api/v1/admin/licenses/{id}", null, ct);

    public Task<ApiResult<AdminLicenseDto>> CreateLicenseAsync(CreateLicenseRequest req, CancellationToken ct = default)
        => SendAsync<AdminLicenseDto>(HttpMethod.Post, "api/v1/admin/licenses", req, ct);

    public Task<ApiResult<AdminLicenseDto>> UpdateLicenseAsync(Guid id, UpdateLicenseRequest req, CancellationToken ct = default)
        => SendAsync<AdminLicenseDto>(HttpMethod.Put, $"api/v1/admin/licenses/{id}", req, ct);

    public Task<ApiResult<object>> SuspendLicenseAsync(Guid id, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/licenses/{id}/suspend", null, ct);

    public Task<ApiResult<object>> ActivateLicenseAsync(Guid id, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/licenses/{id}/activate", null, ct);

    public Task<ApiResult<object>> RevokeLicenseAsync(Guid id, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/licenses/{id}/revoke", null, ct);

    public Task<ApiResult<JsonElement>> ExtendLicenseAsync(Guid id, int days, CancellationToken ct = default)
        => SendAsync<JsonElement>(HttpMethod.Post, $"api/v1/admin/licenses/{id}/extend", new { days }, ct);

    public Task<ApiResult<object>> AssignUserToLicenseAsync(Guid licenseId, string userId, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/licenses/{licenseId}/assign", new { userId }, ct);

    public Task<ApiResult<object>> UnassignUserFromLicenseAsync(Guid licenseId, string userId, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/licenses/{licenseId}/unassign", new { userId }, ct);

    public Task<ApiResult<List<LicenseKeyDto>>> LicenseKeysOfLicenseAsync(Guid licenseId, CancellationToken ct = default)
        => SendAsync<List<LicenseKeyDto>>(HttpMethod.Get, $"api/v1/admin/licenses/{licenseId}/keys", null, ct);

    // --- Lizenzschlüssel ---
    public Task<ApiResult<List<LicenseKeyDto>>> LicenseKeysAsync(Guid? licenseId = null, LicenseKeyStatus? status = null, CancellationToken ct = default)
        => SendAsync<List<LicenseKeyDto>>(HttpMethod.Get,
            $"api/v1/admin/license-keys{Query(("licenseId", licenseId), ("status", status))}", null, ct);

    public Task<ApiResult<List<CreatedLicenseKeyDto>>> CreateLicenseKeysAsync(CreateLicenseKeyRequest req, CancellationToken ct = default)
        => SendAsync<List<CreatedLicenseKeyDto>>(HttpMethod.Post, "api/v1/admin/license-keys", req, ct);

    public Task<ApiResult<object>> RevokeLicenseKeyAsync(Guid id, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/license-keys/{id}/revoke", null, ct);

    // --- Nutzung ---
    public Task<ApiResult<UsageSummaryDto>> UsageSummaryAsync(DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
        => SendAsync<UsageSummaryDto>(HttpMethod.Get, $"api/v1/admin/usage/summary{Query(("from", from?.ToString("o")), ("to", to?.ToString("o")))}", null, ct);

    public Task<ApiResult<List<AdminUsageEventDto>>> UsageEventsAsync(int take = 200, CancellationToken ct = default)
        => SendAsync<List<AdminUsageEventDto>>(HttpMethod.Get, $"api/v1/admin/usage/events?take={take}", null, ct);

    // --- Geräte (Admin) ---
    public Task<ApiResult<List<AdminDeviceDto>>> DevicesAsync(string? search = null, CancellationToken ct = default)
        => SendAsync<List<AdminDeviceDto>>(HttpMethod.Get, $"api/v1/admin/devices{Query(("search", search))}", null, ct);

    public Task<ApiResult<object>> RevokeDeviceAsync(Guid id, CancellationToken ct = default)
        => SendAsync<object>(HttpMethod.Post, $"api/v1/admin/devices/{id}/revoke", null, ct);

    // --- Kerne ---
    private Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct, string? bootstrapSecret = null)
        => SendCoreAsync<T>(method, url, body, ct, retried: false, bootstrapSecret);

    private async Task<ApiResult<T>> SendCoreAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct, bool retried, string? bootstrapSecret = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null)
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        if (!string.IsNullOrEmpty(_tokens.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _tokens.AccessToken);
        if (!string.IsNullOrWhiteSpace(bootstrapSecret))
            request.Headers.TryAddWithoutValidation("X-Bootstrap-Secret", bootstrapSecret);

        try
        {
            using var response = await _http.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized && !retried && _tokens.RefreshToken != null)
            {
                var refreshed = await TryRefreshAsync(ct);
                if (refreshed)
                    return await SendCoreAsync<T>(method, url, body, ct, retried: true, bootstrapSecret);
            }

            if (response.IsSuccessStatusCode)
            {
                if (typeof(T) == typeof(object))
                    return ApiResult<T>.Success((T)(object)new object());
                var value = await response.Content.ReadFromJsonAsync<T>(Json, ct);
                return ApiResult<T>.Success(value!);
            }

            var message = await ExtractErrorAsync(response, ct);
            return ApiResult<T>.Fail(message, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            return ApiResult<T>.Fail($"Verbindung zur API fehlgeschlagen: {ex.Message}", 0);
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Fail("Zeitüberschreitung bei der API-Anfrage.", 0);
        }
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/v1/auth/refresh");
            request.Content = new StringContent(
                JsonSerializer.Serialize(new RefreshRequest { RefreshToken = _tokens.RefreshToken ?? "" }, Json),
                Encoding.UTF8, "application/json");
            using var response = await _http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;
            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, ct);
            if (auth == null || string.IsNullOrEmpty(auth.AccessToken)) return false;
            _tokens.Set(auth);
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

    private static string Query(params (string Key, object? Value)[] pairs)
    {
        var parts = pairs
            .Where(p => p.Value != null && !string.IsNullOrWhiteSpace(p.Value.ToString()))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!.ToString()!)}")
            .ToList();
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }
}
