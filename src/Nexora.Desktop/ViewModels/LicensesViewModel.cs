using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Desktop.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Desktop.ViewModels;

/// <summary>Lizenzzeile mit eingeblendeten Quota-Infos.</summary>
public partial class LicenseRow : ObservableObject
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string AppName { get; init; } = "";
    public string AppSlug { get; init; } = "";
    public LicenseStatus Status { get; init; }
    public DateTime? ExpiresAt { get; init; }
    public int MaxDevices { get; init; }
    public int ActiveDevices { get; init; }
    public string? MaxMessagesPerDay { get; init; }
    public string? MaxTokensPerRequest { get; init; }
    public string ModelsSummary { get; init; } = "";

    public string ExpiryText => ExpiresAt.HasValue
        ? $"gültig bis {ExpiresAt.Value.ToLocalTime():dd.MM.yyyy}"
        : "unbefristet";

    [ObservableProperty] private bool isSelected;
}

public partial class LicensesViewModel : ObservableObject
{
    private readonly NexoraApiClient _api;

    public ObservableCollection<LicenseRow> Licenses { get; } = new();
    public ObservableCollection<UsageEventDto> Usage { get; } = new();

    [ObservableProperty] private LicenseRow? selectedLicense;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private string? quotaText;
    [ObservableProperty] private string? quotaDetail;
    [ObservableProperty] private string keyInput = "";
    [ObservableProperty] private bool isRedeeming;
    [ObservableProperty] private string? redeemMessage;
    [ObservableProperty] private string? redeemedKeyInfo;

    public bool CanRedeem => !IsRedeeming;

    partial void OnIsRedeemingChanged(bool value) => OnPropertyChanged(nameof(CanRedeem));

    public LicensesViewModel(NexoraApiClient api, SessionStore session)
    {
        _api = api;
    }

    partial void OnSelectedLicenseChanged(LicenseRow? value)
    {
        foreach (var l in Licenses) l.IsSelected = ReferenceEquals(l, value);
        if (value != null)
            _ = LoadDetailAsync(value);
    }

    public async Task RefreshAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _api.MyLicensesAsync();
            if (!result.Ok || result.Value == null)
            {
                StatusMessage = result.Error ?? "Lizenzen konnten nicht geladen werden.";
                return;
            }

            Licenses.Clear();
            foreach (var l in result.Value)
            {
                Licenses.Add(new LicenseRow
                {
                    Id = l.Id,
                    Name = string.IsNullOrWhiteSpace(l.Name) ? l.AppName : l.Name!,
                    AppName = l.AppName,
                    AppSlug = l.AppSlug,
                    Status = l.Status,
                    ExpiresAt = l.ExpiresAt,
                    MaxDevices = l.MaxDevices,
                    ActiveDevices = l.ActiveDevices,
                    MaxMessagesPerDay = l.MaxMessagesPerDay?.ToString(),
                    MaxTokensPerRequest = l.MaxTokensPerRequest?.ToString(),
                    ModelsSummary = string.Join(", ", l.Models.Select(m => string.IsNullOrWhiteSpace(m.DisplayName) ? m.ModelId : m.DisplayName))
                });
            }

            if (Licenses.Count > 0)
                SelectedLicense = Licenses.FirstOrDefault(l => l.Status == LicenseStatus.Active) ?? Licenses[0];
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadDetailAsync(LicenseRow row)
    {
        QuotaText = "Kontingent wird geladen …";
        Usage.Clear();
        try
        {
            var quota = await _api.QuotaAsync(row.Id);
            if (quota.Ok && quota.Value != null)
            {
                QuotaText =
                    $"Nachrichten: {quota.Value.MessagesToday.Used}" + (quota.Value.MessagesToday.Limit.HasValue ? $"/{quota.Value.MessagesToday.Limit}" : "") +
                    $" heute · {quota.Value.MessagesThisMonth.Used}" + (quota.Value.MessagesThisMonth.Limit.HasValue ? $"/{quota.Value.MessagesThisMonth.Limit}" : "") + " diesen Monat";
                QuotaDetail =
                    $"Tokens heute: {quota.Value.TotalTokensToday.Used}" + (quota.Value.TotalTokensToday.Limit.HasValue ? $"/{quota.Value.TotalTokensToday.Limit}" : "") +
                    $" · Monat: {quota.Value.TotalTokensThisMonth.Used}" + (quota.Value.TotalTokensThisMonth.Limit.HasValue ? $"/{quota.Value.TotalTokensThisMonth.Limit}" : "");
            }

            var usage = await _api.UsageAsync(row.Id, 25);
            if (usage.Ok && usage.Value != null)
            {
                Usage.Clear();
                foreach (var u in usage.Value) Usage.Add(u);
            }
        }
        catch (Exception ex)
        {
            QuotaText = "Kontingent konnte nicht geladen werden.";
            QuotaDetail = ex.Message;
        }
    }

    [RelayCommand]
    private async Task RedeemAsync()
    {
        var key = KeyInput?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(key)) { RedeemMessage = "Bitte einen Lizenzschlüssel eingeben."; return; }

        IsRedeeming = true;
        RedeemMessage = null;
        RedeemedKeyInfo = null;
        try
        {
            var result = await _api.RedeemKeyAsync(key);
            if (result.Ok)
            {
                RedeemMessage = "✅ Schlüssel erfolgreich eingelöst.";
                KeyInput = "";
                await RefreshAsync();
            }
            else
            {
                RedeemMessage = "❌ " + (result.Error ?? "Einlösung fehlgeschlagen.");
            }
        }
        finally
        {
            IsRedeeming = false;
        }
    }
}