using Microsoft.EntityFrameworkCore;
using Nexora.Api.Data;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Api.Services;

public record DeviceActivationResult(bool Ok, string? Error, DeviceDto? Device);

public interface IDeviceService
{
    Task<DeviceActivationResult> ActivateAsync(string userId, License license, string deviceIdentifier,
        string? deviceName, string? platform, CancellationToken ct);
    Task<bool> RevokeAsync(string userId, Guid deviceId, CancellationToken ct);
    Task<int> CountActiveAsync(Guid licenseId, CancellationToken ct);
}

public class DeviceService : IDeviceService
{
    private readonly AppDbContext _db;
    public DeviceService(AppDbContext db) => _db = db;

    public async Task<int> CountActiveAsync(Guid licenseId, CancellationToken ct)
        => await _db.Activations.CountAsync(a => a.LicenseId == licenseId && a.RevokedAt == null, ct);

    public async Task<DeviceActivationResult> ActivateAsync(string userId, License license, string deviceIdentifier,
        string? deviceName, string? platform, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(deviceIdentifier))
            return new DeviceActivationResult(false, "Geräte-ID fehlt.", null);

        deviceIdentifier = deviceIdentifier.Trim();
        var now = DateTime.UtcNow;

        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceIdentifier == deviceIdentifier, ct);
        if (device == null)
        {
            device = new Device
            {
                DeviceIdentifier = deviceIdentifier,
                DeviceName = string.IsNullOrWhiteSpace(deviceName) ? "Unbenanntes Gerät" : deviceName!.Trim(),
                Platform = string.IsNullOrWhiteSpace(platform) ? "Windows" : platform!.Trim(),
                UserId = userId,
                FirstSeenAt = now,
                LastSeenAt = now
            };
            _db.Devices.Add(device);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            device.LastSeenAt = now;
            device.UserId = userId;
            if (!string.IsNullOrWhiteSpace(deviceName)) device.DeviceName = deviceName!.Trim();
            if (!string.IsNullOrWhiteSpace(platform)) device.Platform = platform!.Trim();
            await _db.SaveChangesAsync(ct);
        }

        var existing = await _db.Activations.FirstOrDefaultAsync(a =>
            a.LicenseId == license.Id && a.DeviceId == device.Id, ct);

        if (existing != null)
        {
            if (existing.RevokedAt == null)
                return new DeviceActivationResult(true, null, DtoMapper.ToDto(device, license.Id, false));
            // Reaktivierung prüfen.
            var activeCountReact = await CountActiveAsync(license.Id, ct);
            if (activeCountReact >= license.MaxDevices)
                return new DeviceActivationResult(false, $"Maximale Geräteanzahl erreicht ({license.MaxDevices}).", null);
            existing.RevokedAt = null;
            existing.ActivatedAt = now;
            existing.LastValidatedAt = now;
            await _db.SaveChangesAsync(ct);
            return new DeviceActivationResult(true, null, DtoMapper.ToDto(device, license.Id, false));
        }

        var activeCount = await CountActiveAsync(license.Id, ct);
        if (activeCount >= license.MaxDevices)
            return new DeviceActivationResult(false, $"Maximale Geräteanzahl erreicht ({license.MaxDevices}).", null);

        _db.Activations.Add(new Activation
        {
            LicenseId = license.Id,
            DeviceId = device.Id,
            ActivatedAt = now,
            LastValidatedAt = now
        });
        await _db.SaveChangesAsync(ct);
        return new DeviceActivationResult(true, null, DtoMapper.ToDto(device, license.Id, false));
    }

    public async Task<bool> RevokeAsync(string userId, Guid deviceId, CancellationToken ct)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId && d.UserId == userId, ct);
        if (device == null) return false;
        var activations = await _db.Activations.Where(a => a.DeviceId == deviceId && a.RevokedAt == null).ToListAsync(ct);
        foreach (var a in activations) a.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
