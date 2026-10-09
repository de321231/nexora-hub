using System.Net;
using System.Net.Http.Json;
using Nexora.Shared.Dtos;

namespace Nexora.Api.Tests;

/// <summary>
/// Zentrale Testdaten-Fabrik: erzeugt Benutzer, Lizenzen, Schlüssel und
/// Zuweisungen über die echte API (keine direkten DB-Manipulationen, außer
/// für explizite Persistenz-Assertions).
/// </summary>
public static class TestData
{
    /// <summary>Vom Seeder automatisch angelegtes Standardmodell.</summary>
    public const string DefaultModelId = "openai/gpt-4o-mini";

    public static string UniqueEmail(string prefix = "user")
        => $"{prefix}-{Guid.NewGuid():N}@nexora.test";

    public static async Task<AuthResponse> RegisterUserAsync(HttpClient client, string prefix = "user")
        => await Api.RegisterAsync(client, UniqueEmail(prefix), "User!Pass123");

    public static async Task<AppDto> NexoraAiAppAsync(HttpClient client, string adminToken)
    {
        var apps = await Api.GetJsonAsync<List<AppDto>>(client, "api/v1/admin/apps", adminToken);
        return apps.Single(a => a.Slug == "nexora-ai");
    }

    public static async Task<AiModelDto> DefaultModelAsync(HttpClient client, string adminToken)
    {
        var models = await Api.GetJsonAsync<List<AiModelDto>>(client, "api/v1/admin/models", adminToken);
        return models.Single(m => m.ModelId == DefaultModelId);
    }

    public static async Task<AdminLicenseDto> CreateLicenseAsync(
        HttpClient client,
        string adminToken,
        Guid appId,
        Guid modelId,
        string? name = null,
        DateTime? startsAt = null,
        DateTime? expiresAt = null,
        int maxDevices = 1,
        int? maxMessagesPerDay = null,
        int? maxMessagesPerMonth = null,
        long? maxInputTokensPerDay = null,
        long? maxOutputTokensPerDay = null,
        long? maxTotalTokensPerDay = null,
        long? maxTokensPerRequest = null)
    {
        var request = new CreateLicenseRequest
        {
            AppId = appId,
            Name = name ?? "Testlizenz " + Guid.NewGuid().ToString("N")[..6],
            StartsAt = startsAt,
            ExpiresAt = expiresAt,
            MaxDevices = maxDevices,
            MaxMessagesPerDay = maxMessagesPerDay,
            MaxMessagesPerMonth = maxMessagesPerMonth,
            MaxInputTokensPerDay = maxInputTokensPerDay,
            MaxOutputTokensPerDay = maxOutputTokensPerDay,
            MaxTotalTokensPerDay = maxTotalTokensPerDay,
            MaxTokensPerRequest = maxTokensPerRequest,
            ModelIds = new List<Guid> { modelId }
        };
        return await Api.PostJsonAsync<AdminLicenseDto>(client, "api/v1/admin/licenses", adminToken, request);
    }

    public static async Task<CreatedLicenseKeyDto> CreateKeyAsync(
        HttpClient client, string adminToken, Guid licenseId, int count = 1, int maxUses = 1)
    {
        var request = new CreateLicenseKeyRequest { LicenseId = licenseId, Count = count, MaxUses = maxUses };
        var created = await Api.PostJsonAsync<List<CreatedLicenseKeyDto>>(
            client, "api/v1/admin/license-keys", adminToken, request);
        return created.First();
    }

    public static async Task AssignAsync(HttpClient client, string adminToken, Guid licenseId, string userId)
    {
        using var resp = await Api.PostAsync(client, $"api/v1/admin/licenses/{licenseId}/assign",
            adminToken, new { userId });
        resp.EnsureSuccessStatusCode();
    }

    public static async Task SuspendAsync(HttpClient client, string adminToken, Guid licenseId)
    {
        using var resp = await Api.PostAsync(client, $"api/v1/admin/licenses/{licenseId}/suspend", adminToken);
        resp.EnsureSuccessStatusCode();
    }

    /// <summary>Löst einen Schlüssel ein und liefert Status plus Fehlermeldung.</summary>
    public static async Task<(HttpStatusCode Status, string? Error)> RedeemAsync(
        HttpClient client, string userToken, string key)
    {
        using var resp = await Api.PostAsync(client, "api/v1/licenses/redeem", userToken, new { key });
        var error = resp.IsSuccessStatusCode ? null : await Api.ReadErrorAsync(resp);
        return (resp.StatusCode, error);
    }

    /// <summary>Sendet eine Chat-Nachricht (Rohantwort für Status-/Body-Assertions).</summary>
    public static Task<HttpResponseMessage> ChatSendAsync(
        HttpClient client, string userToken, string modelId, string message,
        Guid? conversationId = null, int? maxOutputTokens = null)
        => Api.PostAsync(client, "api/v1/chat/send", userToken, new SendMessageRequest
        {
            ModelId = modelId,
            Message = message,
            ConversationId = conversationId,
            MaxOutputTokens = maxOutputTokens
        });
}
