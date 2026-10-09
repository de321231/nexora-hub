using System.Net;

namespace Nexora.Api.Tests;

/// <summary>
/// Test 7–10: Serverseitige Modellberechtigung und Kontingentgrenzen
/// (Nachrichten/Tag, Tokens pro Anfrage, parallele Anfragen).
/// </summary>
[Collection("Api")]
public class QuotaTests
{
    private readonly ApiFactoryFixture _fx;
    public QuotaTests(ApiFactoryFixture fx) => _fx = fx;

    private async Task<(string Token, Guid LicenseId)> SetupUserWithLicenseAsync(
        int? maxMessagesPerDay = null, long? maxTokensPerRequest = null)
    {
        var admin = await Api.AdminLoginAsync(_fx.Client);
        var app = await TestData.NexoraAiAppAsync(_fx.Client, admin.AccessToken);
        var model = await TestData.DefaultModelAsync(_fx.Client, admin.AccessToken);
        var license = await TestData.CreateLicenseAsync(_fx.Client, admin.AccessToken, app.Id, model.Id,
            maxMessagesPerDay: maxMessagesPerDay, maxTokensPerRequest: maxTokensPerRequest);
        var user = await TestData.RegisterUserAsync(_fx.Client, "quota");
        await TestData.AssignAsync(_fx.Client, admin.AccessToken, license.Id, user.User.Id);
        return (user.AccessToken, license.Id);
    }

    // Test 7: Nur freigeschaltete Modelle sind nutzbar.
    [Fact]
    public async Task Model_RestrictionEnforced()
    {
        var (token, _) = await SetupUserWithLicenseAsync();

        using (var forbidden = await TestData.ChatSendAsync(_fx.Client, token, "openai/gpt-4o", "Hallo"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
            Assert.Contains("nicht freigeschaltet", await Api.ReadErrorAsync(forbidden));
        }

        using (var allowed = await TestData.ChatSendAsync(_fx.Client, token, TestData.DefaultModelId, "Hallo"))
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    // Test 8: Nachrichtenlimit pro Tag wird durchgesetzt.
    [Fact]
    public async Task Quota_MessageLimitPerDay()
    {
        var (token, _) = await SetupUserWithLicenseAsync(maxMessagesPerDay: 2);

        using (var r1 = await TestData.ChatSendAsync(_fx.Client, token, TestData.DefaultModelId, "Nachricht 1"))
            Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        using (var r2 = await TestData.ChatSendAsync(_fx.Client, token, TestData.DefaultModelId, "Nachricht 2"))
            Assert.Equal(HttpStatusCode.OK, r2.StatusCode);

        using (var r3 = await TestData.ChatSendAsync(_fx.Client, token, TestData.DefaultModelId, "Nachricht 3"))
        {
            Assert.Equal(HttpStatusCode.TooManyRequests, r3.StatusCode);
            Assert.Contains("Nachrichten", await Api.ReadErrorAsync(r3));
        }
    }

    // Test 9: Tokenlimit pro Anfrage wird durchgesetzt.
    [Fact]
    public async Task Quota_TokenLimitPerRequest()
    {
        var (token, _) = await SetupUserWithLicenseAsync(maxTokensPerRequest: 20);
        var longMessage = new string('x', 400); // ~100 geschätzte Eingabe-Tokens + 20 Ausgabe > 20

        using var resp = await TestData.ChatSendAsync(_fx.Client, token, TestData.DefaultModelId, longMessage);
        Assert.Equal(HttpStatusCode.TooManyRequests, resp.StatusCode);
        Assert.Contains("Tokenlimit pro Anfrage", await Api.ReadErrorAsync(resp));
    }

    // Test 10: Parallele Anfragen können das Limit nicht umgehen.
    [Fact]
    public async Task Quota_ParallelRequests_NoBypass()
    {
        var (token, _) = await SetupUserWithLicenseAsync(maxMessagesPerDay: 3);

        const int parallel = 6;
        var tasks = Enumerable.Range(0, parallel)
            .Select(i => TestData.ChatSendAsync(_fx.Client, token, TestData.DefaultModelId, $"Parallel {i}"))
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var codes = responses.Select(r => (int)r.StatusCode).ToList();
        foreach (var r in responses) r.Dispose();

        Assert.Equal(3, codes.Count(c => c == 200));
        Assert.Equal(3, codes.Count(c => c == 429));
        Assert.DoesNotContain(codes, c => c >= 500);
    }
}
