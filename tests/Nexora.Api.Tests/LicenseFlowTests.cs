using System.Net;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Tests;

/// <summary>
/// Test 4–6: Beide Lizenzpfade (Schlüsseleinlösung, direkte Zuweisung) sowie
/// die Ablehnung abgelaufener/gesperrter Lizenzen.
/// </summary>
[Collection("Api")]
public class LicenseFlowTests
{
    private readonly ApiFactoryFixture _fx;
    public LicenseFlowTests(ApiFactoryFixture fx) => _fx = fx;

    // Test 4: Ein Schlüssel kann genau einmal eingelöst werden.
    [Fact]
    public async Task Key_RedeemedExactlyOnce()
    {
        var admin = await Api.AdminLoginAsync(_fx.Client);
        var app = await TestData.NexoraAiAppAsync(_fx.Client, admin.AccessToken);
        var model = await TestData.DefaultModelAsync(_fx.Client, admin.AccessToken);
        var license = await TestData.CreateLicenseAsync(_fx.Client, admin.AccessToken, app.Id, model.Id, maxDevices: 2);
        var key = await TestData.CreateKeyAsync(_fx.Client, admin.AccessToken, license.Id, maxUses: 1);

        var user1 = await TestData.RegisterUserAsync(_fx.Client, "redeem1");
        var (status1, _) = await TestData.RedeemAsync(_fx.Client, user1.AccessToken, key.Key);
        Assert.Equal(HttpStatusCode.OK, status1);

        // Zweite Einlösung durch denselben Benutzer -> abgelehnt.
        var (status2, error2) = await TestData.RedeemAsync(_fx.Client, user1.AccessToken, key.Key);
        Assert.Equal(HttpStatusCode.BadRequest, status2);
        Assert.Contains("bereits", error2);

        // Einlösung durch einen anderen Benutzer -> ebenfalls abgelehnt.
        var user2 = await TestData.RegisterUserAsync(_fx.Client, "redeem2");
        var (status3, _) = await TestData.RedeemAsync(_fx.Client, user2.AccessToken, key.Key);
        Assert.Equal(HttpStatusCode.BadRequest, status3);

        // Benutzer 1 sieht die Lizenz, Benutzer 2 nicht.
        var mine1 = await Api.GetJsonAsync<List<LicenseDto>>(_fx.Client, "api/v1/licenses/mine", user1.AccessToken);
        Assert.Contains(mine1, l => l.AppSlug == "nexora-ai");
        var mine2 = await Api.GetJsonAsync<List<LicenseDto>>(_fx.Client, "api/v1/licenses/mine", user2.AccessToken);
        Assert.DoesNotContain(mine2, l => l.AppSlug == "nexora-ai");
    }

    // Test 5: Direkte Admin-Zuweisung erscheint in der Benutzersicht.
    [Fact]
    public async Task License_DirectAssignment_VisibleToUser()
    {
        var admin = await Api.AdminLoginAsync(_fx.Client);
        var app = await TestData.NexoraAiAppAsync(_fx.Client, admin.AccessToken);
        var model = await TestData.DefaultModelAsync(_fx.Client, admin.AccessToken);
        var license = await TestData.CreateLicenseAsync(_fx.Client, admin.AccessToken, app.Id, model.Id);

        var user = await TestData.RegisterUserAsync(_fx.Client, "assign");
        await TestData.AssignAsync(_fx.Client, admin.AccessToken, license.Id, user.User.Id);

        var mine = await Api.GetJsonAsync<List<LicenseDto>>(_fx.Client, "api/v1/licenses/mine", user.AccessToken);
        var assigned = Assert.Single(mine);
        Assert.Equal(license.Id, assigned.Id);
        Assert.Equal(LicenseStatus.Active, assigned.Status);
        Assert.Contains(assigned.Models, m => m.ModelId == TestData.DefaultModelId);
    }

    // Test 6: Abgelaufene und gesperrte Lizenzen werden serverseitig abgewiesen.
    [Fact]
    public async Task License_ExpiredOrSuspended_ChatRejected()
    {
        var admin = await Api.AdminLoginAsync(_fx.Client);
        var app = await TestData.NexoraAiAppAsync(_fx.Client, admin.AccessToken);
        var model = await TestData.DefaultModelAsync(_fx.Client, admin.AccessToken);

        // Abgelaufene Lizenz.
        var expired = await TestData.CreateLicenseAsync(_fx.Client, admin.AccessToken, app.Id, model.Id,
            expiresAt: DateTime.UtcNow.AddMinutes(-5));
        var userExpired = await TestData.RegisterUserAsync(_fx.Client, "expired");
        await TestData.AssignAsync(_fx.Client, admin.AccessToken, expired.Id, userExpired.User.Id);
        using (var resp = await TestData.ChatSendAsync(_fx.Client, userExpired.AccessToken, TestData.DefaultModelId, "Hallo"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
            Assert.Contains("Keine aktive Lizenz", await Api.ReadErrorAsync(resp));
        }

        // Gesperrte Lizenz.
        var suspended = await TestData.CreateLicenseAsync(_fx.Client, admin.AccessToken, app.Id, model.Id);
        var userSuspended = await TestData.RegisterUserAsync(_fx.Client, "suspended");
        await TestData.AssignAsync(_fx.Client, admin.AccessToken, suspended.Id, userSuspended.User.Id);
        await TestData.SuspendAsync(_fx.Client, admin.AccessToken, suspended.Id);
        using (var resp = await TestData.ChatSendAsync(_fx.Client, userSuspended.AccessToken, TestData.DefaultModelId, "Hallo"))
            Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
