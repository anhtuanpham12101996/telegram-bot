namespace TelegramRelay.Services;

using TelegramRelay.Models;

public sealed record GitLabNotificationJob(
    long ChatId,
    GitLabWebhookPayload Payload,
    GitLabNotifyKind Kind);
