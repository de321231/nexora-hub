using System.Net;
using System.Text.Json;
using Nexora.Shared.Dtos;

namespace Nexora.Api.Tests;

/// <summary>
/// Test 1–3, 13, 16: Registrierung/Login, Auth-Guards, Admin-Schutz,
/// nativer App-Login (Gerätefluss) und einmaliger Bootstrap.
/// </summary>
[Collection("Api")]
public class AuthTests
{
    private readonly ApiFactoryFixture _fx;
    public AuthTests(ApiFactoryFixture fx) => _fx = fx;

    // Test 1: Registrierung und Login funktionieren.
    [Fact]
    public async Task Auth_RegisterAndLogin_Work()
    {
        var email = TestData.UniqueEmail("auth");

        var registered = await Api.RegisterAsync(_fx.Client, email, "User!Pass123");
        Assert.False(string.IsNullOrEmpty(registered.AccessToken));
        Assert.Contains("User", registered.User.Roles);
        Assert.DoesNotContain("Admin", registered.User.Roles);

        var login = await Api.LoginAsync(_fx.Client, email, "User!Pass123");
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.Equal(email, login.User.Email);

        // Falsches Passwort -> 401.
        using var wrong = await Api.PostAsync(_fx.Client, "api/v1/auth/login", null,
            new { email, password = "Wrong!Pass123", deviceIdentifier = "test" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
    }

    // Test 2: Geschützte Endpunkte verlangen Authentifizierung.
    [Fact]
    public async Task AuthGuards_RequireAuthentication()
    {
        using (var mine = await _fx.Client.GetAsync("api/v1/licenses/mine"))
            Assert.Equal(HttpStatusCode.Unauthorized, mine.StatusCode);

        using (var chat = await Api.PostAsync(_fx.Client, "api/v1/chat/send", null,
                   new SendMessageRequest { ModelId = TestData.DefaultModelId, Message = "Hallo" }))
            Assert.Equal(HttpStatusCode.Unauthorized, chat.StatusCode);

        using (var admin = await _fx.Client.GetAsync("api/v1/admin/licenses"))
            Assert.Equal(HttpStatusCode.Unauthorized, admin.StatusCode);
    }

    // Test 3: Admin-Endpunkte sind nur für Admins; Registrierung vergibt keine Adminrechte.
    [Fact]
    public async Task AdminEndpoints_RejectNonAdmin_AndRegisterGrantsNoAdmin()
    {
        var user = await TestData.RegisterUserAsync(_fx.Client, "noadmin");
        Assert.DoesNotContain("Admin", user.User.Roles);

        using var adminCall = await Api.GetAsync(_fx.Client, "api/v1/admin/licenses", user.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, adminCall.StatusCode);

        var me = await Api.GetJsonAsync<UserDto>(_fx.Client, "api/v1/auth/me", user.AccessToken);
        Assert.DoesNotContain("Admin", me.Roles);
        Assert.Contains("User", me.Roles);
    }

    // Test 13: Nativer App-Login (Gerätekennung) inkl. Refresh-Flow.
    [Fact]
    public async Task App_DeviceLoginFlow_Works()
    {
        var email = TestData.UniqueEmail("app");
        await Api.RegisterAsync(_fx.Client, email, "User!Pass123");

        var login = await Api.LoginAsync(_fx.Client, email, "User!Pass123");
        Assert.False(string.IsNullOrEmpty(login.AccessToken));
        Assert.False(string.IsNullOrEmpty(login.RefreshToken));

        var mine = await Api.GetJsonAsync<List<LicenseDto>>(_fx.Client, "api/v1/licenses/mine", login.AccessToken);
        Assert.Empty(mine);

        using var refresh = await Api.PostAsync(_fx.Client, "api/v1/auth/refresh", null,
            new { refreshToken = login.RefreshToken, deviceIdentifier = "test-dev" });
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var refreshed = await Api.ReadAsync<AuthResponse>(refresh);
        Assert.NotNull(refreshed);
        Assert.False(string.IsNullOrEmpty(refreshed!.AccessToken));

        var me = await Api.GetJsonAsync<UserDto>(_fx.Client, "api/v1/auth/me", refreshed.AccessToken);
        Assert.Equal(email, me.Email);
    }

    // Test 16: Der Bootstrap-Admin lässt sich genau einmal anlegen.
    [Fact]
    public async Task Bootstrap_OnlyOnce_ThenLocked()
    {
        // Fixture hat den ersten Admin bereits angelegt -> erneuter Bootstrap wird gesperrt.
        using var second = await Api.PostAsync(_fx.Client, "api/v1/admin/setup", null,
            new { email = TestData.UniqueEmail("admin2"), password = "Admin!Pass999", fullName = "Zweiter" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);

        var status = await Api.GetJsonAsync<JsonElement>(_fx.Client, "api/v1/admin/setup/status", null);
        Assert.False(status.GetProperty("needsSetup").GetBoolean());
    }
}
