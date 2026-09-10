namespace TelegramRelay.Services;

using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using TelegramRelay.Models;

/// <summary>
/// Service that formats Taiga event payloads into HTML and sends them via Telegram Bot API.
/// </summary>
public sealed class TelegramNotificationService(
    ITelegramBotClient botClient,
    ILogger<TelegramNotificationService> logger) : ITelegramNotificationService
{
    private readonly ITelegramBotClient _botClient = botClient ?? throw new ArgumentNullException(nameof(botClient));
    private readonly ILogger<TelegramNotificationService> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public string FormatMessage(TaigaWebhookPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string projectName = WebUtility.HtmlEncode(payload.Data?.Project?.Name ?? "Taiga Project");
        string itemType = WebUtility.HtmlEncode((payload.Type ?? "ITEM").ToUpperInvariant());
        string action = WebUtility.HtmlEncode(payload.Action ?? "update");
        string title = WebUtility.HtmlEncode(payload.Data?.Title ?? "Untitled");
        string author = WebUtility.HtmlEncode(payload.By?.FullName ?? payload.By?.Username ?? "Taiga User");
        string? link = payload.Data?.Link;

        var sb = new StringBuilder();
        sb.AppendLine($"🚀 <b>[{projectName}]</b> New <code>{itemType}</code> action: <b>{action}</b>");
        sb.AppendLine($"📌 <b>Title:</b> {title}");
        sb.AppendLine($"👤 <b>By:</b> {author}");

        if (!string.IsNullOrWhiteSpace(link))
        {
            string encodedLink = WebUtility.HtmlEncode(link);
            if (Uri.TryCreate(link, UriKind.Absolute, out _))
            {
                sb.Append($"🔗 <a href=\"{encodedLink}\">View in Taiga</a>");
            }
            else
            {
                sb.Append($"🔗 <code>{encodedLink}</code>");
            }
        }

        return sb.ToString().TrimEnd();
    }

    public async Task<bool> SendNotificationAsync(
        long chatId,
        TaigaWebhookPayload payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        string message = FormatMessage(payload);

        try
        {
            await _botClient.SendMessage(
                chatId: chatId,
                text: message,
                parseMode: ParseMode.Html,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Successfully sent Telegram notification to Chat ID {ChatId} for action '{Action}' on '{Type}'.",
                chatId, payload.Action, payload.Type);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification to Chat ID {ChatId}.", chatId);
            return false;
        }
    }
}
