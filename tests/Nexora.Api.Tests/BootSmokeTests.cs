using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Nexora.Api.Tests;

/// <summary>
/// Schneller Boot-Check: prüft, ob der Test-Host mit SQLite, Fake-LiteLLM und
/// Seeder überhaupt startet und der Bootstrap-Admin angelegt wurde.
/// </summary>
[Collection("Api")]
public class BootSmokeTests : IClassFixture<ApiFactoryFixture>
{
    private readonly ApiFactoryFixture _fx;
    public BootSmokeTests(ApiFactoryFixture fx) => _fx = fx;

    [Fact]
    public async Task Health_ReturnsUp()
    {
        using var resp = await _fx.Client.GetAsync("health");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("up", body);
    }

    [Fact]
    public async Task SetupStatus_NeedsSetupIsFalse()
    {
        using var resp = await _fx.Client.GetAsync("api/v1/admin/setup/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.False(doc.RootElement.GetProperty("needsSetup").GetBoolean());
    }

    [Fact]
    public async Task AdminLogin_AfterBootstrap_ReturnsTokens()
    {
        var auth = await Api.AdminLoginAsync(_fx.Client);
        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        Assert.False(string.IsNullOrEmpty(auth.RefreshToken));
        Assert.Contains("Admin", auth.User.Roles);
    }

    [Fact]
    public async Task TestDatabase_ContainsAllLicensingTables()
    {
        await Task.CompletedTask;
        var names = new List<string>();
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_fx.Factory.DbPath}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name";
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) names.Add(reader.GetString(0));
        }

        // Nachweis für die Report-Dokumentation: wie EF das "nexora"-Schema in SQLite abbildet.
        var report = string.Join(" | ", names);
        Assert.True(names.Count > 10, "Zu wenige Tabellen angelegt: " + report);
        Assert.Contains(names, n => n.Contains("Products"));
        Assert.Contains(names, n => n.Contains("Licenses"));
        Assert.Contains(names, n => n.Contains("LicenseKeys"));
        Assert.Contains(names, n => n.Contains("AiModels"));
        Assert.Contains(names, n => n.Contains("AspNetUsers"));
    }
}