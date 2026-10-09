using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Desktop.Services;
using Nexora.Shared.Dtos;

namespace Nexora.Desktop.ViewModels;

/// <summary>Gerätezeile mit Markierung des aktuellen Geräts.</summary>
public partial class DeviceRow : ObservableObject
{
    public Guid Id { get; init; }
    public string DeviceName { get; init; } = "";
    public string DeviceIdentifier { get; init; } = "";
    public string Platform { get; init; } = "";
    public DateTime LastSeenAt { get; init; }
    public bool Revoked { get; init; }
    public bool IsCurrent { get; init; }

    public string LastSeen => Revoked
        ? "deaktiviert"
        : $"zuletzt gesehen: {LastSeenAt.ToLocalTime():dd.MM.yyyy HH:mm}";

    [ObservableProperty] private bool isSelected;
}

public partial class DevicesViewModel : ObservableObject
{
    private readonly NexoraApiClient _api;
    private readonly SessionStore _session;

    public ObservableCollection<DeviceRow> Devices { get; } = new();

    [ObservableProperty] private DeviceRow? selectedDevice;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;

    public DevicesViewModel(NexoraApiClient api, SessionStore session)
    {
        _api = api;
        _session = session;
    }

    partial void OnSelectedDeviceChanged(DeviceRow? value)
    {
        foreach (var d in Devices) d.IsSelected = ReferenceEquals(d, value);
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _api.DevicesAsync();
            if (!result.Ok || result.Value == null)
            {
                StatusMessage = result.Error ?? "Geräte konnten nicht geladen werden.";
                return;
            }

            Devices.Clear();
            foreach (var d in result.Value)
            {
                Devices.Add(new DeviceRow
                {
                    Id = d.Id,
                    DeviceName = d.DeviceName,
                    DeviceIdentifier = d.DeviceIdentifier,
                    Platform = d.Platform,
                    LastSeenAt = d.LastSeenAt,
                    Revoked = d.Revoked,
                    IsCurrent = d.DeviceIdentifier == _session.DeviceIdentifier
                });
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Aktiviert dieses Gerät für die zuletzt aktive Lizenz (falls nötig).</summary>
    [RelayCommand]
    private async Task ActivateAsync()
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            var result = await _api.ActivateDeviceAsync(null);
            if (!result.Ok || result.Value == null)
                StatusMessage = result.Error ?? "Aktivierung fehlgeschlagen.";
            else
                StatusMessage = "Dieses Gerät wurde aktiviert.";
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RevokeAsync()
    {
        if (SelectedDevice == null) return;
        IsBusy = true;
        try
        {
            var result = await _api.RevokeDeviceAsync(SelectedDevice.Id);
            if (!result.Ok)
                StatusMessage = result.Error ?? "Deaktivierung fehlgeschlagen.";
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }
}