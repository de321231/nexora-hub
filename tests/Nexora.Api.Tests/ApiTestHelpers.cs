using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nexora.Shared.Dtos;

namespace Nexora.Api.Tests;

/// <summary>JSON-Serialisierungsoptionen passend zur API (camelCase + Enum als Strings).</summary>
public static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Erstellt JSON-Content für Request-Bodies.</summary>
    public static StringContent Content(object body) =>
        new(JsonSerializer.Serialize(body, Options), Encoding.UTF8, "application/json");
}

public static class Api
{
    /// <summary>Registriert einen Benutzer (liefert direkt Tokens; keine Adminrechte).</summary>
    public static async Task<AuthResponse> RegisterAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsync("api/v1/auth/register",
            Json.Content(new { email, password, fullName = "Test User " + Random.Shared.Next(1000) }));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
    }

    /// <summary>Meldet einen Benutzer an.</summary>
    public static async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password)
    {
        using var response = await client.PostAsync("api/v1/auth/login",
            Json.Content(new { email, password, deviceIdentifier = "test-dev", deviceName = "CI Test" }));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json.Options))!;
    }

    public static async Task<AuthResponse> AdminLoginAsync(HttpClient client)
        => await LoginAsync(client, ApiFactoryFixture.AdminEmail, ApiFactoryFixture.AdminPassword);

    public static HttpRequestMessage Begin(HttpMethod method, string url, string? token, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body != null) request.Content = Json.Content(body);
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public static Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string? token, object? body = null)
        => client.SendAsync(Begin(method, url, token, body));

    public static Task<HttpResponseMessage> GetAsync(HttpClient client, string url, string? token)
        => client.SendAsync(Begin(HttpMethod.Get, url, token));

    public static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, string? token, object? body = null)
        => client.SendAsync(Begin(HttpMethod.Post, url, token, body));

    /// <summary>GET mit Bearer-Token und Deserialisierung in den Zieltyp.</summary>
    public static async Task<T> GetJsonAsync<T>(HttpClient client, string url, string? token)
    {
        using var resp = await GetAsync(client, url, token);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    /// <summary>POST mit Bearer-Token und Deserialisierung in den Zieltyp.</summary>
    public static async Task<T> PostJsonAsync<T>(HttpClient client, string url, string? token, object? body = null)
    {
        using var resp = await PostAsync(client, url, token, body);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<T>(Json.Options))!;
    }

    /// <summary>Liest den Antwortinhalt als Zieltyp (ohne Statusprüfung).</summary>
    public static async Task<T?> ReadAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>(Json.Options);

    /// <summary>Liest die Fehlermeldung aus einem API-Fehler ({ error }).</summary>
    public static async Task<string?> ReadErrorAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
                return error.GetString();
        }
        catch (JsonException) { }
        return raw;
    }
}