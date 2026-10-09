using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Api.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Controllers;

[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Policy = "Admin")]
public class AdminDashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminDashboardController(AppDbContext db) => _db = db;

    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var now = DateTime.UtcNow;
        var dayStart = now.Date;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var licenses = await _db.Licenses.AsNoTracking().ToListAsync();
        int CountByStatus(LicenseStatus s) => licenses.Count(l => LicenseEvaluator.ComputeStatus(l, now) == s);

        var todayEvents = await _db.UsageEvents.AsNoTracking()
            .Where(e => e.CreatedAtUtc >= dayStart).ToListAsync();
        var monthEvents = await _db.UsageEvents.AsNoTracking()
            .Where(e => e.CreatedAtUtc >= monthStart).ToListAsync();

        decimal? cost = null;
        decimal costSum = 0;
        bool anyCost = false;
        foreach (var e in monthEvents)
        {
            if (e.CostUsd.HasValue) { costSum += e.CostUsd.Value; anyCost = true; }
        }
        if (anyCost) cost = costSum;

        var recentUsers = await _db.Users.AsNoTracking()
            .Where(u => u.LastLoginAtUtc != null)
            .OrderByDescending(u => u.LastLoginAtUtc).Take(8)
            .Select(u => new RecentUserDto
            {
                Id = u.Id,
                Email = u.Email ?? "",
                FullName = u.FullName,
                LastLoginAtUtc = u.LastLoginAtUtc
            }).ToListAsync();

        return Ok(new DashboardStatsDto
        {
            TotalUsers = await _db.Users.CountAsync(),
            ActiveUsers = await _db.Users.CountAsync(u => u.IsActive && !u.IsLocked),
            TotalApps = await _db.Products.CountAsync(),
            ActiveLicenses = CountByStatus(LicenseStatus.Active),
            ExpiredLicenses = CountByStatus(LicenseStatus.Expired),
            SuspendedLicenses = CountByStatus(LicenseStatus.Suspended),
            RevokedLicenses = CountByStatus(LicenseStatus.Revoked),
            PendingLicenses = CountByStatus(LicenseStatus.Pending),
            TotalLicenseKeys = await _db.LicenseKeys.CountAsync(),
            RedeemedLicenseKeys = await _db.LicenseKeys.CountAsync(k => k.Status == LicenseKeyStatus.Redeemed),
            ActiveDevices = await _db.Activations.CountAsync(a => a.RevokedAt == null),
            RequestsToday = todayEvents.LongCount(),
            RequestsThisMonth = monthEvents.LongCount(),
            TokensToday = todayEvents.Sum(e => (long)e.TotalTokens),
            TokensThisMonth = monthEvents.Sum(e => (long)e.TotalTokens),
            CostThisMonthUsd = cost,
            RecentlyActiveUsers = recentUsers
        });
    }
}
