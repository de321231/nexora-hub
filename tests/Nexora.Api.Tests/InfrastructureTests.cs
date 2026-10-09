using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Npgsql;

namespace Nexora.Api.Tests;

/// <summary>
/// Test 12, 14, 15: Build-Fähigkeit von Web/Desktop, Legacy-Endpunkt
/// <c>api/v1/apps</c> und die Zusicherung, dass das Wharf-Schema <c>public</c>
/// unangetastet bleibt.
/// </summary>
[Collection("Api")]
public class InfrastructureTests
{
    private readonly ApiFactoryFixture _fx;
    public InfrastructureTests(ApiFactoryFixture fx) => _fx = fx;

    // Test 12: Die Projekte Nexora.Web und Nexora.Desktop bauen fehlerfrei.
    // (Nexora.Api/Shared werden durch den Testlauf selbst kompiliert.)
    [Fact]
    public async Task Build_WebAndDesktop_Succeed()
    {
        var root = RepoRoot();
        foreach (var project in new[]
                 {
                     Path.Combine("src", "Nexora.Web", "Nexora.Web.csproj"),
                     Path.Combine("src", "Nexora.Desktop", "Nexora.Desktop.csproj")
                 })
        {
            var (exitCode, output) = await RunAsync("dotnet",
                $"build \"{Path.Combine(root, project)}\" --nologo -v q", root);
            Assert.True(exitCode == 0, $"Build von {project} fehlgeschlagen (Exit {exitCode}):\n{output}");
        }
    }

    // Test 14: Der Legacy-Endpunkt api/v1/apps antwortet weiterhin.
    [Fact]
    public async Task LegacyApi_V1Apps_StillWorks()
    {
        using (var list = await _fx.Client.GetAsync("api/v1/apps"))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var apps = await Api.ReadAsync<List<JsonElement>>(list);
            Assert.NotNull(apps);
        }

        using (var missing = await _fx.Client.GetAsync($"api/v1/apps/{Guid.NewGuid()}"))
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // Test 15: Im Wharf-Schema public tauchen keine Nexora-Tabellen auf; die
    // Migrationen liegen ausschließlich im Schema nexora.
    [Fact]
    public async Task WharfPublicSchema_Unchanged()
    {
        var raw = Environment.GetEnvironmentVariable("NEXORA_DB");
        if (string.IsNullOrWhiteSpace(raw))
        {
            Assert.Fail("NEXORA_DB ist nicht konfiguriert; Wharf-Prüfung kann nicht ausgeführt werden.");
            return;
        }

        NpgsqlConnectionStringBuilder builder;
        try { builder = ParseNpgsql(raw); }
        catch (Exception ex)
        {
            Assert.Fail("NEXORA_DB konnte nicht geparst werden: " + ex.Message);
            return;
        }

        builder.Timeout = 5;
        await using var conn = new NpgsqlConnection(builder.ConnectionString);
        try { await conn.OpenAsync(); }
        catch (Exception ex) { Assert.Fail("Wharf-DB nicht erreichbar: " + ex.Message); }

        var publicTables = new List<string>();
        await using (var cmd = new NpgsqlCommand(
                   "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public'", conn))
        await using (var reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync()) publicTables.Add(reader.GetString(0));

        // Keine der Nexora-Tabellen darf im fremden public-Schema liegen.
        string[] nexoraTables =
        {
            "Products", "Licenses", "LicenseKeys", "LicenseModels", "UserLicenses",
            "AiModels", "UsagePeriods", "UsageEvents", "ChatConversations", "ChatMessages",
            "RefreshTokens", "AuditLogs", "Devices", "Activations", "AspNetUsers"
        };
        foreach (var table in nexoraTables)
            Assert.DoesNotContain(table, publicTables);

        // Unsere Tabellen liegen im eigenen Schema.
        await using var countCmd = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'nexora'", conn);
        var nexoraCount = Convert.ToInt64(await countCmd.ExecuteScalarAsync());
        Assert.True(nexoraCount > 0, "Das Schema 'nexora' enthält keine Tabellen.");
    }

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "src")))
            dir = Directory.GetParent(dir)?.FullName;
        return dir ?? throw new InvalidOperationException("Repository-Wurzel nicht gefunden.");
    }

    private static async Task<(int ExitCode, string Output)> RunAsync(string file, string args, string workDir)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = Process.Start(psi)!;
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, stdout + "\n" + stderr);
    }

    private static NpgsqlConnectionStringBuilder ParseNpgsql(string raw)
    {
        if (raw.Contains("Host=") || raw.Contains("Server="))
            return new NpgsqlConnectionStringBuilder(raw);

        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "",
            SslMode = SslMode.Prefer
        };
    }
}
