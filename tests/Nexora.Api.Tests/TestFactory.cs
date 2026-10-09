using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Api.Data;
using Nexora.Api.Options;
using Nexora.Api.Services;
using System.Net.Http.Json;
using System.Threading.RateLimiting;

namespace Nexora.Api.Tests;

/// <summary>
/// Test-WebHost: SQLite statt Postgres (isolierter Zustand, keine Berührung der
/// Wharf-Datenbank), Fake-LiteLLM statt echtem KI-Dienst, seeder mit Schemaanlage
/// und hohe Rate-Limits, damit Brute-Force-Assertions nicht das Testkontingent kappen.
/// </summary>
public class NexoraAppFactory : WebApplicationFactory<Program>
{
    public const string JwtKey = "Xt8Qk3vP9sN2mR5wU7hJ4cD1fL0gB6yT1aE4oI2uY9pS6nK3qM7zW5vC0xR8jV4"; // 64 Zeichen

    /// <summary>Dateibasierte SQLite-DB: getrennte Verbindungen für parallele Anfragen.</summary>
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"nexora-tests-{Guid.NewGuid():N}.db");

    private string ConnectionString =>
        new SqliteConnectionStringBuilder
        {
            DataSource = _dbPath,
            ForeignKeys = true,
            DefaultTimeout = 30 // Busy-Timeout: parallele Zugriffe serialisieren sauber
        }.ToString();

    /// <summary>Pfad der Testdatenbank (für direkte DB-Assertions in Tests).</summary>
    public string DbPath => _dbPath;

    public NexoraAppFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("NEXORA_JWT_KEY", JwtKey);
        Environment.SetEnvironmentVariable("NEXORA_BOOTSTRAP_SECRET", "");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            // Datenbank durch isoliertes SQLite ersetzen (Wharf-DB bleibt unberührt).
            // Wichtig: auch die EF-Optionskonfiguration entfernen, sonst bleiben
            // Npgsql- und SQLite-Provider gleichzeitig registriert.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(ConnectionString));

            // Seeder: legt zuerst das Schema an (SQLite kennt keine Npgsql-Migrationen)
            // und erzeugt dann Rollen, App "Nexora AI" und das Standardmodell.
            services.RemoveAll<DatabaseSeeder>();
            services.AddScoped<DatabaseSeeder>(sp =>
            {
                var db = sp.GetRequiredService<AppDbContext>();
                db.Database.EnsureCreated();
                return new DatabaseSeeder(
                    db,
                    sp.GetRequiredService<RoleManager<IdentityRole>>(),
                    sp.GetRequiredService<IOptions<LiteLlmOptions>>(),
                    sp.GetRequiredService<ILogger<DatabaseSeeder>>());
            });

            // Fake-LiteLLM – keine externe Infrastruktur in Tests.
            services.RemoveAll<ILiteLlmClient>();
            services.AddScoped<ILiteLlmClient>(_ => new FakeLiteLlmClient());

            // Standardmodell für den Seeder (erzeugt ein freigeschaltetes AiModel).
            services.Configure<LiteLlmOptions>(o => o.DefaultModel = "openai/gpt-4o-mini");

            // Rate-Limits für Tests großzügig (Brute-Force-Logik bleibt erhalten).
            // Auch die bestehende Options-Konfiguration entfernen, sonst würde die
            // Policy "login" doppelt registriert.
            services.RemoveAll<RateLimiterOptions>();
            services.RemoveAll<IConfigureOptions<RateLimiterOptions>>();
            services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                o.AddPolicy("login", _ => RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: "tests",
                    factory: _ => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
                    {
                        Window = TimeSpan.FromMinutes(1),
                        PermitLimit = 100_000,
                        QueueLimit = 0
                    }));
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

/// <summary>Liefert deterministische Fake-Ergebnisse des KI-Dienstes.</summary>
public sealed class FakeLiteLlmClient : ILiteLlmClient
{
    public bool IsConfigured => true;

    public Task<LiteLlmResult> ChatAsync(string modelId, IReadOnlyList<LiteLlmMessage> messages, int? maxTokens, CancellationToken ct)
        => Task.FromResult(new LiteLlmResult(
            Content: "Das ist die Antwort des Fake-KI-Dienstes aus dem Integrationstest. **Markdown wird unterstützt.**",
            Model: modelId,
            InputTokens: 14,
            OutputTokens: 22,
            TokensEstimated: false,
            CostUsd: 0.0008m));

    public Task<IReadOnlyList<LiteLlmModelInfo>> ListModelsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<LiteLlmModelInfo>>(new List<LiteLlmModelInfo>
        {
            new("openai/gpt-4o-mini", "GPT-4o mini", "openai", 128000)
        });
}

/// <summary>Teilt eine einzige AppFactory über alle Tests (eine Testdatenbank).</summary>
public sealed class ApiFactoryFixture : IDisposable
{
    public NexoraAppFactory Factory { get; }
    public HttpClient Client { get; }

    public ApiFactoryFixture()
    {
        Factory = new NexoraAppFactory();
        Client = Factory.CreateClient();

        // Ersten Admin über den einmaligen Bootstrap anlegen.
        var response = Task.Run(() => Client.PostAsJsonAsync(
            "api/v1/admin/setup",
            new { email = AdminEmail, password = AdminPassword, fullName = "Test-Admin" },
            Json.Options)).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();
    }

    public const string AdminEmail = "admin@nexora.test";
    public const string AdminPassword = "Admin!Pass123";

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
    }
}

/// <summary>Collection-Fixture: eine Instanz für die gesamte Testklasse.</summary>
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFactoryFixture> { }