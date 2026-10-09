using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Services;

public record QuotaReservation(
    Guid LicenseId,
    string UserId,
    DateTime DayStartUtc,
    DateTime MonthStartUtc,
    int EstimatedInputTokens,
    int ReservedOutputTokens);

public record QuotaDecision(bool Allowed, string? Reason, QuotaReservation? Reservation);

public interface IQuotaService
{
    Task<License?> ResolveActiveLicenseAsync(string userId, Guid? appId, CancellationToken ct);
    Task<QuotaDecision> ReserveAsync(string userId, License license, int estimatedInputTokens, int requestedOutputTokens, CancellationToken ct);
    Task ReconcileAsync(QuotaReservation reservation, int actualInput, int actualOutput, bool success,
        string? errorCode, int? latencyMs, decimal? costUsd, string? modelId, Guid? aiModelId, Guid? productId, CancellationToken ct);
    Task<QuotaStatusDto> BuildStatusAsync(string userId, License license, CancellationToken ct);
}

public class QuotaService : IQuotaService
{
    private readonly AppDbContext _db;
    public QuotaService(AppDbContext db) => _db = db;

    public static int EstimateTokens(string? text)
        => string.IsNullOrEmpty(text) ? 0 : Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));

    public async Task<License?> ResolveActiveLicenseAsync(string userId, Guid? appId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var query = from ul in _db.UserLicenses
                    join lic in _db.Licenses on ul.LicenseId equals lic.Id
                    where ul.UserId == userId && ul.RevokedAt == null
                    select lic;

        if (appId.HasValue)
            query = query.Where(l => l.ProductId == appId.Value);

        var candidates = await query.Distinct().ToListAsync(ct);
        foreach (var lic in candidates)
        {
            if (LicenseEvaluator.RefreshStoredStatus(lic, now))
                await _db.SaveChangesAsync(ct);
        }
        return candidates.FirstOrDefault(l => LicenseEvaluator.IsUsable(l, now));
    }

    public async Task<QuotaDecision> ReserveAsync(string userId, License license, int estimatedInputTokens, int requestedOutputTokens, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var reserveOutput = requestedOutputTokens > 0
            ? requestedOutputTokens
            : (int)(license.MaxTokensPerRequest ?? 2048);
        if (reserveOutput <= 0) reserveOutput = 2048;

        var reservedTotal = (long)estimatedInputTokens + reserveOutput;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await AcquireLockAsync(userId, license.Id, ct);

        var day = await GetOrCreatePeriodAsync(userId, license.Id, UsagePeriodType.Day, dayStart, ct);
        var month = await GetOrCreatePeriodAsync(userId, license.Id, UsagePeriodType.Month, monthStart, ct);

        string? fail = null;

        // Nachrichtenlimits
        if (license.MaxMessagesPerDay is int mday && day.MessageCount + 1 > mday)
            fail = $"Tageslimit für Nachrichten erreicht ({mday}).";
        if (license.MaxMessagesPerMonth is int mmon && month.MessageCount + 1 > mmon)
            fail = $"Monatslimit für Nachrichten erreicht ({mmon}).";

        // Tokenlimits pro Anfrage
        if (license.MaxTokensPerRequest is long mreq && reservedTotal > mreq)
            fail = $"Anfrage überschreitet das Tokenlimit pro Anfrage ({mreq}).";

        // Tageslimits
        if (license.MaxInputTokensPerDay is long iday && day.InputTokens + estimatedInputTokens > iday)
            fail = $"Tageslimit für Eingabe-Tokens erreicht ({iday}).";
        if (license.MaxOutputTokensPerDay is long oday && day.OutputTokens + reserveOutput > oday)
            fail = $"Tageslimit für Ausgabe-Tokens erreicht ({oday}).";
        if (license.MaxTotalTokensPerDay is long tday && day.TotalTokens + reservedTotal > tday)
            fail = $"Tageslimit für Tokens erreicht ({tday}).";

        // Monatslimits
        if (license.MaxInputTokensPerMonth is long imon && month.InputTokens + estimatedInputTokens > imon)
            fail = $"Monatslimit für Eingabe-Tokens erreicht ({imon}).";
        if (license.MaxOutputTokensPerMonth is long omon && month.OutputTokens + reserveOutput > omon)
            fail = $"Monatslimit für Ausgabe-Tokens erreicht ({omon}).";
        if (license.MaxTotalTokensPerMonth is long tmon && month.TotalTokens + reservedTotal > tmon)
            fail = $"Monatslimit für Tokens erreicht ({tmon}).";

        if (fail != null)
        {
            await tx.RollbackAsync(ct);
            return new QuotaDecision(false, fail, null);
        }

        // Reservierung verbuchen
        day.MessageCount += 1;
        day.InputTokens += estimatedInputTokens;
        day.OutputTokens += reserveOutput;
        day.TotalTokens += reservedTotal;
        day.UpdatedAt = now;

        month.MessageCount += 1;
        month.InputTokens += estimatedInputTokens;
        month.OutputTokens += reserveOutput;
        month.TotalTokens += reservedTotal;
        month.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new QuotaDecision(true, null,
            new QuotaReservation(license.Id, userId, dayStart, monthStart, estimatedInputTokens, reserveOutput));
    }

    public async Task ReconcileAsync(QuotaReservation r, int actualInput, int actualOutput, bool success,
        string? errorCode, int? latencyMs, decimal? costUsd, string? modelId, Guid? aiModelId, Guid? productId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await AcquireLockAsync(r.UserId, r.LicenseId, ct);

        var day = await _db.UsagePeriods.FirstOrDefaultAsync(p =>
            p.UserId == r.UserId && p.LicenseId == r.LicenseId &&
            p.PeriodType == UsagePeriodType.Day && p.PeriodStartUtc == r.DayStartUtc, ct);
        var month = await _db.UsagePeriods.FirstOrDefaultAsync(p =>
            p.UserId == r.UserId && p.LicenseId == r.LicenseId &&
            p.PeriodType == UsagePeriodType.Month && p.PeriodStartUtc == r.MonthStartUtc, ct);

        if (!success)
        {
            // Reservierung vollständig zurücknehmen, Nachricht nicht zählen.
            if (day != null)
            {
                day.MessageCount = Math.Max(0, day.MessageCount - 1);
                day.InputTokens = Math.Max(0, day.InputTokens - r.EstimatedInputTokens);
                day.OutputTokens = Math.Max(0, day.OutputTokens - r.ReservedOutputTokens);
                day.TotalTokens = Math.Max(0, day.TotalTokens - ((long)r.EstimatedInputTokens + r.ReservedOutputTokens));
                day.UpdatedAt = DateTime.UtcNow;
            }
            if (month != null)
            {
                month.MessageCount = Math.Max(0, month.MessageCount - 1);
                month.InputTokens = Math.Max(0, month.InputTokens - r.EstimatedInputTokens);
                month.OutputTokens = Math.Max(0, month.OutputTokens - r.ReservedOutputTokens);
                month.TotalTokens = Math.Max(0, month.TotalTokens - ((long)r.EstimatedInputTokens + r.ReservedOutputTokens));
                month.UpdatedAt = DateTime.UtcNow;
            }
        }
        else
        {
            // Reservierung durch tatsächliche Werte ersetzen.
            var inputDelta = actualInput - r.EstimatedInputTokens;
            var outputDelta = actualOutput - r.ReservedOutputTokens;
            var totalDelta = (long)inputDelta + outputDelta;
            if (day != null)
            {
                day.InputTokens = Math.Max(0, day.InputTokens + inputDelta);
                day.OutputTokens = Math.Max(0, day.OutputTokens + outputDelta);
                day.TotalTokens = Math.Max(0, day.TotalTokens + totalDelta);
                day.UpdatedAt = DateTime.UtcNow;
            }
            if (month != null)
            {
                month.InputTokens = Math.Max(0, month.InputTokens + inputDelta);
                month.OutputTokens = Math.Max(0, month.OutputTokens + outputDelta);
                month.TotalTokens = Math.Max(0, month.TotalTokens + totalDelta);
                month.UpdatedAt = DateTime.UtcNow;
            }
        }

        _db.UsageEvents.Add(new UsageEvent
        {
            UserId = r.UserId,
            LicenseId = r.LicenseId,
            ProductId = productId,
            AiModelId = aiModelId,
            ModelId = modelId,
            InputTokens = actualInput,
            OutputTokens = actualOutput,
            TotalTokens = actualInput + actualOutput,
            TokensEstimated = false,
            Success = success,
            ErrorCode = errorCode,
            LatencyMs = latencyMs,
            CostUsd = costUsd,
            CreatedAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<QuotaStatusDto> BuildStatusAsync(string userId, License license, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var day = await _db.UsagePeriods.AsNoTracking().FirstOrDefaultAsync(p =>
            p.UserId == userId && p.LicenseId == license.Id &&
            p.PeriodType == UsagePeriodType.Day && p.PeriodStartUtc == dayStart, ct);
        var month = await _db.UsagePeriods.AsNoTracking().FirstOrDefaultAsync(p =>
            p.UserId == userId && p.LicenseId == license.Id &&
            p.PeriodType == UsagePeriodType.Month && p.PeriodStartUtc == monthStart, ct);

        var models = await GetLicenseModelsAsync(license.Id, ct);

        static QuotaCounterDto Counter(long? limit, long used) => new()
        {
            Limit = limit,
            Used = used,
            Remaining = limit.HasValue ? Math.Max(0, limit.Value - used) : null
        };

        return new QuotaStatusDto
        {
            LicenseId = license.Id,
            AppName = license.Product?.Name ?? "",
            Status = LicenseEvaluator.ComputeStatus(license, now),
            ExpiresAt = license.ExpiresAt,
            MessagesToday = Counter(license.MaxMessagesPerDay, day?.MessageCount ?? 0),
            MessagesThisMonth = Counter(license.MaxMessagesPerMonth, month?.MessageCount ?? 0),
            InputTokensToday = Counter(license.MaxInputTokensPerDay, day?.InputTokens ?? 0),
            OutputTokensToday = Counter(license.MaxOutputTokensPerDay, day?.OutputTokens ?? 0),
            TotalTokensToday = Counter(license.MaxTotalTokensPerDay, day?.TotalTokens ?? 0),
            InputTokensThisMonth = Counter(license.MaxInputTokensPerMonth, month?.InputTokens ?? 0),
            OutputTokensThisMonth = Counter(license.MaxOutputTokensPerMonth, month?.OutputTokens ?? 0),
            TotalTokensThisMonth = Counter(license.MaxTotalTokensPerMonth, month?.TotalTokens ?? 0),
            AllowedModels = models
        };
    }

    private async Task<List<LicenseModelDto>> GetLicenseModelsAsync(Guid licenseId, CancellationToken ct)
        => await (from lm in _db.LicenseModels
                  join m in _db.AiModels on lm.AiModelId equals m.Id
                  where lm.LicenseId == licenseId && m.IsActive
                  orderby m.ModelId
                  select new LicenseModelDto { Id = m.Id, ModelId = m.ModelId, DisplayName = m.DisplayName })
                 .AsNoTracking().ToListAsync(ct);

    private async Task<UsagePeriod> GetOrCreatePeriodAsync(string userId, Guid licenseId, UsagePeriodType type, DateTime start, CancellationToken ct)
    {
        var existing = await _db.UsagePeriods.FirstOrDefaultAsync(p =>
            p.UserId == userId && p.LicenseId == licenseId && p.PeriodType == type && p.PeriodStartUtc == start, ct);
        if (existing != null) return existing;
        var created = new UsagePeriod
        {
            UserId = userId,
            LicenseId = licenseId,
            PeriodType = type,
            PeriodStartUtc = start
        };
        _db.UsagePeriods.Add(created);
        return created;
    }

    private async Task AcquireLockAsync(string userId, Guid licenseId, CancellationToken ct)
    {
        if (!_db.Database.IsNpgsql()) return; // SQLite/InMemory: Sperre über Transaktion
        var key = $"{userId}:{licenseId}";
        await _db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext({0})::bigint)", new object[] { key }, ct);
    }
}
