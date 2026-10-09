namespace Nexora.Api.Options;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "Nexora";
    public string Audience { get; set; } = "NexoraClients";

    /// <summary>Symmetrischer Schlüssel (min. 32 Byte). Aus Konfiguration/Umgebungsvariable.</summary>
    public string Key { get; set; } = "";

    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}

public class LiteLlmOptions
{
    public const string SectionName = "LiteLlm";

    /// <summary>Basis-URL der LiteLLM-Instanz, z. B. http://192.168.188.198:4000 .</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>API-Schlüssel für LiteLLM. Verbleibt ausschließlich serverseitig.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Optionales Standardmodell (wird beim Start als Modell angelegt).</summary>
    public string DefaultModel { get; set; } = "";

    public int TimeoutSeconds { get; set; } = 120;
}

public class BootstrapOptions
{
    public const string SectionName = "Bootstrap";

    /// <summary>Optionales Geheimnis, das der einmalige Admin-Bootstrap zusätzlich verlangt.</summary>
    public string Secret { get; set; } = "";
}
