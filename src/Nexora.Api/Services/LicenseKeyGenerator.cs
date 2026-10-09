using System.Security.Cryptography;
using System.Text;

namespace Nexora.Api.Services;

/// <summary>
/// Erzeugt kryptografisch sichere Lizenzschlüssel und deren Hashes.
/// Der Klartextschlüssel wird niemals gespeichert.
/// </summary>
public static class LicenseKeyGenerator
{
    // Ohne mehrdeutige Zeichen (0/O, 1/I/L).
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public static (string Plain, string Hash, string Prefix) Generate(string licensePrefix, int groups = 3, int groupSize = 5)
    {
        var sb = new StringBuilder();
        for (int g = 0; g < groups; g++)
        {
            if (g > 0) sb.Append('-');
            for (int i = 0; i < groupSize; i++)
                sb.Append(Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]);
        }
        var body = sb.ToString();
        var plain = $"{licensePrefix}-{body}";
        var hash = Hash(plain);
        var prefix = $"{licensePrefix}-{body.Split('-')[0]}";
        return (plain, hash, prefix);
    }

    /// <summary>Normalisiert (Groß-/Kleinschreibung, Leerzeichen) und hasht.</summary>
    public static string Hash(string plain)
    {
        var normalized = Normalize(plain);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    public static string Normalize(string plain)
        => plain.Trim().Replace(" ", "").ToUpperInvariant();
}
