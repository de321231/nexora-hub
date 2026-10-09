using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nexora.Desktop.Services;
using Nexora.Shared.Dtos;
using Nexora.Shared.Models;

namespace Nexora.Desktop.ViewModels;

/// <summary>Anzeigezeile im Chatverlauf.</summary>
public class ChatDisplayItem
{
    public bool IsUser { get; init; }
    public string Author { get; init; } = "";
    public string Content { get; set; } = "";
    public FlowDocument? Document { get; set; }
    public DateTime CreatedAt { get; init; }
    public string Time => CreatedAt.ToLocalTime().ToString("HH:mm");
}

/// <summary>Navigationseintrag einer Unterhaltung.</summary>
public partial class ConversationItem : ObservableObject
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    [ObservableProperty] private int messageCount;
    public DateTime UpdatedAt { get; init; }
    public string Subtitle => $"{MessageCount} Nachrichten · {UpdatedAt.ToLocalTime():dd.MM. HH:mm}";

    [ObservableProperty] private bool isActive;

    public void IncrementMessages(int delta)
    {
        MessageCount += delta;
        OnPropertyChanged(nameof(Subtitle));
    }
}

/// <summary>Modellauswahl für die Chat-Eingabe.</summary>
public record ModelOption(string ModelId, string DisplayName);

/// <summary>
/// Chat-Seite mit Unterhaltungsliste, Verlauf, Modellauswahl und Senden.
/// Die Lizenzprüfung (Status, Modellberechtigung, Kontingente) erfolgt
/// ausschließlich serverseitig über die Nexora-API.
/// </summary>
public partial class ChatViewModel : ObservableObject
{
    private readonly NexoraApiClient _api;

    public ObservableCollection<ConversationItem> Conversations { get; } = new();
    public ObservableCollection<ChatDisplayItem> Messages { get; } = new();
    public ObservableCollection<ModelOption> Models { get; } = new();

    [ObservableProperty] private ConversationItem? selectedConversation;
    [ObservableProperty] private ModelOption? selectedModel;
    [ObservableProperty] private string inputText = "";
    [ObservableProperty] private bool isSending;
    [ObservableProperty] private bool isInitializing = true;
    [ObservableProperty] private string? statusMessage;
    [ObservableProperty] private bool hasLicense;
    [ObservableProperty] private string licenseBadge = "";
    [ObservableProperty] private string quotaText = "";
    [ObservableProperty] private string? quotaDetail;

    public bool InputEnabled => !IsSending;

    partial void OnIsSendingChanged(bool value) => OnPropertyChanged(nameof(InputEnabled));

    partial void OnSelectedConversationChanged(ConversationItem? value)
    {
        foreach (var c in Conversations) c.IsActive = ReferenceEquals(c, value);
        if (value != null)
            _ = LoadConversationMessagesAsync(value.Id);
    }

    public ChatViewModel(NexoraApiClient api, SessionStore session)
    {
        _api = api;
    }

    public async Task InitializeAsync()
    {
        IsInitializing = true;
        ClearStatus();
        try
        {
            await Task.WhenAll(LoadLicensesAndQuotaAsync(), LoadConversationsAsync());
        }
        catch (Exception ex)
        {
            StatusMessage = "Initialisierung fehlgeschlagen: " + ex.Message;
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private async Task LoadLicensesAndQuotaAsync()
    {
        var licenses = await _api.MyLicensesAsync();
        if (!licenses.Ok || licenses.Value == null || licenses.Value.Count == 0)
        {
            HasLicense = false;
            LicenseBadge = "Keine Lizenz";
            Models.Clear();
            QuotaText = "Keine aktive Lizenz gefunden. Löse auf der Seite 'Lizenzen' einen Schlüssel ein.";
            return;
        }

        // Bevorzugt die nexus-visible App (Nexora AI) mit aktivem Status.
        var app = licenses.Value.FirstOrDefault(l => l.Status == LicenseStatus.Active) ?? licenses.Value[0];
        HasLicense = true;
        LicenseBadge = $"{app.AppName} · {StatusText(app.Status)}";

        var quota = await _api.QuotaAsync(app.Id);
        if (quota.Ok && quota.Value != null)
        {
            UpdateModelsFromQuota(quota.Value);
            UpdateQuotaDisplay(quota.Value);
        }
        else if (app.Models.Count > 0)
        {
            Models.Clear();
            foreach (var m in app.Models) Models.Add(new ModelOption(m.ModelId, m.DisplayName));
            SelectedModel = Models.FirstOrDefault();
            QuotaText = "Kontingentstatus konnte nicht geladen werden.";
        }
    }

    private void UpdateModelsFromQuota(QuotaStatusDto quota)
    {
        Models.Clear();
        var models = quota.AllowedModels;
        foreach (var m in models)
            Models.Add(new ModelOption(m.ModelId, string.IsNullOrWhiteSpace(m.DisplayName) ? m.ModelId : m.DisplayName));
        SelectedModel = Models.FirstOrDefault();
    }

    private void UpdateQuotaDisplay(QuotaStatusDto q)
    {
        var msg = FormatCounter(q.MessagesToday, "Nachrichten heute");
        var msgsMonth = FormatCounter(q.MessagesThisMonth, "Nachrichten diesen Monat");
        var tok = FormatCounter(q.TotalTokensToday, "Tokens heute");
        QuotaText = $"Lizenz: {q.AppName} · Status: {StatusText(q.Status)} · {msg}";
        QuotaDetail = $"{msgsMonth} · {tok}" + (q.ExpiresAt.HasValue ? $" · gültig bis {q.ExpiresAt.Value.ToLocalTime():dd.MM.yyyy}" : "");
        if (q.Status != LicenseStatus.Active)
            StatusMessage = "Achtung: Die Lizenz ist " + StatusText(q.Status).ToLower() + ".";
    }

    private static string FormatCounter(QuotaCounterDto c, string label)
    {
        var used = c.Used;
        var limit = c.Limit;
        return limit.HasValue ? $"{used}/{limit} {label}" : $"{used} {label}";
    }

    private static string StatusText(LicenseStatus s) => s switch
    {
        LicenseStatus.Active => "aktiv",
        LicenseStatus.Pending => "ausstehend",
        LicenseStatus.Expired => "abgelaufen",
        LicenseStatus.Suspended => "gesperrt",
        LicenseStatus.Revoked => "widerrufen",
        _ => s.ToString().ToLower()
    };

    private async Task LoadConversationsAsync()
    {
        var result = await _api.ConversationsAsync();
        if (!result.Ok || result.Value == null)
        {
            StatusMessage = result.Error ?? "Unterhaltungen konnten nicht geladen werden.";
            return;
        }

        Conversations.Clear();
        foreach (var c in result.Value)
            Conversations.Add(new ConversationItem
            {
                Id = c.Id, Title = c.Title, MessageCount = c.MessageCount, UpdatedAt = c.UpdatedAt
            });

        if (Conversations.Count > 0)
            SelectedConversation = Conversations[0];
    }

    private async Task LoadConversationMessagesAsync(Guid id)
    {
        IsInitializing = true;
        try
        {
            var result = await _api.ConversationMessagesAsync(id);
            if (!result.Ok || result.Value == null) return;

            Messages.Clear();
            foreach (var m in result.Value)
                Messages.Add(ToDisplayItem(m));
            ScrollRequested?.Invoke();
        }
        finally
        {
            IsInitializing = false;
        }
    }

    private ChatDisplayItem ToDisplayItem(ChatMessageDto m)
    {
        if (m.Role == ChatRole.User)
        {
            return new ChatDisplayItem { IsUser = true, Author = "Du", Content = m.Content, CreatedAt = m.CreatedAt };
        }
        return new ChatDisplayItem
        {
            IsUser = false,
            Author = m.Role == ChatRole.Assistant ? "Nexora AI" : "System",
            Content = m.Content,
            Document = MarkdownRenderer.ToFlowDocument(m.Content),
            CreatedAt = m.CreatedAt
        };
    }

    /// <summary>Feuert nach neuen Nachrichten, damit die View nach unten scrollt.</summary>
    public event Action? ScrollRequested;

    [RelayCommand]
    private void NewConversation()
    {
        SelectedConversation = null;
        foreach (var c in Conversations) c.IsActive = false;
        Messages.Clear();
        QuotaText = "Neue Unterhaltung";
        StatusMessage = null;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (IsSending) return;
        var text = InputText?.Trim() ?? "";
        if (text.Length == 0) return;
        if (!HasLicense) { StatusMessage = "Keine aktive Lizenz – bitte zuerst einen Schlüssel einlösen."; return; }
        if (SelectedModel == null) { StatusMessage = "Kein Modell für diese Lizenz freigeschaltet."; return; }

        IsSending = true;
        ClearStatus();
        var isNew = SelectedConversation == null;
        if (isNew) { SelectedConversation = null; foreach (var c in Conversations) c.IsActive = false; }
        Messages.Add(new ChatDisplayItem { IsUser = true, Author = "Du", Content = text, CreatedAt = DateTime.UtcNow });
        InputText = "";

        try
        {
            var result = await _api.SendMessageAsync(new SendMessageRequest
            {
                ConversationId = isNew ? null : SelectedConversation?.Id,
                ModelId = SelectedModel.ModelId,
                Message = text
            });

            if (!result.Ok || result.Value == null)
            {
                Messages.Add(new ChatDisplayItem
                {
                    IsUser = false,
                    Author = "System",
                    Content = "",
                    Document = MarkdownRenderer.ToFlowDocument("⚠️ " + (result.Error ?? "Anfrage fehlgeschlagen.")),
                    CreatedAt = DateTime.UtcNow
                });
                StatusMessage = result.Error;
                return;
            }

            var resp = result.Value;
            Messages.Add(new ChatDisplayItem
            {
                IsUser = false,
                Author = "Nexora AI",
                Content = resp.Content,
                Document = MarkdownRenderer.ToFlowDocument(resp.Content),
                CreatedAt = DateTime.UtcNow
            });

            if (isNew)
            {
                // Unterhaltungsliste aktualisieren und neue Unterhaltung auswählen.
                var convs = await _api.ConversationsAsync();
                if (convs.Ok && convs.Value != null)
                {
                    Conversations.Clear();
                    foreach (var c in convs.Value)
                        Conversations.Add(new ConversationItem
                        {
                            Id = c.Id, Title = c.Title, MessageCount = c.MessageCount, UpdatedAt = c.UpdatedAt
                        });
                    SelectedConversation = Conversations.FirstOrDefault(c => c.Id == resp.ConversationId);
                }
            }
            else if (SelectedConversation != null)
            {
                SelectedConversation.IncrementMessages(2);
            }

            UpdateQuotaDisplay(resp.Quota);
            ScrollRequested?.Invoke();
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task DeleteConversationAsync(ConversationItem? item)
    {
        if (item == null) return;
        var result = await _api.DeleteConversationAsync(item.Id);
        if (!result.Ok)
        {
            StatusMessage = result.Error;
            return;
        }
        Conversations.Remove(item);
        if (SelectedConversation == null || ReferenceEquals(SelectedConversation, item))
        {
            SelectedConversation = Conversations.FirstOrDefault();
            if (SelectedConversation == null)
            {
                Messages.Clear();
                NewConversation();
            }
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await Task.WhenAll(LoadLicensesAndQuotaAsync(), LoadConversationsAsync());
        ClearStatus();
    }

    [RelayCommand]
    private void ClearStatus() => StatusMessage = null;
}