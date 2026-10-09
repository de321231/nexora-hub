using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Desktop.Services;
using Nexora.Shared.Dtos;

namespace Nexora.Desktop.ViewModels;

/// <summary>
/// Root-ViewModel: steuert die Navigation zwischen Login und den
/// angemeldeten Bereichen (Chat, Lizenzen, Geräte, Einstellungen).
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly SessionStore _session;
    private readonly NexoraApiClient _api;

    [ObservableProperty] private object? currentPage;
    [ObservableProperty] private bool isAuthenticated;
    [ObservableProperty] private string? userName;
    [ObservableProperty] private string? userEmail;

    public LoginViewModel Login { get; }
    public ChatViewModel Chat { get; }
    public LicensesViewModel Licenses { get; }
    public DevicesViewModel Devices { get; }
    public SettingsViewModel Settings { get; }

    public MainViewModel()
    {
        _session = new SessionStore(AppSettings.Load());
        _api = new NexoraApiClient(_session);

        Login = new LoginViewModel(_api, _session);
        Login.Authenticated += OnAuthenticated;

        Chat = new ChatViewModel(_api, _session);
        Licenses = new LicensesViewModel(_api, _session);
        Devices = new DevicesViewModel(_api, _session);
        Settings = new SettingsViewModel(_session, _api);
        Settings.LoggedOut += OnLoggedOut;

        CurrentPage = Login;
    }

    public async Task InitializeAsync()
    {
        if (!_session.HasSession)
        {
            CurrentPage = Login;
            return;
        }

        // Sitzung validieren (401 -> Refresh-Token wird intern verwendet).
        var me = await _api.MeAsync();
        if (!me.Ok)
        {
            // Definitiv ungültig (z. B. abgelaufener Refresh-Token)
            _session.Clear();
            await _api.LogoutAsync();
            CurrentPage = Login;
            return;
        }

        IsAuthenticated = true;
        ApplyUserInfo();
        NavigateToChat();
        await Task.WhenAll(Chat.InitializeAsync(), Licenses.RefreshAsync(), Devices.RefreshAsync());
    }

    private void OnAuthenticated(AuthResponse auth)
    {
        _session.ApplyAuth(auth);
        IsAuthenticated = true;
        ApplyUserInfo();
        NavigateToChat();
        // Seiten werden asynchron gefüllt; Fehler landen in den jeweiligen Statusfeldern.
        _ = Chat.InitializeAsync();
        _ = Licenses.RefreshAsync();
        _ = Devices.RefreshAsync();
    }

    private void ApplyUserInfo()
    {
        UserName = _session.User?.FullName ?? _session.User?.Email ?? "Benutzer";
        UserEmail = _session.User?.Email ?? "";
    }

    private void OnLoggedOut()
    {
        // Best-effort: Refresh-Token serverseitig widerrufen.
        _ = _api.LogoutAsync();
        _session.Clear();
        IsAuthenticated = false;
        UserName = "";
        UserEmail = "";
        CurrentPage = Login;
    }

    [RelayCommand] private void NavigateToChat() => CurrentPage = Chat;
    [RelayCommand] private void NavigateToLicenses() => CurrentPage = Licenses;
    [RelayCommand] private void NavigateToDevices() => CurrentPage = Devices;
    [RelayCommand] private void NavigateToSettings() => CurrentPage = Settings;
}