namespace Nexora.Shared.Models;

/// <summary>Art der lizenzierten Anwendung.</summary>
public enum AppType
{
    WebApp = 0,
    WindowsProgram = 1
}

/// <summary>Veröffentlichungsstatus einer App / eines Produkts.</summary>
public enum AppStatus
{
    Draft = 0,
    Published = 1,
    Archived = 2
}

/// <summary>
/// Status einer Lizenz.
/// Active = Aktiv, Pending = Ausstehend, Expired = Abgelaufen,
/// Suspended = Gesperrt, Revoked = Widerrufen.
/// </summary>
public enum LicenseStatus
{
    Pending = 0,
    Active = 1,
    Expired = 2,
    Suspended = 3,
    Revoked = 4
}

/// <summary>Status eines Lizenzschlüssels.</summary>
public enum LicenseKeyStatus
{
    Available = 0,
    Redeemed = 1,
    Revoked = 2,
    Expired = 3
}

/// <summary>Zeitraumtyp für Kontingente.</summary>
public enum UsagePeriodType
{
    Day = 0,
    Month = 1
}

/// <summary>Rolle einer Chat-Nachricht.</summary>
public enum ChatRole
{
    System = 0,
    User = 1,
    Assistant = 2
}

/// <summary>Protokollierte Aktion.</summary>
public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Login = 3,
    LoginFailed = 4,
    Logout = 5,
    Register = 6,
    Activate = 7,
    Revoke = 8,
    Download = 9,
    RedeemKey = 10,
    LicenseAssigned = 11,
    LicenseRemoved = 12,
    QuotaExceeded = 13,
    ChatRequest = 14
}
