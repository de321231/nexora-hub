using Nexora.Shared.Models;

namespace Nexora.Api.Services;

/// <summary>
/// Zentrale Regeln für die zeitliche Gültigkeit einer Lizenz.
/// </summary>
public static class LicenseEvaluator
{
    /// <summary>
    /// Errechnet den effektiven Status aus manuellen Statusangaben und den
    /// Zeitfenstern. Revoked/Suspended bleiben unangetastet.
    /// </summary>
    public static LicenseStatus ComputeStatus(License license, DateTime nowUtc)
    {
        if (license.Status is LicenseStatus.Revoked or LicenseStatus.Suspended)
            return license.Status;

        if (license.ExpiresAt.HasValue && license.ExpiresAt.Value <= nowUtc)
            return LicenseStatus.Expired;

        if (license.StartsAt.HasValue && license.StartsAt.Value > nowUtc)
            return LicenseStatus.Pending;

        return LicenseStatus.Active;
    }

    public static bool IsUsable(License license, DateTime nowUtc)
        => ComputeStatus(license, nowUtc) == LicenseStatus.Active;

    /// <summary>Aktualisiert den gespeicherten Status, falls er vom effektiven abweicht.</summary>
    public static bool RefreshStoredStatus(License license, DateTime nowUtc)
    {
        var effective = ComputeStatus(license, nowUtc);
        if (license.Status != effective)
        {
            license.Status = effective;
            license.UpdatedAt = nowUtc;
            return true;
        }
        return false;
    }
}
