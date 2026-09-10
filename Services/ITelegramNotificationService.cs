namespace TelegramRelay.Services;

using TelegramRelay.Models;

/// <summary>
/// Service for formatting and dispatching Taiga event notifications to Telegram.
/// </summary>
public interface ITelegramNotificationService
{
    /// <summary>
    /// Formats the Taiga webhook payload into a Telegram HTML-compatible notification string.
    /// </summary>
    /// <param name="payload">The parsed Taiga webhook payload.</param>
    /// <returns>HTML-formatted message string.</returns>
    string FormatMessage(TaigaWebhookPayload payload);

    /// <summary>
    /// Sends a formatted HTML notification to the target Telegram Chat ID.
    /// </summary>
    /// <param name="chatId">The target Telegram Chat ID.</param>
    /// <param name="payload">The parsed Taiga webhook payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if message was sent successfully; otherwise false.</returns>
    Task<bool> SendNotificationAsync(long chatId, TaigaWebhookPayload payload, CancellationToken cancellationToken = default);
}

