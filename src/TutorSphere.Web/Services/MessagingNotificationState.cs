using TutorSphere.Application.DTOs.Messages;

namespace TutorSphere.Web.Services;

/// <summary>Circuit-scoped unread count + subtle toast for live messages.</summary>
public sealed class MessagingNotificationState
{
    private CancellationTokenSource? _toastCts;

    /// <summary>When set, incoming messages from this peer do not bump the badge.</summary>
    public string? ActiveConversationUserId { get; set; }

    public int UnreadTotal { get; private set; }
    public bool ShowToast { get; private set; }
    public string? ToastFrom { get; private set; }
    public string? ToastPreview { get; private set; }

    public event Action? Changed;

    public void SetUnreadTotal(int total)
    {
        UnreadTotal = Math.Max(0, total);
        Notify();
    }

    public void AdjustUnread(int delta)
    {
        UnreadTotal = Math.Max(0, UnreadTotal + delta);
        Notify();
    }

    public void HandleIncoming(MessageDto message, string? senderDisplayName = null)
    {
        if (!string.IsNullOrEmpty(ActiveConversationUserId)
            && string.Equals(ActiveConversationUserId, message.SenderUserId, StringComparison.Ordinal))
        {
            return;
        }

        UnreadTotal++;
        ShowIncomingToast(senderDisplayName ?? "Nouveau message", message.Body);
    }

    public void ShowIncomingToast(string from, string preview)
    {
        ToastFrom = from;
        ToastPreview = Truncate(preview, 72);
        ShowToast = true;
        Notify();

        _toastCts?.Cancel();
        _toastCts = new CancellationTokenSource();
        var token = _toastCts.Token;
        _ = DismissAfterDelayAsync(token);
    }

    public void DismissToast()
    {
        ShowToast = false;
        ToastFrom = null;
        ToastPreview = null;
        Notify();
    }

    private async Task DismissAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(4500, ct);
            if (!ct.IsCancellationRequested)
                DismissToast();
        }
        catch (OperationCanceledException) { /* replaced by newer toast */ }
    }

    private void Notify() => Changed?.Invoke();

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        text = text.Trim();
        return text.Length <= max ? text : text[..(max - 1)] + "…";
    }
}
