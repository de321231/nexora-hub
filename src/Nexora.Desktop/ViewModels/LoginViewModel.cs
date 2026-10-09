using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Desktop.Services;
using Nexora.Shared.Dtos;

namespace Nexora.Desktop.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly NexoraApiClient _api;
    private readonly SessionStore _session;

    [ObservableProperty] private string apiUrl;
    [ObservableProperty] private string email = "";
    [ObservableProperty] private string password = "";
    [ObservableProperty] private string fullName = "";
    [ObservableProperty] private bool isRegisterMode;
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string? errorMessage;
    [ObservableProperty] private string? infoMessage;

    /// <summary>Wird nach erfolgreicher Anmeldung/Registrierung gefeuert.</summary>
    public event Action<AuthResponse>? Authenticated;

    public LoginViewModel(NexoraApiClient api, SessionStore session)
    {
        _api = api;
        _session = session;
        apiUrl = session.Settings.ApiUrl;
    }

    public string SubmitButtonText => IsRegisterMode ? "Konto erstellen" : "Anmelden";
    public string ToggleModeText => IsRegisterMode ? "Zurück zur Anmeldung" : "Konto registrieren";
    public bool InputEnabled => !IsBusy;

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(InputEnabled));

    partial void OnIsRegisterModeChanged(bool value)
    {
        ErrorMessage = null;
        InfoMessage = value
            ? "Registrierung erstellt ein Benutzerkonto. Adminrechte werden nie automatisch vergeben – eine Lizenz kann anschließend über einen Schlüssel eingelöst werden."
            : null;
        OnPropertyChanged(nameof(SubmitButtonText));
        OnPropertyChanged(nameof(ToggleModeText));
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy) return;
        ErrorMessage = null;

        var url = ApiUrl?.Trim().TrimEnd('/') ?? "";
        if (!Uri.TryCreate(url.Contains("://") ? url : "http://" + url, UriKind.Absolute, out _))
        {
            ErrorMessage = "Bitte eine gültige API-Adresse angeben.";
            return;
        }
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Bitte E-Mail und Passwort eingeben.";
            return;
        }

        IsBusy = true;
        try
        {
            ApplyApiUrl(url);

            ApiResult<AuthResponse> result;
            if (IsRegisterMode)
            {
                if (Password.Length < 8)
                {
                    ErrorMessage = "Das Passwort muss mindestens 8 Zeichen lang sein.";
                    return;
                }
                result = await _api.RegisterAsync(Email.Trim(), Password, string.IsNullOrWhiteSpace(FullName) ? null : FullName.Trim());
            }
            else
            {
                result = await _api.LoginAsync(Email.Trim(), Password);
            }

            if (result.Ok && result.Value != null)
            {
                Authenticated?.Invoke(result.Value);
            }
            else
            {
                ErrorMessage = result.Error ?? "Anmeldung fehlgeschlagen.";
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TryDemoLoginAsync()
    {
        if (!string.IsNullOrWhiteSpace(ApiUrl) && ApiUrl != _session.Settings.ApiUrl)
        {
            ApplyApiUrl(ApiUrl.Trim().TrimEnd('/'));
        }
        var result = await _api.LoginAsync("admin@nexora.local", "Admin!Pass123");
        if (result.Ok && result.Value != null)
            Authenticated?.Invoke(result.Value);
        else
            ErrorMessage = result.Error ?? "Demo-Anmeldung fehlgeschlagen.";
    }

    [RelayCommand]
    private void ToggleMode()
    {
        IsRegisterMode = !IsRegisterMode;
        ErrorMessage = null;
    }

    private void ApplyApiUrl(string url)
    {
        var normalized = url.TrimEnd('/');
        _session.Settings.ApiUrl = normalized;
        _session.Settings.Save();
        _api.ApplyBaseUrl(normalized);
    }
}