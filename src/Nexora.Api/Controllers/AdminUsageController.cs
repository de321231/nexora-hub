using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Shared.Dtos;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/usage")]
[Authorize(Policy = "Admin")]
public class AdminUsageController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminUsageController(AppDbContext db) => _db = db;

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var (fromUtc, toUtc) = Normalize(from, to);
        var events = await _db.UsageEvents.AsNoTracking()
            .Where(e => e.CreatedAtUtc >= fromUtc && e.CreatedAtUtc < toUtc)
            .ToListAsync();

        var byModel = events
            .GroupBy(e => e.ModelId ?? "(unbekannt)")
            .Select(g => new UsageByModelDto
            {
                ModelId = g.Key,
                Requests = g.LongCount(),
                TotalTokens = g.Sum(x => (long)x.TotalTokens),
                CostUsd = SumNullable(g.Select(x => x.CostUsd))
            })
            .OrderByDescending(x => x.TotalTokens)
            .ToList();

        var byDay = events
            .GroupBy(e => DateTime.SpecifyKind(e.CreatedAtUtc.Date, DateTimeKind.Utc))
            .Select(g => new UsageByDayDto
            {
                DayUtc = g.Key,
                Requests = g.LongCount(),
                TotalTokens = g.Sum(x => (long)x.TotalTokens)
            })
            .OrderBy(x => x.DayUtc)
            .ToList();

        var emails = await _db.Users.AsNoTracking().ToDictionaryAsync(u => u.Id, u => u.Email);
        var byUser = events
            .Where(e => e.UserId != null)
            .GroupBy(e => e.UserId!)
            .Select(g => new UsageByUserDto
            {
                UserId = g.Key,
                Email = emails.TryGetValue(g.Key, out var mail) ? mail ?? "" : "",
                Requests = g.LongCount(),
                TotalTokens = g.Sum(x => (long)x.TotalTokens)
            })
            .OrderByDescending(x => x.TotalTokens)
            .ToList();

        return Ok(new UsageSummaryDto
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalRequests = events.LongCount(),
            TotalInputTokens = events.Sum(e => (long)e.InputTokens),
            TotalOutputTokens = events.Sum(e => (long)e.OutputTokens),
            TotalTokens = events.Sum(e => (long)e.TotalTokens),
            TotalCostUsd = SumNullable(events.Select(e => e.CostUsd)),
            ByModel = byModel,
            ByDay = byDay,
            ByUser = byUser
        });
    }

    [HttpGet("events")]
    public async Task<IActionResult> Events([FromQuery] int take = 200, [FromQuery] string? userId = null,
        [FromQuery] Guid? licenseId = null, [FromQuery] bool? success = null)
    {
        take = Math.Clamp(take, 1, 1000);
        var query = _db.UsageEvents.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(e => e.UserId == userId);
        if (licenseId.HasValue) query = query.Where(e => e.LicenseId == licenseId.Value);
        if (success.HasValue) query = query.Where(e => e.Success == success.Value);

        var events = await query.OrderByDescending(e => e.CreatedAtUtc).Take(take).ToListAsync();
        var userIds = events.Where(e => e.UserId != null).Select(e => e.UserId!).Distinct().ToList();
        var emails = await _db.Users.Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Email);

        return Ok(events.Select(e => new AdminUsageEventDto
        {
            Id = e.Id,
            UserId = e.UserId,
            UserEmail = e.UserId != null && emails.TryGetValue(e.UserId, out var mail) ? mail : null,
            LicenseId = e.LicenseId,
            ModelId = e.ModelId,
            InputTokens = e.InputTokens,
            OutputTokens = e.OutputTokens,
            TotalTokens = e.TotalTokens,
            TokensEstimated = e.TokensEstimated,
            Success = e.Success,
            ErrorCode = e.ErrorCode,
            LatencyMs = e.LatencyMs,
            CostUsd = e.CostUsd,
            CreatedAtUtc = e.CreatedAtUtc
        }).ToList());
    }

    private static (DateTime FromUtc, DateTime ToUtc) Normalize(DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var toUtc = to.HasValue ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc) : now.AddDays(1);
        var fromUtc = from.HasValue
            ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc)
            : new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (toUtc <= fromUtc) toUtc = fromUtc.AddDays(1);
        return (fromUtc, toUtc);
    }

    private static decimal? SumNullable(IEnumerable<decimal?> values)
    {
        decimal sum = 0;
        bool any = false;
        foreach (var v in values)
        {
            if (v.HasValue) { sum += v.Value; any = true; }
        }
        return any ? sum : null;
    }
}
