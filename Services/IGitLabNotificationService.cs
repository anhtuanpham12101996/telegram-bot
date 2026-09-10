namespace TelegramRelay.Services;

using TelegramRelay.Models;

/// <summary>
/// Formats and dispatches GitLab merge-request events to Telegram.
/// </summary>
public interface IGitLabNotificationService
{
    string FormatMessage(GitLabWebhookPayload payload, GitLabNotifyKind kind);

    Task<bool> SendNotificationAsync(
        long chatId,
        GitLabWebhookPayload payload,
        GitLabNotifyKind kind,
        CancellationToken cancellationToken = default);
}
