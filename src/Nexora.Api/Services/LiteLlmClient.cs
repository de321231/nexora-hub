using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nexora.Api.Options;

namespace Nexora.Api.Services;

public record LiteLlmMessage(string Role, string Content);

public record LiteLlmResult(
    string Content,
    string Model,
    int InputTokens,
    int OutputTokens,
    bool TokensEstimated,
    decimal? CostUsd);

public record LiteLlmModelInfo(string ModelId, string DisplayName, string Provider, int? MaxContextTokens);

public class LiteLlmNotConfiguredException : Exception
{
    public LiteLlmNotConfiguredException() : base("LiteLLM ist nicht konfiguriert.") { }
}

public class LiteLlmException : Exception
{
    public LiteLlmException(string message) : base(message) { }
}

public interface ILiteLlmClient
{
    bool IsConfigured { get; }
    Task<LiteLlmResult> ChatAsync(string modelId, IReadOnlyList<LiteLlmMessage> messages, int? maxTokens, CancellationToken ct);
    Task<IReadOnlyList<LiteLlmModelInfo>> ListModelsAsync(CancellationToken ct);
}

public class LiteLlmClient : ILiteLlmClient
{
    private readonly IHttpClientFactory _factory;
    private readonly LiteLlmOptions _options;
    private readonly ILogger<LiteLlmClient> _logger;

    public LiteLlmClient(IHttpClientFactory factory, IOptions<LiteLlmOptions> options, ILogger<LiteLlmClient> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.BaseUrl);

    private HttpClient CreateClient()
    {
        var client = _factory.CreateClient("litellm");
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        return client;
    }

    public async Task<LiteLlmResult> ChatAsync(string modelId, IReadOnlyList<LiteLlmMessage> messages, int? maxTokens, CancellationToken ct)
    {
        if (!IsConfigured) throw new LiteLlmNotConfiguredException();

        var payload = new Dictionary<string, object?>
        {
            ["model"] = modelId,
            ["messages"] = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray()
        };
        if (maxTokens is > 0) payload["max_tokens"] = maxTokens;

        using var client = CreateClient();
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync("v1/chat/completions", payload, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new LiteLlmException($"Verbindung zum KI-Dienst fehlgeschlagen: {ex.Message}");
        }

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("LiteLLM Fehler {Status}: {Body}", (int)response.StatusCode, Truncate(body, 500));
            throw new LiteLlmException($"KI-Dienst antwortete mit Fehler {(int)response.StatusCode}.");
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            string content = "";
            string model = modelId;
            int inputTokens = 0, outputTokens = 0;
            bool estimated = false;
            decimal? cost = null;

            if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var first = choices[0];
                if (first.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var c))
                    content = c.GetString() ?? "";
            }
            if (root.TryGetProperty("model", out var mEl) && mEl.ValueKind == JsonValueKind.String)
                model = mEl.GetString() ?? modelId;

            if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt) && pt.TryGetInt32(out var pti)) inputTokens = pti;
                if (usage.TryGetProperty("completion_tokens", out var ctok) && ctok.TryGetInt32(out var cti)) outputTokens = cti;
                if (usage.TryGetProperty("cost", out var costEl) && costEl.ValueKind == JsonValueKind.Number)
                    cost = costEl.GetDecimal();
            }
            if (root.TryGetProperty("response_cost", out var rc) && rc.ValueKind == JsonValueKind.Number)
                cost = rc.GetDecimal();

            if (inputTokens <= 0 && outputTokens <= 0)
            {
                estimated = true;
                inputTokens = QuotaService.EstimateTokens(string.Concat(messages.Select(m => m.Content)));
                outputTokens = QuotaService.EstimateTokens(content);
            }

            return new LiteLlmResult(content, model, inputTokens, outputTokens, estimated, cost);
        }
        catch (JsonException)
        {
            throw new LiteLlmException("Antwort des KI-Dienstes konnte nicht gelesen werden.");
        }
    }

    public async Task<IReadOnlyList<LiteLlmModelInfo>> ListModelsAsync(CancellationToken ct)
    {
        if (!IsConfigured) throw new LiteLlmNotConfiguredException();
        using var client = CreateClient();
        var body = await client.GetStringAsync("v1/models", ct);
        var list = new List<LiteLlmModelInfo>();
        using var doc = JsonDocument.Parse(body);
        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(id)) continue;
                var provider = "";
                if (item.TryGetProperty("owned_by", out var owned) && owned.ValueKind == JsonValueKind.String)
                    provider = owned.GetString() ?? "";
                if (string.IsNullOrEmpty(provider) && id.Contains('/'))
                    provider = id.Split('/')[0];
                int? maxCtx = null;
                if (item.TryGetProperty("max_input_tokens", out var mi) && mi.ValueKind == JsonValueKind.Number)
                    maxCtx = mi.GetInt32();
                list.Add(new LiteLlmModelInfo(id!, id!, provider, maxCtx));
            }
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
