using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Desktop.Services;

namespace Nexora.Desktop.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SessionStore _session;

    [ObservableProperty] private string apiUrl;
    [ObservableProperty] private string? userEmail;
    [ObservableProperty] private string? userName;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool isBusy;

    private readonly NexoraApiClient _api;

    public string AppVersion =>
        (System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3))
        ?? "1.0.0";

    public string ApiDescription =>
        "Die NexoraAI-Desktop-App kommuniziert ausschließlich mit der Nexora-API. " +
        "Der LiteLLM-Schlüssel verbleibt serverseitig und wird nie an diese App übertragen.";

    /// <summary>Wird bei Abmeldung gefeuert.</summary>
    public event Action? LoggedOut;

    public SettingsViewModel(SessionStore session, NexoraApiClient api)
    {
        _session = session;
        _api = api;
        apiUrl = session.BaseUrl;
        userEmail = session.User?.Email;
        userName = session.User?.FullName ?? session.User?.Email;
    }

    [RelayCommand]
    private void Save()
    {
        var url = ApiUrl?.Trim().TrimEnd('/') ?? "";
        if (!Uri.TryCreate(url.Contains("://") ? url : "http://" + url, UriKind.Absolute, out _))
        {
            StatusMessage = "Die API-Adresse ist ungültig.";
            return;
        }
        _session.Settings.ApiUrl = url;
        _session.Settings.Save();
        _api.ApplyBaseUrl(url);
        StatusMessage = "Einstellungen gespeichert.";
        _session.UpdateBaseUrl(url);
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        IsBusy = true;
        try
        {
            StatusMessage = null;
            LoggedOut?.Invoke();
        }
        finally
        {
            IsBusy = false;
        }
    }
}