using System.ComponentModel.DataAnnotations;

namespace Nexora.Shared.Models;

/// <summary>
/// Ein Gerät, auf dem eine Lizenz aktiviert wurde. Die Geräte-ID ist ein
/// zufällig erzeugter Bezeichner – bewusst kein Hardware-Fingerprint.
/// </summary>
public class Device
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();

    public string? UserId { get; set; }

    [MaxLength(200)] public string DeviceName { get; set; } = "";

    /// <summary>Zufällige, vom Client erzeugte Geräte-ID.</summary>
    [MaxLength(128)] public string DeviceIdentifier { get; set; } = "";

    [MaxLength(40)] public string Platform { get; set; } = "Windows";

    [MaxLength(128)] public string MachineHash { get; set; } = "";

    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
