using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;

namespace Nexora.Api.Tests;

/// <summary>
/// Test 11: Lizenzschlüssel werden ausschließlich gehasht gespeichert; der
/// Klartext erscheint genau einmal (bei der Erstellung) und nie wieder.
/// </summary>
[Collection("Api")]
public class SecurityTests
{
    private readonly ApiFactoryFixture _fx;
    public SecurityTests(ApiFactoryFixture fx) => _fx = fx;

    [Fact]
    public async Task Keys_StoredHashed_PlaintextOnlyOnce()
    {
        var admin = await Api.AdminLoginAsync(_fx.Client);
        var app = await TestData.NexoraAiAppAsync(_fx.Client, admin.AccessToken);
        var model = await TestData.DefaultModelAsync(_fx.Client, admin.AccessToken);
        var license = await TestData.CreateLicenseAsync(_fx.Client, admin.AccessToken, app.Id, model.Id);

        var request = new CreateLicenseKeyRequest { LicenseId = license.Id, Count = 1, MaxUses = 1 };
        var created = await Api.PostJsonAsync<List<CreatedLicenseKeyDto>>(
            _fx.Client, "api/v1/admin/license-keys", admin.AccessToken, request);
        var plain = Assert.Single(created).Key;
        Assert.False(string.IsNullOrWhiteSpace(plain));
        Assert.StartsWith("NX-", plain);

        // Datenbank-Assertion: nur der SHA-256-Hash ist gespeichert.
        using (var scope = _fx.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.LicenseKeys.AsNoTracking()
                .Where(k => k.LicenseId == license.Id)
                .ToListAsync();
            var key = Assert.Single(stored);
            Assert.NotEqual(plain, key.KeyHash);
            Assert.Equal(LicenseKeyGenerator.Hash(plain), key.KeyHash);
            Assert.DoesNotContain(plain, key.KeyHash);
        }

        // Der Listen-Endpunkt darf den Klartext niemals zurückgeben.
        using (var list = await Api.GetAsync(_fx.Client, $"api/v1/admin/license-keys?licenseId={license.Id}", admin.AccessToken))
        {
            list.EnsureSuccessStatusCode();
            var json = await list.Content.ReadAsStringAsync();
            Assert.DoesNotContain(plain, json);
        }
    }
}
