using Microsoft.AspNetCore.HttpOverrides;
using Nexora.Web.Components;
using Nexora.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Reverse-Proxy-Header (optional, NEXORA_USE_FORWARDED_HEADERS=1) – vorausgesetzt,
// das Panel ist ausschließlich über den vertrauenswürdigen Reverse Proxy erreichbar.
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

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Sitzung (JWT) wird serverseitig pro Circuit gehalten – nie im Browser.
builder.Services.AddScoped<TokenStore>();

builder.Services.AddHttpClient<NexoraApiClient>(client =>
{
    var baseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5199";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

if (Environment.GetEnvironmentVariable("NEXORA_USE_FORWARDED_HEADERS") == "1")
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseAntiforgery();

app.MapStaticAssets();
app.MapGet("/health", () => Results.Ok(new { status = "up", service = "Nexora.Web" }));
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
