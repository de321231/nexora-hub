using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexora.Api.Data;
using Nexora.Api.Options;
using Nexora.Shared.Models;

namespace Nexora.Api.Services;

/// <summary>
/// Legt benötigte Rollen und die App "Nexora AI" idempotent an.
/// Es wird niemals automatisch ein Administrator erzeugt.
/// </summary>
public class DatabaseSeeder
{
    public const string AdminRole = "Admin";
    public const string UserRole = "User";
    public const string NexoraAiSlug = "nexora-ai";

    private readonly AppDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly LiteLlmOptions _liteLlm;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(AppDbContext db, RoleManager<IdentityRole> roleManager, IOptions<LiteLlmOptions> liteLlm, ILogger<DatabaseSeeder> logger)
    {
        _db = db;
        _roleManager = roleManager;
        _liteLlm = liteLlm.Value;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        var product = await SeedNexoraAiAsync();
        await SeedDefaultModelAsync(product);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in new[] { AdminRole, UserRole })
        {
            if (!await _roleManager.RoleExistsAsync(role))
                await _roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    private async Task<Product> SeedNexoraAiAsync()
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Slug == NexoraAiSlug);
        if (product == null)
        {
            product = new Product
            {
                Name = "Nexora AI",
                Slug = NexoraAiSlug,
                Description = "Nativer KI-Chat über LiteLLM.",
                Kind = AppType.WindowsProgram,
                Status = AppStatus.Published,
                LicenseRequired = true,
                IsActive = true
            };
            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            _logger.LogInformation("App 'Nexora AI' angelegt ({Id}).", product.Id);
        }
        return product;
    }

    private async Task SeedDefaultModelAsync(Product product)
    {
        if (string.IsNullOrWhiteSpace(_liteLlm.DefaultModel)) return;
        var modelId = _liteLlm.DefaultModel.Trim();
        if (await _db.AiModels.AnyAsync(m => m.ModelId == modelId)) return;
        _db.AiModels.Add(new AiModel
        {
            ModelId = modelId,
            DisplayName = modelId,
            Provider = modelId.Contains('/') ? modelId.Split('/')[0] : "",
            IsActive = true
        });
        await _db.SaveChangesAsync();
        _logger.LogInformation("Standardmodell '{Model}' aus Konfiguration angelegt.", modelId);
    }
}
