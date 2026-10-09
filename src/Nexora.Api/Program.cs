using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Nexora.Api.Data;
using Nexora.Api.Options;
using Nexora.Api.Services;
using Nexora.Shared.Models;
using Serilog;

// ---------------------------------------------------------------------------
// .env einlesen (nicht eingecheckt) – Werte werden nur gesetzt, wenn noch nicht vorhanden.
// ---------------------------------------------------------------------------
static void LoadDotEnv(string contentRoot)
{
    foreach (var candidate in new[] { Path.Combine(contentRoot, ".env"), Path.Combine(contentRoot, "..", ".env"), Path.Combine(contentRoot, "..", "..", ".env") })
    {
        var path = Path.GetFullPath(candidate);
        if (!File.Exists(path)) continue;
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var idx = trimmed.IndexOf('=');
            if (idx <= 0) continue;
            var key = trimmed[..idx].Trim();
            var value = trimmed[(idx + 1)..].Trim();
            if (Environment.GetEnvironmentVariable(key) == null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }
}

static string ParseNpgsqlConnectionString(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
        return "Host=localhost;Database=nexorahub;Username=nexora;Password=nexora";
    if (raw.Contains("Host=") || raw.Contains("Server="))
        return raw; // bereits ADO.NET-Format
    try
    {
        var uri = new Uri(raw);
        var userInfo = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(userInfo[0]);
        var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var port = uri.Port > 0 ? uri.Port : 5432;
        var db = uri.AbsolutePath.TrimStart('/');
        return $"Host={uri.Host};Port={port};Database={db};Username={user};Password={pass};SSL Mode=Prefer";
    }
    catch
    {
        return "Host=localhost;Database=nexorahub;Username=nexora;Password=nexora";
    }
}

var builder = WebApplication.CreateBuilder(args);
LoadDotEnv(builder.Environment.ContentRootPath);

builder.Host.UseSerilog((ctx, lc) => lc.WriteTo.Console().ReadFrom.Configuration(ctx.Configuration));

// --- Konfiguration ---
var jwtOptions = new JwtOptions
{
    Issuer = Environment.GetEnvironmentVariable("NEXORA_JWT_ISSUER") ?? "Nexora",
    Audience = Environment.GetEnvironmentVariable("NEXORA_JWT_AUDIENCE") ?? "NexoraClients",
    Key = Environment.GetEnvironmentVariable("NEXORA_JWT_KEY") ?? ""
};
if (string.IsNullOrWhiteSpace(jwtOptions.Key) || Encoding.UTF8.GetByteCount(jwtOptions.Key) < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        jwtOptions.Key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        Log.Warning("Kein JWT-Schlüssel konfiguriert (NEXORA_JWT_KEY). Es wird ein flüchtiger Entwicklungsschlüssel verwendet; Tokens werden nach einem Neustart ungültig.");
    }
    else
    {
        throw new InvalidOperationException("NEXORA_JWT_KEY fehlt (min. 32 Byte). In Produktion zwingend erforderlich.");
    }
}
builder.Configuration["Jwt:Key"] = jwtOptions.Key;
builder.Configuration["Jwt:Issuer"] = jwtOptions.Issuer;
builder.Configuration["Jwt:Audience"] = jwtOptions.Audience;

builder.Services.Configure<JwtOptions>(o =>
{
    o.Issuer = jwtOptions.Issuer;
    o.Audience = jwtOptions.Audience;
    o.Key = jwtOptions.Key;
    o.AccessTokenMinutes = int.TryParse(Environment.GetEnvironmentVariable("NEXORA_JWT_ACCESS_MINUTES"), out var am) ? am : 60;
    o.RefreshTokenDays = int.TryParse(Environment.GetEnvironmentVariable("NEXORA_JWT_REFRESH_DAYS"), out var rd) ? rd : 30;
});

builder.Services.Configure<LiteLlmOptions>(o =>
{
    o.BaseUrl = Environment.GetEnvironmentVariable("NEXORA_LITELLM_URL") ?? builder.Configuration["LiteLlm:BaseUrl"] ?? "";
    o.ApiKey = Environment.GetEnvironmentVariable("NEXORA_LITELLM_KEY") ?? builder.Configuration["LiteLlm:ApiKey"] ?? "";
    o.DefaultModel = Environment.GetEnvironmentVariable("NEXORA_LITELLM_DEFAULT_MODEL") ?? builder.Configuration["LiteLlm:DefaultModel"] ?? "";
    o.TimeoutSeconds = 120;
});

builder.Services.Configure<BootstrapOptions>(o =>
{
    o.Secret = Environment.GetEnvironmentVariable("NEXORA_BOOTSTRAP_SECRET") ?? "";
});

// --- Datenbank ---
var connStrEnv = Environment.GetEnvironmentVariable("NEXORA_DB");
var configuredConn = builder.Configuration.GetConnectionString("DefaultConnection");
var rawConn = !string.IsNullOrWhiteSpace(configuredConn) ? configuredConn
    : !string.IsNullOrWhiteSpace(connStrEnv) ? connStrEnv
    : "Host=localhost;Database=nexorahub;Username=nexora;Password=nexora";
var connStr = ParseNpgsqlConnectionString(rawConn);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(connStr, npgsql =>
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "nexora")));

// --- Identity ---
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.SignIn.RequireConfirmedAccount = false;
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 8;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    o.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// --- Authentifizierung (JWT, geeignet für native Clients) ---
// Wichtig: AddIdentity setzt das Standard-Schema auf Cookies. Hier wird das
// JWT-Bearer-Schema daher explizit als Standard für Authentifizierung und
// Challenge gesetzt.
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = System.Security.Claims.ClaimTypes.Name,
            RoleClaimType = System.Security.Claims.ClaimTypes.Role
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", p => p.RequireRole(DatabaseSeeder.AdminRole));
});

// --- Rate Limiting (Brute-Force-Schutz) ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
            QueueLimit = 0
        }));
});

// --- HttpClients ---
builder.Services.AddHttpClient("litellm", (sp, client) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LiteLlmOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
    {
        var baseUrl = opt.BaseUrl.TrimEnd('/') + "/";
        client.BaseAddress = new Uri(baseUrl);
    }
    client.Timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
});

// --- Anwendungsdienste ---
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IQuotaService, QuotaService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<ILiteLlmClient, LiteLlmClient>();
builder.Services.AddScoped<DatabaseSeeder>();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Nexora API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer Token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", doc),
            new List<string>()
        }
    });
});

// --- CORS ---
var allowedOrigins = (Environment.GetEnvironmentVariable("NEXORA_CORS_ORIGINS") ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(p => p.AddPolicy("NexoraCors", b =>
{
    if (allowedOrigins.Length > 0)
    {
        b.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
    }
    else if (builder.Environment.IsDevelopment())
    {
        b.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    }
    // Produktion ohne konfigurierte Origins: keine CORS-Erlaubnis. Browser
    // blockieren dadurch Cross-Origin-Aufrufe; die nativen Desktop-Clients
    // sind von CORS nicht betroffen.
}));

// --- Reverse-Proxy-Header (optional, NEXORA_USE_FORWARDED_HEADERS=1) ---
// Voraussetzung: Die API ist ausschließlich über den vertrauenswürdigen
// Reverse Proxy erreichbar, damit X-Forwarded-* nicht gespooft werden kann.
if (Environment.GetEnvironmentVariable("NEXORA_USE_FORWARDED_HEADERS") == "1")
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                          | ForwardedHeaders.XForwardedProto
                          | ForwardedHeaders.XForwardedHost;
        o.KnownIPNetworks.Clear();
        o.KnownProxies.Clear();
        o.ForwardLimit = null;
    });
}

var app = builder.Build();

// --- Reverse-Proxy-Header anwenden (muss vor allen anderen Middleware laufen) ---
if (Environment.GetEnvironmentVariable("NEXORA_USE_FORWARDED_HEADERS") == "1")
    app.UseForwardedHeaders();

// --- Kontrollierter Migrationsmodus ---
// Führt ausschließlich ausstehende Migrationen aus und beendet sich danach.
// Verwendung: docker compose run --rm nexora-api --migrate
if (args.Contains("--migrate"))
{
    using var migrateScope = app.Services.CreateScope();
    var migrateLogger = migrateScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var migrateDb = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var pending = (await migrateDb.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count == 0)
    {
        migrateLogger.LogInformation("Keine ausstehenden Migrationen – Datenbank ist aktuell.");
    }
    else
    {
        migrateLogger.LogInformation("Wende {Count} ausstehende Migration(en) an: {Pending}",
            pending.Count, string.Join(", ", pending));
        await migrateDb.Database.MigrateAsync();
        migrateLogger.LogInformation("Migrationen erfolgreich angewendet.");
    }
    return;
}

// --- Startaufgaben ---
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (Environment.GetEnvironmentVariable("NEXORA_AUTO_MIGRATE") == "1")
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Migrationen angewendet.");
        }
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Startinitialisierung fehlgeschlagen (DB erreichbar?).");
    }
}

app.UseSerilogRequestLogging();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("NexoraCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "up", service = "Nexora.Api", utc = DateTime.UtcNow }));
// Readiness: true nur, wenn die Datenbank erreichbar ist (überschreibt nichts, nur Lesen).
app.MapGet("/health/ready", async (AppDbContext db, CancellationToken ct) =>
{
    var dbOk = await db.Database.CanConnectAsync(ct);
    return dbOk
        ? Results.Ok(new { status = "ready", service = "Nexora.Api" })
        : Results.Json(new { status = "not_ready", service = "Nexora.Api" },
            statusCode: StatusCodes.Status503ServiceUnavailable);
});
app.MapGet("/error", () => Results.Problem(title: "Interner Serverfehler", statusCode: StatusCodes.Status500InternalServerError));

app.Run();

public partial class Program { }
