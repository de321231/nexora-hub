using System.ComponentModel.DataAnnotations;
using Nexora.Shared.Models;

namespace Nexora.Shared.Dtos;

// ---------------------------------------------------------------------------
// Authentifizierung
// ---------------------------------------------------------------------------

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)] public string Email { get; set; } = "";
    [Required, MinLength(8), MaxLength(128)] public string Password { get; set; } = "";
    [MaxLength(200)] public string? FullName { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = "";
    [Required] public string Password { get; set; } = "";
    [MaxLength(128)] public string? DeviceIdentifier { get; set; }
    [MaxLength(200)] public string? DeviceName { get; set; }
}

public class RefreshRequest
{
    [Required] public string RefreshToken { get; set; } = "";
    [MaxLength(128)] public string? DeviceIdentifier { get; set; }
}

public class ChangePasswordRequest
{
    [Required] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(8), MaxLength(128)] public string NewPassword { get; set; } = "";
}

public class UserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public bool IsActive { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
    public List<string> Roles { get; set; } = new();
    public int ActiveLicenses { get; set; }
}

public class AuthResponse
{
    public string AccessToken { get; set; } = "";
    public string RefreshToken { get; set; } = "";
    public DateTime AccessTokenExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Lizenzen (Benutzersicht)
// ---------------------------------------------------------------------------

public class RedeemKeyRequest
{
    [Required, MaxLength(200)] public string Key { get; set; } = "";
    [MaxLength(128)] public string? DeviceIdentifier { get; set; }
    [MaxLength(200)] public string? DeviceName { get; set; }
}

public class LicenseModelDto
{
    public Guid Id { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
}

public class LicenseDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public Guid AppId { get; set; }
    public string AppName { get; set; } = "";
    public string AppSlug { get; set; } = "";
    public LicenseStatus Status { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int MaxDevices { get; set; }
    public int ActiveDevices { get; set; }
    public int? MaxMessagesPerDay { get; set; }
    public int? MaxMessagesPerMonth { get; set; }
    public long? MaxInputTokensPerDay { get; set; }
    public long? MaxOutputTokensPerDay { get; set; }
    public long? MaxTotalTokensPerDay { get; set; }
    public long? MaxInputTokensPerMonth { get; set; }
    public long? MaxOutputTokensPerMonth { get; set; }
    public long? MaxTotalTokensPerMonth { get; set; }
    public long? MaxTokensPerRequest { get; set; }
    public List<LicenseModelDto> Models { get; set; } = new();
}

public class QuotaCounterDto
{
    public long? Limit { get; set; }
    public long Used { get; set; }
    public long? Remaining { get; set; }
}

public class QuotaStatusDto
{
    public Guid LicenseId { get; set; }
    public string AppName { get; set; } = "";
    public LicenseStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public QuotaCounterDto MessagesToday { get; set; } = new();
    public QuotaCounterDto MessagesThisMonth { get; set; } = new();
    public QuotaCounterDto InputTokensToday { get; set; } = new();
    public QuotaCounterDto OutputTokensToday { get; set; } = new();
    public QuotaCounterDto TotalTokensToday { get; set; } = new();
    public QuotaCounterDto InputTokensThisMonth { get; set; } = new();
    public QuotaCounterDto OutputTokensThisMonth { get; set; } = new();
    public QuotaCounterDto TotalTokensThisMonth { get; set; } = new();
    public List<LicenseModelDto> AllowedModels { get; set; } = new();
}

public class UsageEventDto
{
    public Guid Id { get; set; }
    public string? ModelId { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public bool TokensEstimated { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public int? LatencyMs { get; set; }
    public decimal? CostUsd { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

// ---------------------------------------------------------------------------
// Geräte
// ---------------------------------------------------------------------------

public class DeviceDto
{
    public Guid Id { get; set; }
    public string DeviceName { get; set; } = "";
    public string DeviceIdentifier { get; set; } = "";
    public string Platform { get; set; } = "";
    public Guid? LicenseId { get; set; }
    public string? UserId { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool Revoked { get; set; }
}

public class ActivateDeviceRequest
{
    [Required, MaxLength(128)] public string DeviceIdentifier { get; set; } = "";
    [MaxLength(200)] public string? DeviceName { get; set; }
    [MaxLength(40)] public string? Platform { get; set; }
    public Guid? LicenseId { get; set; }
}

/// <summary>Geräteansicht für Admins (enthält Benutzer- und Lizenzkontext).</summary>
public class AdminDeviceDto : DeviceDto
{
    public string? UserEmail { get; set; }
    public string? LicenseName { get; set; }
    public string? AppName { get; set; }
    public bool DeviceActive { get; set; }
    public DateTime ActivatedAt { get; set; }
    public DateTime? LastValidatedAt { get; set; }
}

// ---------------------------------------------------------------------------
// Chat
// ---------------------------------------------------------------------------

public class ConversationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MessageCount { get; set; }
}

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public ChatRole Role { get; set; }
    public string Content { get; set; } = "";
    public string? ModelId { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendMessageRequest
{
    public Guid? ConversationId { get; set; }
    [Required, MaxLength(200)] public string ModelId { get; set; } = "";
    [Required, MaxLength(32000)] public string Message { get; set; } = "";
    /// <summary>Optionales System-Prompt.</summary>
    [MaxLength(8000)] public string? SystemPrompt { get; set; }
    public int? MaxOutputTokens { get; set; }
}

public class ChatCompletionResponse
{
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }
    public string Content { get; set; } = "";
    public string ModelId { get; set; } = "";
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public bool TokensEstimated { get; set; }
    public decimal? CostUsd { get; set; }
    public QuotaStatusDto Quota { get; set; } = new();
}

// ---------------------------------------------------------------------------
// Admin: Apps / Produkte
// ---------------------------------------------------------------------------

public class AppDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public AppType Kind { get; set; }
    public AppStatus Status { get; set; }
    public bool LicenseRequired { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAppRequest
{
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(80)] public string? Slug { get; set; }
    [MaxLength(500)] public string? Description { get; set; }
    public AppType Kind { get; set; } = AppType.WindowsProgram;
    public AppStatus Status { get; set; } = AppStatus.Draft;
    public bool LicenseRequired { get; set; } = true;
}

public class UpdateAppRequest
{
    [Required, MaxLength(120)] public string Name { get; set; } = "";
    [MaxLength(500)] public string? Description { get; set; }
    public AppType Kind { get; set; }
    public AppStatus Status { get; set; }
    public bool LicenseRequired { get; set; }
    public bool IsActive { get; set; }
}

// ---------------------------------------------------------------------------
// Admin: KI-Modelle
// ---------------------------------------------------------------------------

public class AiModelDto
{
    public Guid Id { get; set; }
    public string ModelId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Provider { get; set; } = "";
    public bool IsActive { get; set; }
    public int? MaxContextTokens { get; set; }
    public decimal? InputCostPer1M { get; set; }
    public decimal? OutputCostPer1M { get; set; }
}

public class CreateModelRequest
{
    [Required, MaxLength(200)] public string ModelId { get; set; } = "";
    [MaxLength(200)] public string? DisplayName { get; set; }
    [MaxLength(100)] public string? Provider { get; set; }
    public bool IsActive { get; set; } = true;
    public int? MaxContextTokens { get; set; }
    public decimal? InputCostPer1M { get; set; }
    public decimal? OutputCostPer1M { get; set; }
}

public class UpdateModelRequest
{
    [Required, MaxLength(200)] public string ModelId { get; set; } = "";
    [MaxLength(200)] public string? DisplayName { get; set; }
    [MaxLength(100)] public string? Provider { get; set; }
    public bool IsActive { get; set; } = true;
    public int? MaxContextTokens { get; set; }
    public decimal? InputCostPer1M { get; set; }
    public decimal? OutputCostPer1M { get; set; }
}

// ---------------------------------------------------------------------------
// Admin: Lizenzen
// ---------------------------------------------------------------------------

public class AdminLicenseDto : LicenseDto
{
    public int KeyCount { get; set; }
    public int AssignedUserCount { get; set; }
    public List<string> AssignedUserEmails { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class CreateLicenseRequest
{
    [Required] public Guid AppId { get; set; }
    [MaxLength(200)] public string? Name { get; set; }
    [MaxLength(20)] public string? KeyPrefix { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int MaxDevices { get; set; } = 1;
    public int? MaxMessagesPerDay { get; set; }
    public int? MaxMessagesPerMonth { get; set; }
    public long? MaxInputTokensPerDay { get; set; }
    public long? MaxOutputTokensPerDay { get; set; }
    public long? MaxTotalTokensPerDay { get; set; }
    public long? MaxInputTokensPerMonth { get; set; }
    public long? MaxOutputTokensPerMonth { get; set; }
    public long? MaxTotalTokensPerMonth { get; set; }
    public long? MaxTokensPerRequest { get; set; }
    public List<Guid> ModelIds { get; set; } = new();
    [MaxLength(2000)] public string? AdminNotes { get; set; }
}

public class UpdateLicenseRequest
{
    [MaxLength(200)] public string? Name { get; set; }
    public LicenseStatus Status { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int MaxDevices { get; set; } = 1;
    public int? MaxMessagesPerDay { get; set; }
    public int? MaxMessagesPerMonth { get; set; }
    public long? MaxInputTokensPerDay { get; set; }
    public long? MaxOutputTokensPerDay { get; set; }
    public long? MaxTotalTokensPerDay { get; set; }
    public long? MaxInputTokensPerMonth { get; set; }
    public long? MaxOutputTokensPerMonth { get; set; }
    public long? MaxTotalTokensPerMonth { get; set; }
    public long? MaxTokensPerRequest { get; set; }
    public List<Guid> ModelIds { get; set; } = new();
    [MaxLength(2000)] public string? AdminNotes { get; set; }
}

public class AssignLicenseRequest
{
    [Required] public string UserId { get; set; } = "";
    [Required] public Guid LicenseId { get; set; }
}

// ---------------------------------------------------------------------------
// Admin: Lizenzschlüssel
// ---------------------------------------------------------------------------

public class CreateLicenseKeyRequest
{
    [Required] public Guid LicenseId { get; set; }
    public int Count { get; set; } = 1;
    public DateTime? ExpiresAt { get; set; }
    public int MaxUses { get; set; } = 1;
    [MaxLength(200)] public string? Note { get; set; }
}

public class LicenseKeyDto
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public string? LicenseName { get; set; }
    public string KeyPrefix { get; set; } = "";
    public LicenseKeyStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int MaxUses { get; set; }
    public int UsedCount { get; set; }
    public string? RedeemedByUserId { get; set; }
    public string? RedeemedByEmail { get; set; }
    public DateTime? RedeemedAt { get; set; }
}

/// <summary>Antwort bei der Schlüsselerstellung – enthält den Klartext genau einmal.</summary>
public class CreatedLicenseKeyDto
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public string Key { get; set; } = "";
    public string KeyPrefix { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
}

// ---------------------------------------------------------------------------
// Admin: Statistik
// ---------------------------------------------------------------------------

public class DashboardStatsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalApps { get; set; }
    public int ActiveLicenses { get; set; }
    public int ExpiredLicenses { get; set; }
    public int SuspendedLicenses { get; set; }
    public int RevokedLicenses { get; set; }
    public int PendingLicenses { get; set; }
    public int TotalLicenseKeys { get; set; }
    public int RedeemedLicenseKeys { get; set; }
    public int ActiveDevices { get; set; }
    public long RequestsToday { get; set; }
    public long RequestsThisMonth { get; set; }
    public long TokensToday { get; set; }
    public long TokensThisMonth { get; set; }
    public decimal? CostThisMonthUsd { get; set; }
    public List<RecentUserDto> RecentlyActiveUsers { get; set; } = new();
}

public class RecentUserDto
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? FullName { get; set; }
    public DateTime? LastLoginAtUtc { get; set; }
}

public class UsageSummaryDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public long TotalRequests { get; set; }
    public long TotalInputTokens { get; set; }
    public long TotalOutputTokens { get; set; }
    public long TotalTokens { get; set; }
    public decimal? TotalCostUsd { get; set; }
    public List<UsageByModelDto> ByModel { get; set; } = new();
    public List<UsageByDayDto> ByDay { get; set; } = new();
    public List<UsageByUserDto> ByUser { get; set; } = new();
}

public class UsageByModelDto
{
    public string ModelId { get; set; } = "";
    public long Requests { get; set; }
    public long TotalTokens { get; set; }
    public decimal? CostUsd { get; set; }
}

public class UsageByDayDto
{
    public DateTime DayUtc { get; set; }
    public long Requests { get; set; }
    public long TotalTokens { get; set; }
}

public class UsageByUserDto
{
    public string UserId { get; set; } = "";
    public string Email { get; set; } = "";
    public long Requests { get; set; }
    public long TotalTokens { get; set; }
}

public class AdminUsageEventDto : UsageEventDto
{
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public Guid? LicenseId { get; set; }
}

// ---------------------------------------------------------------------------
// Admin: Benutzer
// ---------------------------------------------------------------------------

public class UpdateUserRequest
{
    [MaxLength(200)] public string? FullName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class SetUserRolesRequest
{
    public List<string> Roles { get; set; } = new();
}

public class UserLicenseRowDto
{
    public Guid Id { get; set; }
    public Guid LicenseId { get; set; }
    public string? Name { get; set; }
    public string? AppName { get; set; }
    public LicenseStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool Revoked { get; set; }
}

public class UserDetailDto
{
    public UserDto User { get; set; } = new();
    public List<UserLicenseRowDto> Licenses { get; set; } = new();
}

public class LicenseUserLinkDto
{
    public string Id { get; set; } = "";
    public string? Email { get; set; }
    public string? FullName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public bool Revoked { get; set; }
}

public class LicenseDetailDto
{
    public AdminLicenseDto License { get; set; } = new();
    public List<LicenseKeyDto> Keys { get; set; } = new();
    public List<LicenseUserLinkDto> Users { get; set; } = new();
}
