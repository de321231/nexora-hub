using Microsoft.AspNetCore.Components;
using Nexora.Shared.Models;
using Nexora.Web.Services;

namespace Nexora.Web.Components;

/// <summary>
/// Basis für alle geschützten Admin-Seiten. Leitet nicht angemeldete Benutzer zur
/// Anmeldeseite und angemeldete Nicht-Administratoren zur Zugriffsverweigerungs-Seite.
/// </summary>
public abstract class AdminPageBase : ComponentBase
{
    [Inject] protected TokenStore Tokens { get; set; } = default!;
    [Inject] protected NavigationManager Nav { get; set; } = default!;

    /// <summary>True, sobald der Benutzer als Admin bestätigt ist und die Seite rendern darf.</summary>
    protected bool Ready { get; private set; }

    /// <summary>True während Daten geladen werden.</summary>
    protected bool Loading { get; private set; } = true;

    /// <summary>Fehlermeldung der letzten Aktion, sonst null.</summary>
    protected string? Error { get; private set; }

    protected override void OnInitialized()
    {
        if (!Tokens.IsAuthenticated)
        {
            Nav.NavigateTo("/login");
            return;
        }
        if (!Tokens.IsAdmin)
        {
            Nav.NavigateTo("/admin/denied");
            return;
        }
        Ready = true;
    }

    /// <summary>Führt eine Aktion mit Lade-/Fehlerzustand aus.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        Loading = true;
        Error = null;
        await InvokeAsync(StateHasChanged);
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected void Retry() { }

    /// <summary>Gibt eine konsistente Status-Plakette für Lizenzstatus zurück.</summary>
    protected static string StatusBadge(LicenseStatus status) => status switch
    {
        LicenseStatus.Active => "active",
        LicenseStatus.Pending => "pending",
        LicenseStatus.Expired => "expired",
        LicenseStatus.Suspended => "expired",
        LicenseStatus.Revoked => "revoked",
        _ => "pending"
    };

    protected static string StatusBadge(LicenseKeyStatus status) => status switch
    {
        LicenseKeyStatus.Available => "active",
        LicenseKeyStatus.Redeemed => "pending",
        LicenseKeyStatus.Revoked => "revoked",
        LicenseKeyStatus.Expired => "expired",
        _ => "pending"
    };

    protected static string StatusBadge(AppStatus status) => status switch
    {
        AppStatus.Published => "active",
        AppStatus.Draft => "pending",
        AppStatus.Archived => "expired",
        _ => "pending"
    };

    protected static string Fmt(DateTime? value, string fallback = "—")
        => value.HasValue ? value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : fallback;

    protected static string FmtLong(long value)
        => value >= 1_000_000 ? $"{value / 1_000_000.0:0.#}M"
         : value >= 1_000 ? $"{value / 1_000.0:0.#}k"
         : value.ToString();
}