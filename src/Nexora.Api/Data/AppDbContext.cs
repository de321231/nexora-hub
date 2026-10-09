using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Nexora.Shared.Models;

namespace Nexora.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<AppDefinition> AppDefinitions => Set<AppDefinition>();
    public DbSet<AppVersion> AppVersions => Set<AppVersion>();
    public DbSet<Download> Downloads => Set<Download>();
    public DbSet<License> Licenses => Set<License>();
    public DbSet<LicenseKey> LicenseKeys => Set<LicenseKey>();
    public DbSet<UserLicense> UserLicenses => Set<UserLicense>();
    public DbSet<AiModel> AiModels => Set<AiModel>();
    public DbSet<LicenseModel> LicenseModels => Set<LicenseModel>();
    public DbSet<Activation> Activations => Set<Activation>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<UsagePeriod> UsagePeriods => Set<UsagePeriod>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<ChatConversation> ChatConversations => Set<ChatConversation>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.HasDefaultSchema("nexora");

        // --- Legacy content-distribution tables (kept for compatibility) ---
        b.Entity<Product>().ToTable("Products");
        b.Entity<Product>().HasIndex(p => p.Slug).IsUnique();

        b.Entity<AppDefinition>().ToTable("AppDefinitions");
        b.Entity<AppDefinition>().HasIndex(a => a.ProductId);

        b.Entity<AppVersion>().ToTable("AppVersions");
        b.Entity<AppVersion>().HasIndex(a => a.AppId);

        b.Entity<Download>().ToTable("Downloads");
        b.Entity<Download>().HasIndex(d => d.AppVersionId);

        // --- Licensing ---
        b.Entity<License>().ToTable("Licenses");
        b.Entity<License>().HasIndex(l => l.ProductId);
        b.Entity<License>().HasIndex(l => l.Status);
        b.Entity<License>()
            .HasOne(l => l.Product).WithMany()
            .HasForeignKey(l => l.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<LicenseKey>().ToTable("LicenseKeys");
        b.Entity<LicenseKey>().HasIndex(k => k.KeyHash).IsUnique();
        b.Entity<LicenseKey>().HasIndex(k => k.Status);
        b.Entity<LicenseKey>()
            .HasOne(k => k.License).WithMany()
            .HasForeignKey(k => k.LicenseId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<UserLicense>().ToTable("UserLicenses");
        b.Entity<UserLicense>().HasIndex(u => new { u.UserId, u.LicenseId }).IsUnique();
        b.Entity<UserLicense>().HasIndex(u => u.UserId);
        b.Entity<UserLicense>()
            .HasOne(u => u.License).WithMany()
            .HasForeignKey(u => u.LicenseId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- AI models ---
        b.Entity<AiModel>().ToTable("AiModels");
        b.Entity<AiModel>().HasIndex(m => m.ModelId).IsUnique();

        b.Entity<LicenseModel>().ToTable("LicenseModels");
        b.Entity<LicenseModel>().HasIndex(lm => new { lm.LicenseId, lm.AiModelId }).IsUnique();
        b.Entity<LicenseModel>()
            .HasOne(lm => lm.License).WithMany()
            .HasForeignKey(lm => lm.LicenseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<LicenseModel>()
            .HasOne(lm => lm.AiModel).WithMany()
            .HasForeignKey(lm => lm.AiModelId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Devices / activations ---
        b.Entity<Device>().ToTable("Devices");
        b.Entity<Device>().HasIndex(d => d.UserId);
        b.Entity<Device>().HasIndex(d => d.DeviceIdentifier).IsUnique();

        b.Entity<Activation>().ToTable("Activations");
        b.Entity<Activation>().HasIndex(a => a.LicenseId);
        b.Entity<Activation>().HasIndex(a => a.DeviceId);
        b.Entity<Activation>()
            .HasOne(a => a.License).WithMany()
            .HasForeignKey(a => a.LicenseId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Activation>()
            .HasOne(a => a.Device).WithMany()
            .HasForeignKey(a => a.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Usage ---
        b.Entity<UsagePeriod>().ToTable("UsagePeriods");
        b.Entity<UsagePeriod>()
            .HasIndex(u => new { u.UserId, u.LicenseId, u.PeriodType, u.PeriodStartUtc })
            .IsUnique();

        b.Entity<UsageEvent>().ToTable("UsageEvents");
        b.Entity<UsageEvent>().HasIndex(e => e.CreatedAtUtc);
        b.Entity<UsageEvent>().HasIndex(e => e.UserId);
        b.Entity<UsageEvent>().HasIndex(e => e.LicenseId);

        // --- Chat ---
        b.Entity<ChatConversation>().ToTable("ChatConversations");
        b.Entity<ChatConversation>().HasIndex(c => c.UserId);

        b.Entity<ChatMessage>().ToTable("ChatMessages");
        b.Entity<ChatMessage>().HasIndex(m => m.ConversationId);
        b.Entity<ChatMessage>()
            .HasOne(m => m.Conversation).WithMany()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        // --- Refresh tokens ---
        b.Entity<RefreshToken>().ToTable("RefreshTokens");
        b.Entity<RefreshToken>().HasIndex(t => t.TokenHash).IsUnique();
        b.Entity<RefreshToken>().HasIndex(t => t.UserId);

        // --- Audit ---
        b.Entity<AuditLog>().ToTable("AuditLogs");
        b.Entity<AuditLog>().HasIndex(a => a.Timestamp);
    }
}
