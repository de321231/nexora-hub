using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Services;

public static class DtoMapper
{
    public static UserDto ToDto(ApplicationUser u, IEnumerable<string>? roles = null, int activeLicenses = 0) => new()
    {
        Id = u.Id,
        Email = u.Email ?? "",
        FullName = u.FullName,
        IsActive = u.IsActive,
        IsLocked = u.IsLocked,
        CreatedAt = u.CreatedAt,
        LastLoginAtUtc = u.LastLoginAtUtc,
        Roles = roles?.ToList() ?? new(),
        ActiveLicenses = activeLicenses
    };

    public static AppDto ToDto(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Slug = p.Slug,
        Description = p.Description,
        Kind = p.Kind,
        Status = p.Status,
        LicenseRequired = p.LicenseRequired,
        IsActive = p.IsActive,
        CreatedAt = p.CreatedAt
    };

    public static AiModelDto ToDto(AiModel m) => new()
    {
        Id = m.Id,
        ModelId = m.ModelId,
        DisplayName = m.DisplayName,
        Provider = m.Provider,
        IsActive = m.IsActive,
        MaxContextTokens = m.MaxContextTokens,
        InputCostPer1M = m.InputCostPer1M,
        OutputCostPer1M = m.OutputCostPer1M
    };

    public static LicenseDto ToDto(License l, IEnumerable<LicenseModel>? licenseModels = null, int activeDevices = 0)
    {
        var dto = new LicenseDto
        {
            Id = l.Id,
            Name = l.Name,
            AppId = l.ProductId,
            AppName = l.Product?.Name ?? "",
            AppSlug = l.Product?.Slug ?? "",
            Status = l.Status,
            StartsAt = l.StartsAt,
            ExpiresAt = l.ExpiresAt,
            MaxDevices = l.MaxDevices,
            ActiveDevices = activeDevices,
            MaxMessagesPerDay = l.MaxMessagesPerDay,
            MaxMessagesPerMonth = l.MaxMessagesPerMonth,
            MaxInputTokensPerDay = l.MaxInputTokensPerDay,
            MaxOutputTokensPerDay = l.MaxOutputTokensPerDay,
            MaxTotalTokensPerDay = l.MaxTotalTokensPerDay,
            MaxInputTokensPerMonth = l.MaxInputTokensPerMonth,
            MaxOutputTokensPerMonth = l.MaxOutputTokensPerMonth,
            MaxTotalTokensPerMonth = l.MaxTotalTokensPerMonth,
            MaxTokensPerRequest = l.MaxTokensPerRequest
        };
        if (licenseModels != null)
        {
            dto.Models = licenseModels
                .Where(lm => lm.AiModel != null)
                .Select(lm => new LicenseModelDto
                {
                    Id = lm.AiModel!.Id,
                    ModelId = lm.AiModel.ModelId,
                    DisplayName = lm.AiModel.DisplayName
                }).ToList();
        }
        return dto;
    }

    public static DeviceDto ToDto(Device d, Guid? licenseId = null, bool revoked = false) => new()
    {
        Id = d.Id,
        DeviceName = d.DeviceName,
        DeviceIdentifier = d.DeviceIdentifier,
        Platform = d.Platform,
        LicenseId = licenseId,
        UserId = d.UserId,
        FirstSeenAt = d.FirstSeenAt,
        LastSeenAt = d.LastSeenAt,
        Revoked = revoked
    };

    public static ChatMessageDto ToDto(ChatMessage m) => new()
    {
        Id = m.Id,
        Role = m.Role,
        Content = m.Content,
        ModelId = m.ModelId,
        InputTokens = m.InputTokens,
        OutputTokens = m.OutputTokens,
        CreatedAt = m.CreatedAt
    };
}
